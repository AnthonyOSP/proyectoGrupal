using System.Text.Json.Serialization;
using proyectoGrupal.Models;

namespace proyectoGrupal.Services.Algolia;

// Registro que se envía a Algolia. Se arma a mano (no se indexa la entidad de EF):
// solo datos que ya son públicos en /Home/Incidencias. No incluye UsuarioId, correo,
// historial ni ningún dato del ciudadano.
public class IncidenciaAlgoliaDocumento
{
    // Id de la incidencia en SQLite. Permite volver al registro real.
    [JsonPropertyName("objectID")]
    public string ObjectID { get; set; } = "";

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = "";

    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = "";

    [JsonPropertyName("categoria")]
    public string Categoria { get; set; } = "";

    [JsonPropertyName("ubicacion")]
    public string Ubicacion { get; set; } = "";

    [JsonPropertyName("estado")]
    public string Estado { get; set; } = "";

    // Fecha legible (ISO 8601) y la misma fecha como número, para ordenar y filtrar por fecha.
    [JsonPropertyName("fechaRegistro")]
    public string FechaRegistro { get; set; } = "";

    [JsonPropertyName("fechaRegistroTimestamp")]
    public long FechaRegistroTimestamp { get; set; }

    public static IncidenciaAlgoliaDocumento DesdeEntidad(Incidencia incidencia) => new()
    {
        ObjectID = incidencia.Id.ToString(),
        Titulo = incidencia.Titulo,
        Descripcion = incidencia.Descripcion,
        Categoria = incidencia.Categoria,
        Ubicacion = incidencia.Ubicacion,
        Estado = incidencia.Estado,
        FechaRegistro = incidencia.FechaRegistro.ToString("yyyy-MM-ddTHH:mm:ss"),
        FechaRegistroTimestamp = new DateTimeOffset(incidencia.FechaRegistro).ToUnixTimeSeconds()
    };
}
