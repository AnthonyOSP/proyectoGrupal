using System.Text.Json.Serialization;
using Algolia.Search.Clients;
using Algolia.Search.Exceptions;
using Algolia.Search.Models.Search;
using Algolia.Search.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using proyectoGrupal.Models;

namespace proyectoGrupal.Services.Algolia;

// Implementación con el cliente oficial Algolia.Search (v7).
// Se registra como singleton: un solo SearchClient reutilizado por toda la aplicación.
// Si Algolia no está configurado, todos los métodos devuelven "sin éxito" sin intentar conectarse.
public class AlgoliaIncidenciaService : IAlgoliaIncidenciaService
{
    // Tiempos máximos por operación: si Algolia no responde, el usuario no espera más que esto.
    private static readonly TimeSpan LimiteBusqueda = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LimiteEscritura = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LimiteSincronizacion = TimeSpan.FromMinutes(2);

    // Máximo de resultados que Algolia devuelve por búsqueda (límite de su paginación).
    private const int MaximoResultados = 1000;

    private readonly AlgoliaOptions _opciones;
    private readonly ILogger<AlgoliaIncidenciaService> _logger;
    private readonly SearchClient? _cliente;

    // La configuración del índice (campos de búsqueda, filtros, orden) se aplica una vez por ejecución.
    private readonly SemaphoreSlim _candadoConfiguracion = new(1, 1);
    private bool _indiceConfigurado;

    // Pausa tras un fallo: durante este tiempo no se vuelve a llamar a Algolia (búsquedas y escrituras
    // responden enseguida con "sin éxito"), para que un Algolia caído no haga esperar cada página.
    // La sincronización manual del administrador ignora la pausa.
    private static readonly TimeSpan Pausa = TimeSpan.FromSeconds(30);
    private long _pausaHastaTicks;

    private bool EnPausa => DateTime.UtcNow.Ticks < Interlocked.Read(ref _pausaHastaTicks);

    public AlgoliaIncidenciaService(IOptions<AlgoliaOptions> opciones, IWebHostEnvironment entorno, ILogger<AlgoliaIncidenciaService> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;

        if (!_opciones.EstaConfigurado)
        {
            _logger.LogWarning(
                "Algolia no está configurado (faltan ALGOLIA_APPLICATION_ID y/o ALGOLIA_ADMIN_API_KEY). " +
                "La búsqueda de incidencias usará SQLite.");
            return;
        }

        var config = new SearchConfig(_opciones.ApplicationId!, _opciones.AdminApiKey!)
        {
            ConnectTimeout = TimeSpan.FromSeconds(2),
            ReadTimeout = LimiteBusqueda,
            WriteTimeout = LimiteEscritura
        };

        // Servidor de pruebas local (solo en Development). En producción siempre se usan los de Algolia.
        if (entorno.IsDevelopment() && Uri.TryCreate(_opciones.HostPruebas, UriKind.Absolute, out var host))
        {
            config.CustomHosts = new List<StatefulHost>
            {
                new()
                {
                    Url = host.Host,
                    Port = host.Port,
                    Scheme = host.Scheme == "http" ? HttpScheme.Http : HttpScheme.Https,
                    Up = true,
                    Accept = CallType.Read | CallType.Write
                }
            };
            _logger.LogWarning("Algolia usa el servidor de pruebas {Host} (solo Development).", host.Authority);
        }

        // NullLoggerFactory: el cliente no escribe en el log detalles de las peticiones (cabeceras con la clave).
        _cliente = new SearchClient(config, NullLoggerFactory.Instance);
        _logger.LogInformation("Algolia configurado. Índice de búsqueda: {Indice}", _opciones.IndexName);
    }

    public bool EstaConfigurado => _cliente != null;

    public string NombreIndice => _opciones.IndexName;

    public async Task<bool> IndexarAsync(Incidencia incidencia, CancellationToken cancellationToken = default)
    {
        if (_cliente == null)
        {
            return false;
        }

        if (EnPausa)
        {
            _logger.LogWarning("Algolia en pausa tras un fallo reciente: la incidencia {Id} no se indexó (usar la resincronización).", incidencia.Id);
            return false;
        }

        using var limite = LimitarTiempo(LimiteEscritura, cancellationToken);
        try
        {
            await AsegurarConfiguracionAsync(esperar: false, limite.Token);
            var documento = IncidenciaAlgoliaDocumento.DesdeEntidad(incidencia);
            await _cliente.AddOrUpdateObjectAsync(_opciones.IndexName, documento.ObjectID, documento, cancellationToken: limite.Token);
            return true;
        }
        catch (Exception ex) when (EsFalloDeAlgolia(ex))
        {
            RegistrarFallo(ex, "indexar la incidencia {Id}", incidencia.Id);
            return false;
        }
    }

    public async Task<bool> EliminarAsync(int incidenciaId, CancellationToken cancellationToken = default)
    {
        if (_cliente == null)
        {
            return false;
        }

        if (EnPausa)
        {
            _logger.LogWarning("Algolia en pausa tras un fallo reciente: la incidencia {Id} no se quitó del índice (usar la resincronización).", incidenciaId);
            return false;
        }

        using var limite = LimitarTiempo(LimiteEscritura, cancellationToken);
        try
        {
            await _cliente.DeleteObjectAsync(_opciones.IndexName, incidenciaId.ToString(), cancellationToken: limite.Token);
            return true;
        }
        catch (Exception ex) when (EsFalloDeAlgolia(ex))
        {
            RegistrarFallo(ex, "eliminar del índice la incidencia {Id}", incidenciaId);
            return false;
        }
    }

    public async Task<ResultadoBusquedaAlgolia> BuscarIdsAsync(string texto, string? categoria, string? estado, CancellationToken cancellationToken = default)
    {
        // En pausa: se responde enseguida y la página usa la búsqueda de SQLite, sin volver a esperar.
        if (_cliente == null || EnPausa)
        {
            return ResultadoBusquedaAlgolia.Fallo();
        }

        // Filtros exactos (se combinan con Y). Solo sobre los atributos configurados para filtrar.
        var filtros = new List<FacetFilters>();
        if (!string.IsNullOrEmpty(categoria))
        {
            filtros.Add(new FacetFilters($"categoria:{categoria}"));
        }
        if (!string.IsNullOrEmpty(estado))
        {
            filtros.Add(new FacetFilters($"estado:{estado}"));
        }

        var parametros = new SearchParamsObject
        {
            Query = texto,
            HitsPerPage = MaximoResultados,
            // Solo se piden los Id: los datos que se muestran salen siempre de SQLite.
            AttributesToRetrieve = new List<string> { "objectID" },
            AttributesToHighlight = new List<string>(),
            AttributesToSnippet = new List<string>(),
            FacetFilters = filtros.Count > 0 ? new FacetFilters(filtros) : null
        };

        using var limite = LimitarTiempo(LimiteBusqueda, cancellationToken);
        try
        {
            await AsegurarConfiguracionAsync(esperar: false, limite.Token);
            var respuesta = await _cliente.SearchSingleIndexAsync<ResultadoId>(
                _opciones.IndexName, new SearchParams(parametros), cancellationToken: limite.Token);

            var ids = respuesta.Hits
                .Select(h => int.TryParse(h.ObjectID, out var id) ? id : (int?)null)
                .Where(id => id != null)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            return new ResultadoBusquedaAlgolia(true, ids);
        }
        catch (Exception ex) when (EsFalloDeAlgolia(ex))
        {
            RegistrarFallo(ex, "buscar \"{Texto}\"", texto);
            return ResultadoBusquedaAlgolia.Fallo();
        }
    }

    public async Task<ResultadoSincronizacion> SincronizarTodoAsync(IReadOnlyCollection<Incidencia> incidencias, CancellationToken cancellationToken = default)
    {
        if (_cliente == null)
        {
            return new ResultadoSincronizacion(false, 0, "Algolia no está configurado.");
        }

        using var limite = LimitarTiempo(LimiteSincronizacion, cancellationToken);
        try
        {
            // 1. Configuración del índice (también lo crea si no existe) y esperar a que se aplique.
            await AsegurarConfiguracionAsync(esperar: true, limite.Token, forzar: true);

            // 2. Reemplazo completo: Algolia arma un índice temporal y lo cambia por el actual de una vez.
            //    Como objectID = Id de SQLite, no quedan duplicados ni registros de incidencias que ya no existen.
            var documentos = incidencias.Select(IncidenciaAlgoliaDocumento.DesdeEntidad).ToList();
            if (documentos.Count == 0)
            {
                var vaciado = await _cliente.ClearObjectsAsync(_opciones.IndexName, cancellationToken: limite.Token);
                await _cliente.WaitForTaskAsync(_opciones.IndexName, vaciado.TaskID, ct: limite.Token);
            }
            else
            {
                await _cliente.ReplaceAllObjectsAsync(
                    _opciones.IndexName, documentos, 1000,
                    new List<ScopeType> { ScopeType.Settings, ScopeType.Synonyms, ScopeType.Rules },
                    cancellationToken: limite.Token);
            }

            // Algolia respondió: se termina cualquier pausa por fallos anteriores.
            Interlocked.Exchange(ref _pausaHastaTicks, 0);
            _logger.LogInformation("Índice de Algolia sincronizado: {Cantidad} incidencias.", documentos.Count);
            return new ResultadoSincronizacion(true, documentos.Count, $"Se indexaron {documentos.Count} incidencias.");
        }
        catch (Exception ex) when (EsFalloDeAlgolia(ex))
        {
            RegistrarFallo(ex, "sincronizar el índice {Indice}", _opciones.IndexName);
            return new ResultadoSincronizacion(false, 0, "No se pudo sincronizar con Algolia. Revisa la configuración y el log del servidor.");
        }
    }

    // Campos de búsqueda, filtros y orden del índice.
    private async Task AsegurarConfiguracionAsync(bool esperar, CancellationToken cancellationToken, bool forzar = false)
    {
        if (_indiceConfigurado && !forzar)
        {
            return;
        }

        await _candadoConfiguracion.WaitAsync(cancellationToken);
        try
        {
            if (_indiceConfigurado && !forzar)
            {
                return;
            }

            var ajustes = new IndexSettings
            {
                // Texto libre: el orden define la importancia (el título pesa más que la descripción).
                SearchableAttributes = new List<string> { "titulo", "descripcion", "categoria", "ubicacion" },
                // Solo estos atributos se pueden usar como filtro (no se habilitan filtros arbitrarios).
                AttributesForFaceting = new List<string> { "filterOnly(categoria)", "filterOnly(estado)" },
                NumericAttributesForFiltering = new List<string> { "fechaRegistroTimestamp" },
                // A igual relevancia, primero las incidencias más recientes.
                CustomRanking = new List<string> { "desc(fechaRegistroTimestamp)" },
                // Idioma español: plurales ("postes" = "poste") y tolerancia a errores de tipeo.
                QueryLanguages = new List<SupportedLanguage> { SupportedLanguage.Es },
                IndexLanguages = new List<SupportedLanguage> { SupportedLanguage.Es },
                IgnorePlurals = new IgnorePlurals(true),
                TypoTolerance = new TypoTolerance(TypoToleranceEnum.True),
                PaginationLimitedTo = MaximoResultados
            };

            var respuesta = await _cliente!.SetSettingsAsync(_opciones.IndexName, ajustes, cancellationToken: cancellationToken);
            if (esperar)
            {
                await _cliente.WaitForTaskAsync(_opciones.IndexName, respuesta.TaskID, ct: cancellationToken);
            }

            _indiceConfigurado = true;
        }
        finally
        {
            _candadoConfiguracion.Release();
        }
    }

    private static CancellationTokenSource LimitarTiempo(TimeSpan limite, CancellationToken cancellationToken)
    {
        var fuente = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        fuente.CancelAfter(limite);
        return fuente;
    }

    // Errores de red, de la API de Algolia o tiempo agotado. Los errores de programación no se ocultan.
    // En el cliente v7, AlgoliaApiException (p. ej. clave rechazada) y AlgoliaUnreachableHostException
    // (servicio caído o inalcanzable) NO heredan de AlgoliaException: se nombran explícitamente.
    private static bool EsFalloDeAlgolia(Exception ex) =>
        ex is AlgoliaException or AlgoliaApiException or AlgoliaUnreachableHostException
            or HttpRequestException or OperationCanceledException or TimeoutException
            or System.Text.Json.JsonException;

    // Solo el tipo y el mensaje del error: nunca la configuración ni las cabeceras (que incluyen la clave).
    private void RegistrarFallo(Exception ex, string operacion, object? dato)
    {
        Interlocked.Exchange(ref _pausaHastaTicks, DateTime.UtcNow.Add(Pausa).Ticks);
        _logger.LogWarning("Algolia: no se pudo " + operacion + ". {Tipo}: {Mensaje}", dato, ex.GetType().Name, ex.Message);
    }

    // Resultado mínimo de una búsqueda: solo el objectID.
    internal sealed class ResultadoId
    {
        [JsonPropertyName("objectID")]
        public string? ObjectID { get; set; }
    }
}
