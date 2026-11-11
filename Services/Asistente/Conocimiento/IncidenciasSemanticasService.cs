using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using proyectoGrupal.Models;

namespace proyectoGrupal.Services.Asistente.Conocimiento;

public record CoincidenciaSemantica(int IncidenciaId, double Puntaje);

// Índice semántico de las incidencias en Qdrant: permite encontrar reportes por SIGNIFICADO
// ("hueco en la pista" encuentra "bache"). SQLite sigue siendo la fuente de verdad: aquí solo se guarda
// el texto convertido en vector, con el Id, el estado y la categoría para filtrar.
//   - Se actualiza al crear un reporte, al cambiar su estado y al eliminarlo (igual que Algolia).
//   - Si Qdrant no está configurado o falla, nada se rompe: el chat usa la búsqueda de siempre.
public interface IIncidenciasSemanticasService
{
    bool EstaConfigurado { get; }

    string Coleccion { get; }

    Task IndexarAsync(Incidencia incidencia, CancellationToken cancellationToken = default);

    Task ActualizarEstadoAsync(int incidenciaId, string estado, CancellationToken cancellationToken = default);

    Task EliminarAsync(int incidenciaId, CancellationToken cancellationToken = default);

    // Reemplaza el índice completo con las incidencias de SQLite (se puede repetir sin duplicar).
    Task<ResultadoOperacionConocimiento> SincronizarTodoAsync(IReadOnlyList<Incidencia> incidencias, CancellationToken cancellationToken = default);

    // null si Qdrant no está configurado o no responde; 0 si la colección no existe.
    Task<long?> ContarAsync(CancellationToken cancellationToken = default);

    // Incidencias más parecidas al texto, de mayor a menor puntaje (vacío si Qdrant no está disponible).
    Task<IReadOnlyList<CoincidenciaSemantica>> BuscarAsync(string texto, int limite, bool soloAbiertas, CancellationToken cancellationToken = default);
}

public class IncidenciasSemanticasService : IIncidenciasSemanticasService
{
    private const int PuntosPorLote = 32;
    private static readonly TimeSpan LimiteOperacion = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan LimiteSincronizacion = TimeSpan.FromMinutes(2);

    private readonly ClienteQdrant _qdrant;
    private readonly ILogger<IncidenciasSemanticasService> _logger;

    public IncidenciasSemanticasService(ClienteQdrant qdrant, ILogger<IncidenciasSemanticasService> logger)
    {
        _qdrant = qdrant;
        _logger = logger;
    }

    public bool EstaConfigurado => _qdrant.EstaConfigurado;

    public string Coleccion => _qdrant.Opciones.ColeccionIncidencias;

    // Texto que se convierte en vector: lo que describe el problema y dónde ocurre.
    private static string Texto(Incidencia i) =>
        $"{i.Titulo}. {i.Descripcion} Ubicación: {i.Ubicacion}. Categoría: {i.Categoria}.";

    private object Punto(Incidencia i) => new
    {
        id = i.Id,
        vector = new Dictionary<string, object> { [ClienteQdrant.NombreVector] = _qdrant.Documento("passage: " + Texto(i)) },
        payload = new { incidenciaId = i.Id, estado = i.Estado, categoria = i.Categoria }
    };

    public Task IndexarAsync(Incidencia incidencia, CancellationToken cancellationToken = default) =>
        IntentarAsync($"indexar la incidencia {incidencia.Id}", async ct =>
        {
            await _qdrant.AsegurarColeccionAsync(Coleccion, ["estado", "categoria"], ct);
            using var respuesta = await _qdrant.Crear().PutAsJsonAsync(
                $"collections/{Coleccion}/points?wait=true", new { points = new[] { Punto(incidencia) } }, ct);
            await _qdrant.AsegurarExitoAsync(respuesta, "indexar la incidencia", ct);
        }, cancellationToken);

    public Task ActualizarEstadoAsync(int incidenciaId, string estado, CancellationToken cancellationToken = default) =>
        IntentarAsync($"actualizar el estado de la incidencia {incidenciaId}", async ct =>
        {
            // Solo cambia el dato para filtrar: el texto (y su vector) no cambia.
            using var respuesta = await _qdrant.Crear().PostAsJsonAsync(
                $"collections/{Coleccion}/points/payload?wait=true",
                new { payload = new { estado }, points = new[] { incidenciaId } }, ct);
            if (respuesta.StatusCode != HttpStatusCode.NotFound)
            {
                await _qdrant.AsegurarExitoAsync(respuesta, "actualizar el estado", ct);
            }
        }, cancellationToken);

    public Task EliminarAsync(int incidenciaId, CancellationToken cancellationToken = default) =>
        IntentarAsync($"eliminar la incidencia {incidenciaId}", async ct =>
        {
            using var respuesta = await _qdrant.Crear().PostAsJsonAsync(
                $"collections/{Coleccion}/points/delete?wait=true", new { points = new[] { incidenciaId } }, ct);
            if (respuesta.StatusCode != HttpStatusCode.NotFound)
            {
                await _qdrant.AsegurarExitoAsync(respuesta, "eliminar la incidencia", ct);
            }
        }, cancellationToken);

    public async Task<ResultadoOperacionConocimiento> SincronizarTodoAsync(IReadOnlyList<Incidencia> incidencias, CancellationToken cancellationToken = default)
    {
        if (!EstaConfigurado)
        {
            return new(false, "Qdrant no está configurado: no hay índice de incidencias que sincronizar.");
        }

        try
        {
            using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tiempo.CancelAfter(LimiteSincronizacion);

            // Se borra la colección y se vuelve a crear: así no quedan incidencias que ya no existen.
            using (var borrada = await _qdrant.Crear().DeleteAsync($"collections/{Coleccion}", tiempo.Token))
            {
                if (borrada.StatusCode != HttpStatusCode.NotFound)
                {
                    await _qdrant.AsegurarExitoAsync(borrada, "vaciar el índice de incidencias", tiempo.Token);
                }
            }
            await _qdrant.AsegurarColeccionAsync(Coleccion, ["estado", "categoria"], tiempo.Token);

            foreach (var lote in incidencias.Select(Punto).Chunk(PuntosPorLote))
            {
                using var respuesta = await _qdrant.Crear().PutAsJsonAsync(
                    $"collections/{Coleccion}/points?wait=true", new { points = lote }, tiempo.Token);
                await _qdrant.AsegurarExitoAsync(respuesta, "cargar incidencias", tiempo.Token);
            }

            _logger.LogInformation("Qdrant | Índice de incidencias sincronizado: {Cantidad} incidencias.", incidencias.Count);
            return new(true, $"Índice de incidencias sincronizado: {incidencias.Count} incidencia(s) cargadas en Qdrant.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
        {
            _logger.LogError(ex, "Qdrant | No se pudo sincronizar el índice de incidencias.");
            return new(false, "No se pudo sincronizar el índice de incidencias con Qdrant. Revisa el log del servidor.");
        }
    }

    public async Task<long?> ContarAsync(CancellationToken cancellationToken = default)
    {
        if (!EstaConfigurado)
        {
            return null;
        }

        try
        {
            using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tiempo.CancelAfter(LimiteOperacion);
            using var respuesta = await _qdrant.Crear().PostAsJsonAsync(
                $"collections/{Coleccion}/points/count", new { exact = true }, tiempo.Token);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return 0;
            }
            await _qdrant.AsegurarExitoAsync(respuesta, "contar las incidencias indexadas", tiempo.Token);

            using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(tiempo.Token));
            return json.RootElement.GetProperty("result").GetProperty("count").GetInt64();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Qdrant | No se pudo contar el índice de incidencias.");
            return null;
        }
    }

    public async Task<IReadOnlyList<CoincidenciaSemantica>> BuscarAsync(string texto, int limite, bool soloAbiertas, CancellationToken cancellationToken = default)
    {
        if (!EstaConfigurado || string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        try
        {
            using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tiempo.CancelAfter(LimiteOperacion);

            var cuerpo = new Dictionary<string, object>
            {
                ["query"] = _qdrant.Documento("query: " + texto.Trim()),
                ["using"] = ClienteQdrant.NombreVector,
                ["limit"] = limite,
                ["with_payload"] = false
            };
            if (soloAbiertas)
            {
                cuerpo["filter"] = new { must_not = new[] { new { key = "estado", match = new { value = EstadosIncidencia.Atendida } } } };
            }

            using var respuesta = await _qdrant.Crear().PostAsJsonAsync($"collections/{Coleccion}/points/query", cuerpo, tiempo.Token);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }
            await _qdrant.AsegurarExitoAsync(respuesta, "buscar incidencias por significado", tiempo.Token);

            using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(tiempo.Token));
            return json.RootElement.GetProperty("result").GetProperty("points").EnumerateArray()
                .Select(p => new CoincidenciaSemantica(p.GetProperty("id").GetInt32(), p.GetProperty("score").GetDouble()))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Qdrant | No se pudo buscar incidencias por significado.");
            return [];
        }
    }

    // Las escrituras nunca lanzan excepciones: la incidencia ya está en SQLite y el administrador puede
    // resincronizar el índice desde el panel.
    private async Task IntentarAsync(string accion, Func<CancellationToken, Task> operacion, CancellationToken cancellationToken)
    {
        if (!EstaConfigurado)
        {
            return;
        }

        try
        {
            using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tiempo.CancelAfter(LimiteOperacion);
            await operacion(tiempo.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Qdrant | No se pudo {Accion}. Se puede resincronizar desde el panel.", accion);
        }
    }
}
