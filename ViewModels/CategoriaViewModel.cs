namespace proyectoGrupal.ViewModels;

// Categoría de incidencia que se muestra en la interfaz.
public class CategoriaViewModel
{
    // Identificador corto usado en la URL de los filtros. Ej: "alumbrado".
    public string Slug { get; set; } = "";

    public string Nombre { get; set; } = "";

    // Nombre corto para filtros y etiquetas pequeñas.
    public string NombreCorto { get; set; } = "";

    public string Descripcion { get; set; } = "";

    // Nombre del icono SVG (ver Helpers/Iconos.cs).
    public string Icono { get; set; } = "";
}
