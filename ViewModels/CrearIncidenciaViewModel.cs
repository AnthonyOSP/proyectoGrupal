using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.ViewModels;

// Datos que el vecino envía desde el formulario "Reportar incidencia".
// No incluye Id, Estado ni FechaRegistro: esos valores los asigna el servidor.
// Las validaciones funcionan tanto en el navegador como en el servidor.
public class CrearIncidenciaViewModel
{
    [Display(Name = "Título de la incidencia")]
    [Required(ErrorMessage = "Escribe un título para tu reporte.")]
    [StringLength(100, MinimumLength = 5, ErrorMessage = "El título debe tener entre 5 y 100 caracteres.")]
    public string? Titulo { get; set; }

    // Nombre de la categoría, uno de CategoriasIncidencia.Todas
    [Display(Name = "Categoría")]
    [Required(ErrorMessage = "Elige la categoría que mejor describe el problema.")]
    public string? Categoria { get; set; }

    [Display(Name = "Descripción")]
    [Required(ErrorMessage = "Cuéntanos qué ocurre.")]
    [StringLength(1000, MinimumLength = 15, ErrorMessage = "La descripción debe tener entre 15 y 1000 caracteres.")]
    public string? Descripcion { get; set; }

    [Display(Name = "Ubicación")]
    [Required(ErrorMessage = "Indica dónde está el problema.")]
    [StringLength(250, ErrorMessage = "La ubicación no puede superar los 250 caracteres.")]
    public string? Ubicacion { get; set; }

    // Enlace a una foto. Queda preparado para cuando exista la subida real de imágenes.
    [Display(Name = "Fotografía")]
    [Url(ErrorMessage = "El enlace de la foto no es válido.")]
    [StringLength(500)]
    public string? FotoUrl { get; set; }
}
