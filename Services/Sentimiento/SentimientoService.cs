using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using proyectoGrupal.Data;
using proyectoGrupal.Models;

namespace proyectoGrupal.Services.Sentimiento;

// Resultado del análisis de un mensaje: "Positivo" o "Negativo" y la probabilidad de que sea positivo.
public record ResultadoSentimiento(string Sentimiento, float ProbabilidadPositiva);

// Consumo (y reentrenamiento) del modelo de sentimientos de ML.NET (MLModels/SentimientoModel.zip).
public interface ISentimientoService
{
    // false si no hay modelo cargado: los mensajes se guardan igual, sin sentimiento.
    bool EstaDisponible { get; }

    // Datos del último entrenamiento (fecha, ejemplos, exactitud), o null si no se conocen.
    InfoEntrenamiento? Info { get; }

    // null si no hay modelo o si falló la predicción (nunca lanza excepciones).
    ResultadoSentimiento? Analizar(string texto);

    // Entrena un modelo nuevo con el CSV + las correcciones del administrador, lo guarda y lo
    // empieza a usar enseguida (sin reiniciar la aplicación).
    Task<InfoEntrenamiento> ReentrenarAsync(IReadOnlyCollection<EntradaSentimiento> correcciones);
}

// Singleton. Un PredictionEngine de ML.NET NO se puede usar desde varios hilos a la vez: las
// predicciones pasan por un candado. Cada una tarda menos de un milisegundo, así que alcanza de sobra
// para un formulario de contacto. El candado también permite cambiar de modelo al reentrenar.
public class SentimientoService : ISentimientoService
{
    // Modelo y datos de entrenamiento, relativos a la raíz de la aplicación (ContentRootPath).
    public static readonly string RutaRelativaModelo = Path.Combine("MLModels", "SentimientoModel.zip");

    // El CSV está en ML/ en el repositorio; al publicar se copia a MLModels/ (ver proyectoGrupal.csproj).
    private static readonly string[] RutasRelativasCsv =
    {
        Path.Combine("ML", "sentimentAnalysisNLP", "trainingData", "comentarios_entrenamiento.csv"),
        Path.Combine("MLModels", "comentarios_entrenamiento.csv")
    };

    private readonly MLContext _ml = new(seed: 1);
    private readonly object _candado = new();
    private readonly SemaphoreSlim _entrenando = new(1, 1);
    private readonly ILogger<SentimientoService> _logger;
    private readonly string _rutaModelo;
    private readonly string _raiz;

    private PredictionEngine<EntradaSentimiento, PrediccionSentimiento>? _motor;

    public SentimientoService(IWebHostEnvironment entorno, ILogger<SentimientoService> logger)
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

    public InfoEntrenamiento? Info { get; private set; }

    public ResultadoSentimiento? Analizar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        try
        {
            PrediccionSentimiento prediccion;
            lock (_candado)
            {
                if (_motor == null)
                {
                    return null;
                }
                prediccion = _motor.Predict(new EntradaSentimiento { Comentario = texto });
            }

            var sentimiento = prediccion.EsPositivo ? SentimientosMensaje.Positivo : SentimientosMensaje.Negativo;
            return new ResultadoSentimiento(sentimiento, prediccion.Probability);
        }
        catch (Exception ex)
        {
            // Un fallo del modelo nunca debe impedir que se guarde el mensaje de contacto.
            _logger.LogError(ex, "No se pudo analizar el sentimiento del mensaje.");
            return null;
        }
    }

    public async Task<InfoEntrenamiento> ReentrenarAsync(IReadOnlyCollection<EntradaSentimiento> correcciones)
    {
        var rutaCsv = RutasRelativasCsv.Select(r => Path.Combine(_raiz, r)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("No se encontró el archivo de entrenamiento comentarios_entrenamiento.csv.");

        // Un solo entrenamiento a la vez (por si dos administradores presionan el botón juntos).
        await _entrenando.WaitAsync();
        try
        {
            // Entrenar usa la CPU un par de segundos: se hace fuera del hilo de la petición.
            return await Task.Run(() =>
            {
                var (modelo, esquema, info) = EntrenadorSentimiento.Entrenar(_ml, rutaCsv, correcciones);
                EntrenadorSentimiento.Guardar(_ml, modelo, esquema, info, _rutaModelo);

                var motorNuevo = _ml.Model.CreatePredictionEngine<EntradaSentimiento, PrediccionSentimiento>(modelo);
                lock (_candado)
                {
                    _motor = motorNuevo;
                    Info = info;
                }

                _logger.LogInformation(
                    "Modelo de sentimientos reentrenado: {Csv} ejemplos del CSV + {Correcciones} correcciones, exactitud {Exactitud:P1}.",
                    info.EjemplosCsv, info.Correcciones, info.Exactitud);
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
            _logger.LogWarning("No se encontró el modelo de sentimientos ({Ruta}): los mensajes de contacto se guardan sin sentimiento. " +
                "Entrénalo con: dotnet run --project ML/sentimentAnalysisNLP/Entrenamiento", _rutaModelo);
            return;
        }

        try
        {
            var modelo = _ml.Model.Load(_rutaModelo, out _);
            _motor = _ml.Model.CreatePredictionEngine<EntradaSentimiento, PrediccionSentimiento>(modelo);
            Info = EntrenadorSentimiento.LeerInfo(_rutaModelo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo cargar el modelo de sentimientos ({Ruta}).", _rutaModelo);
        }
    }

    // Analiza los mensajes sin sentimiento (anteriores a esta función o guardados sin modelo).
    // Con todosLosAutomaticos = true vuelve a analizar también los que ya tenían sentimiento del modelo
    // (después de reentrenar). Nunca toca los que corrigió el administrador.
    public static async Task<int> AnalizarMensajesAsync(ApplicationDbContext context, ISentimientoService sentimiento, bool todosLosAutomaticos = false)
    {
        if (!sentimiento.EstaDisponible)
        {
            return 0;
        }

        var mensajes = await context.MensajesContacto
            .Where(m => !m.SentimientoManual && (todosLosAutomaticos || m.Sentimiento == null))
            .ToListAsync();

        foreach (var mensaje in mensajes)
        {
            var resultado = sentimiento.Analizar(mensaje.Mensaje);
            mensaje.Sentimiento = resultado?.Sentimiento;
            mensaje.ProbabilidadPositiva = resultado?.ProbabilidadPositiva;
        }

        await context.SaveChangesAsync();
        return mensajes.Count;
    }
}
