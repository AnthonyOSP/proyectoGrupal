using Microsoft.ML;

namespace proyectoGrupal.Services.Categorias;

// Categoría sugerida para un texto y la probabilidad que le da el modelo (0 a 1).
public record SugerenciaCategoria(string Categoria, float Confianza);

// Consumo (y reentrenamiento) del clasificador de categorías (MLModels/CategoriaModel.zip).
public interface ICategoriaSugeridaService
{
    bool EstaDisponible { get; }

    InfoEntrenamientoCategorias? Info { get; }

    // null si no hay modelo, si el texto está vacío o si falló la predicción (nunca lanza excepciones).
    SugerenciaCategoria? Sugerir(string texto);

    // Entrena un modelo nuevo con el CSV + las incidencias reales y lo empieza a usar enseguida.
    Task<InfoEntrenamientoCategorias> ReentrenarAsync(IReadOnlyCollection<EntradaCategoria> incidencias);
}

// Singleton. Un PredictionEngine no se puede usar desde varios hilos a la vez: las predicciones pasan
// por un candado (cada una tarda menos de un milisegundo). El candado también permite cambiar de modelo.
public class CategoriaSugeridaService : ICategoriaSugeridaService
{
    public static readonly string RutaRelativaModelo = Path.Combine("MLModels", "CategoriaModel.zip");

    // El CSV está en ML/ en el repositorio; al publicar se copia a MLModels/ (ver proyectoGrupal.csproj).
    private static readonly string[] RutasRelativasCsv =
    {
        Path.Combine("ML", "categorizacionIncidencias", "trainingData", "incidencias_entrenamiento.csv"),
        Path.Combine("MLModels", "incidencias_entrenamiento.csv")
    };

    private readonly MLContext _ml = new(seed: 1);
    private readonly object _candado = new();
    private readonly SemaphoreSlim _entrenando = new(1, 1);
    private readonly ILogger<CategoriaSugeridaService> _logger;
    private readonly string _rutaModelo;
    private readonly string _raiz;

    private PredictionEngine<EntradaCategoria, PrediccionCategoria>? _motor;
    private string[] _clases = Array.Empty<string>();

    public CategoriaSugeridaService(IWebHostEnvironment entorno, ILogger<CategoriaSugeridaService> logger)
    {
        _logger = logger;
        _raiz = entorno.ContentRootPath;
        _rutaModelo = Path.Combine(_raiz, RutaRelativaModelo);
        CargarModelo();
    }

    public bool EstaDisponible
    {
        get { lock (_candado) { return _motor != null; } }
    }

    public InfoEntrenamientoCategorias? Info { get; private set; }

    public SugerenciaCategoria? Sugerir(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        try
        {
            lock (_candado)
            {
                if (_motor == null)
                {
                    return null;
                }

                var prediccion = _motor.Predict(new EntradaCategoria { Texto = texto });
                var indice = Array.IndexOf(_clases, prediccion.Categoria);
                var confianza = indice >= 0 && indice < prediccion.Score.Length ? prediccion.Score[indice] : 0f;
                return new SugerenciaCategoria(prediccion.Categoria, confianza);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo sugerir la categoría.");
            return null;
        }
    }

    public async Task<InfoEntrenamientoCategorias> ReentrenarAsync(IReadOnlyCollection<EntradaCategoria> incidencias)
    {
        var rutaCsv = RutasRelativasCsv.Select(r => Path.Combine(_raiz, r)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("No se encontró el archivo de entrenamiento incidencias_entrenamiento.csv.");

        await _entrenando.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                var (modelo, esquema, info) = EntrenadorCategorias.Entrenar(_ml, rutaCsv, incidencias);
                EntrenadorCategorias.Guardar(_ml, modelo, esquema, info, _rutaModelo);
                Usar(modelo, info);
                _logger.LogInformation(
                    "Clasificador de categorías reentrenado: {Csv} ejemplos del CSV + {Incidencias} incidencias, exactitud {Exactitud:P1}.",
                    info.EjemplosCsv, info.EjemplosIncidencias, info.ExactitudMicro);
                return info;
            });
        }
        finally
        {
            _entrenando.Release();
        }
    }

    private void CargarModelo()
    {
        if (!File.Exists(_rutaModelo))
        {
            _logger.LogWarning("No se encontró el clasificador de categorías ({Ruta}): el formulario Reportar no sugerirá categorías. " +
                "Entrénalo con: dotnet run --project ML/categorizacionIncidencias/Entrenamiento", _rutaModelo);
            return;
        }

        try
        {
            Usar(_ml.Model.Load(_rutaModelo, out _), EntrenadorCategorias.LeerInfo(_rutaModelo));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo cargar el clasificador de categorías ({Ruta}).", _rutaModelo);
        }
    }

    private void Usar(ITransformer modelo, InfoEntrenamientoCategorias? info)
    {
        var motor = _ml.Model.CreatePredictionEngine<EntradaCategoria, PrediccionCategoria>(modelo);
        var clases = EntrenadorCategorias.NombresDeClases(motor);
        lock (_candado)
        {
            _motor = motor;
            _clases = clases;
            Info = info;
        }
    }
}
