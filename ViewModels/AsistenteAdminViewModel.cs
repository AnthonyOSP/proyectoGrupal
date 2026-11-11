using proyectoGrupal.Services.Asistente.Conocimiento;

namespace proyectoGrupal.ViewModels;

// Página del panel /Admin/Asistente: estado del asistente y de la base de conocimiento.
public class AsistenteAdminViewModel
{
    public bool AsistenteDisponible { get; set; }

    public string Modelo { get; set; } = string.Empty;

    public bool UsaQdrant { get; set; }

    public string Coleccion { get; set; } = string.Empty;

    public string ModeloEmbedding { get; set; } = string.Empty;

    // Archivos .md de la carpeta Conocimiento/ del proyecto.
    public IReadOnlyList<string> ArchivosCarpeta { get; set; } = [];

    // Documentos cargados en Qdrant (vacío si Qdrant no está configurado).
    public IReadOnlyList<DocumentoConocimiento> Documentos { get; set; } = [];

    public string? ErrorDocumentos { get; set; }

    // Prueba de búsqueda: lo que encontraría el asistente para esta consulta.
    public string? Consulta { get; set; }

    public BusquedaConocimiento? Busqueda { get; set; }

    // Índice semántico de incidencias en Qdrant (null si Qdrant no está configurado o no responde).
    public string ColeccionIncidencias { get; set; } = string.Empty;

    public long? IncidenciasIndexadas { get; set; }

    public int IncidenciasTotales { get; set; }

    // Valoraciones de los vecinos (👍/👎) de los últimos DiasValoraciones días.
    public const int DiasValoraciones = 30;

    public int ValoracionesUtiles { get; set; }

    public int ValoracionesNoUtiles { get; set; }

    // Respuestas que no sirvieron, de la más reciente a la más antigua: dicen qué falta en la ayuda.
    public IReadOnlyList<Models.ValoracionAsistente> NoUtiles { get; set; } = [];
}
