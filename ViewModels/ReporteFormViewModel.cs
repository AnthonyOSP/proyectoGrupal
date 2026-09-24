using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.ViewModels;

// Datos del formulario "Reportar incidencia".
// Las validaciones funcionan tanto en el navegador como en el servidor.
public class ReporteFormViewModel
{
    [Display(Name = "Título de la incidencia")]
    [Required(ErrorMessage = "Escribe un título para tu reporte.")]
    [StringLength(80, MinimumLength = 5, ErrorMessage = "El título debe tener entre 5 y 80 caracteres.")]
    public string? Titulo { get; set; }

    [Display(Name = "Categoría")]
    [Required(ErrorMessage = "Elige la categoría que mejor describe el problema.")]
    public string? Categoria { get; set; }

    [Display(Name = "Descripción")]
    [Required(ErrorMessage = "Cuéntanos qué ocurre.")]
    [StringLength(500, MinimumLength = 15, ErrorMessage = "La descripción debe tener entre 15 y 500 caracteres.")]
    public string? Descripcion { get; set; }

    [Display(Name = "Ubicación")]
    [Required(ErrorMessage = "Indica dónde está el problema.")]
    [StringLength(120, ErrorMessage = "La ubicación no puede superar los 120 caracteres.")]
    public string? Ubicacion { get; set; }

    // La foto todavía no se guarda (Etapa 2).
    [Display(Name = "Fotografía")]
    public IFormFile? Foto { get; set; }
}
