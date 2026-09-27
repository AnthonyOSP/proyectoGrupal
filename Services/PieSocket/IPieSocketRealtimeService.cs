using proyectoGrupal.Models;

namespace proyectoGrupal.Services.PieSocket;

// Tiempo real con PieSocket. Solo transporta avisos: nunca guarda datos (SQLite es la fuente de verdad).
// Los métodos de publicación no lanzan excepciones por fallos de PieSocket: devuelven false y lo
// registran, para que un fallo de PieSocket nunca afecte a lo ya guardado en SQLite.
public interface IPieSocketRealtimeService
{
    bool EstaConfigurado { get; }

    // Datos PÚBLICOS para que el navegador se conecte (nunca incluyen el API secret).
    // Ej: "wss://CLUSTER_ID.piesocket.com/v4". Null si no está configurado.
    string? UrlWebSocket { get; }

    string? ApiKeyPublica { get; }

    // JWT HS256 firmado con el API secret, válido solo para ese canal y por pocos minutos.
    // Quien llama debe haber comprobado antes que el usuario puede escuchar el canal.
    string CrearJwt(string canal);

    // Aviso de nueva incidencia al canal de administración.
    Task<bool> NotificarIncidenciaCreadaAsync(Incidencia incidencia, CancellationToken cancellationToken = default);

    // Aviso de cambio de estado al canal de la incidencia y al de administración.
    Task<bool> NotificarEstadoActualizadoAsync(Incidencia incidencia, string estadoAnterior, DateTime fecha, CancellationToken cancellationToken = default);
}
