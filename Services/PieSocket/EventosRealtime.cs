using System.Text.Json.Serialization;

namespace proyectoGrupal.Services.PieSocket;

// Eventos que el servidor publica en PieSocket. Solo llevan datos que ya son públicos
// (nunca usuario, correo ni datos de Identity). El navegador los usa como señal para volver
// a pedir la página a MVC: lo que se muestra siempre sale de SQLite.
public static class EventosRealtime
{
    public const string IncidenciaCreada = "incidencia.creada";
    public const string EstadoActualizado = "incidencia.estado_actualizado";
}

// Mensaje con la forma {event, data} que usan los SDK de PieSocket.
public record MensajeRealtime(
    [property: JsonPropertyName("event")] string Evento,
    [property: JsonPropertyName("data")] object Datos);

public record IncidenciaCreadaEvento(
    [property: JsonPropertyName("incidenciaId")] int IncidenciaId,
    [property: JsonPropertyName("titulo")] string Titulo,
    [property: JsonPropertyName("categoria")] string Categoria,
    [property: JsonPropertyName("estado")] string Estado,
    [property: JsonPropertyName("fecha")] string Fecha);

public record EstadoActualizadoEvento(
    [property: JsonPropertyName("incidenciaId")] int IncidenciaId,
    [property: JsonPropertyName("estadoAnterior")] string EstadoAnterior,
    [property: JsonPropertyName("estadoNuevo")] string EstadoNuevo,
    [property: JsonPropertyName("fecha")] string Fecha);
