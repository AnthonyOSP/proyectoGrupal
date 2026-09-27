using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.ViewModels;

// Datos que el vecino envía desde el formulario "Reportar incidencia".
// No incluye Id, Estado, FechaRegistro ni UsuarioId: esos valores los asigna el servidor.
// Las validaciones funcionan tanto en el navegador como en el servidor.
// El controlador normaliza los textos (espacios sobrantes) ANTES de validar, así las longitudes
// se comprueban sobre lo que realmente se guarda.
public class CrearIncidenciaViewModel
{
    // Límites en un solo lugar: los usan las validaciones, la vista y el contador de caracteres.
    // Los máximos coinciden con las columnas de la tabla Incidencias.
    public const int TituloMinimo = 5;
    public const int TituloMaximo = 100;
    public const int DescripcionMinimo = 15;
    public const int DescripcionMaximo = 1000;
    public const int UbicacionMinimo = 5;
    public const int UbicacionMaximo = 250;

    [Display(Name = "Título del problema")]
    [Required(ErrorMessage = "Escribe un título para tu reporte.")]
    [StringLength(TituloMaximo, MinimumLength = TituloMinimo, ErrorMessage = "El título debe tener entre 5 y 100 caracteres.")]
    public string? Titulo { get; set; }

    // Nombre de la categoría, uno de CategoriasIncidencia.Todas (se vuelve a comprobar en el controlador).
    [Display(Name = "Categoría")]
    [Required(ErrorMessage = "Elige la categoría que mejor describe el problema.")]
    public string? Categoria { get; set; }

    [Display(Name = "Descripción")]
    [Required(ErrorMessage = "Cuéntanos qué ocurre.")]
    [StringLength(DescripcionMaximo, MinimumLength = DescripcionMinimo, ErrorMessage = "La descripción debe tener entre 15 y 1000 caracteres.")]
    public string? Descripcion { get; set; }

    [Display(Name = "Ubicación")]
    [Required(ErrorMessage = "Indica dónde ocurre el problema.")]
    [StringLength(UbicacionMaximo, MinimumLength = UbicacionMinimo, ErrorMessage = "La ubicación debe tener entre 5 y 250 caracteres.")]
    public string? Ubicacion { get; set; }

    // Fotografía opcional. Se procesa en el servidor (rostros pixelados) antes de guardarse.
    // No hay campo FotoUrl: la ruta de la foto la genera el servidor, nunca el formulario.
    [Display(Name = "Fotografía de la incidencia")]
    public IFormFile? Foto { get; set; }
}
