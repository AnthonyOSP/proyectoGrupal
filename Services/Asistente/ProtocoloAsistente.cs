using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace proyectoGrupal.Services.Asistente;

// Protocolo de la respuesta del chat (texto en streaming).
// El texto del asistente viaja tal cual; al final puede ir un mensaje de control en JSON entre dos
// caracteres STX (\u0002) con las fuentes consultadas y las tarjetas (incidencias o borrador de reporte).
// asistente.js separa el control del texto y dibuja las tarjetas.
public static class ProtocoloAsistente
{
    public const char Delimitador = '\u0002';

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Envolver(MensajeControlAsistente mensaje) =>
        $"{Delimitador}{JsonSerializer.Serialize(mensaje, Json)}{Delimitador}";

    // Quita los mensajes de control (para guardar en el historial solo lo que dijo el modelo).
    public static string Limpiar(string texto)
    {
        if (texto.IndexOf(Delimitador) < 0)
        {
            return texto;
        }

        var limpio = new StringBuilder(texto.Length);
        var dentro = false;
        foreach (var c in texto)
        {
            if (c == Delimitador)
            {
                dentro = !dentro;
            }
            else if (!dentro)
            {
                limpio.Append(c);
            }
        }
        return limpio.ToString();
    }
}

public sealed class MensajeControlAsistente
{
    // Documentos de ayuda consultados (para mostrar "Fuentes").
    public IReadOnlyList<string> Fuentes { get; init; } = [];

    public IReadOnlyList<TarjetaIncidencia> Incidencias { get; init; } = [];

    public BorradorReporte? Borrador { get; init; }
}

// Incidencia pública que el chat muestra como tarjeta (los mismos datos del listado público).
public sealed class TarjetaIncidencia
{
    public int Id { get; init; }

    public string Titulo { get; init; } = string.Empty;

    public string Categoria { get; init; } = string.Empty;

    public string Estado { get; init; } = string.Empty;

    // Clase CSS del estado (estado--pendiente, estado--revision o estado--atendida).
    public string EstadoClase { get; init; } = string.Empty;

    public string Ubicacion { get; init; } = string.Empty;

    public string Fecha { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;
}

// Reporte preparado en la conversación. El chat muestra un botón que abre el formulario ya completado:
// el asistente nunca envía el reporte, el vecino lo revisa y lo envía desde el formulario.
public sealed class BorradorReporte
{
    public string Titulo { get; init; } = string.Empty;

    public string Descripcion { get; init; } = string.Empty;

    public string Ubicacion { get; init; } = string.Empty;

    public string? Categoria { get; init; }

    public string Url { get; init; } = string.Empty;

    public bool RequiereSesion { get; init; }
}
