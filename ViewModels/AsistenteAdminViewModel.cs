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
}
