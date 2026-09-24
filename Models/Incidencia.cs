using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.Models;

// Entidad que se guarda en la tabla "Incidencias" de SQLite.
public class Incidencia
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(100, ErrorMessage = "El título no puede superar los 100 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(1000, ErrorMessage = "La descripción no puede superar los 1000 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    // Uno de los valores de CategoriasIncidencia.Todas
    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [StringLength(50)]
    public string Categoria { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ubicación es obligatoria.")]
    [StringLength(250, ErrorMessage = "La ubicación no puede superar los 250 caracteres.")]
    public string Ubicacion { get; set; } = string.Empty;

    // Opcional: no todas las incidencias tienen foto.
    [StringLength(500)]
    public string? FotoUrl { get; set; }

    // Uno de los valores de EstadosIncidencia.Todos
    [Required(ErrorMessage = "El estado es obligatorio.")]
    [StringLength(20)]
    public string Estado { get; set; } = EstadosIncidencia.Pendiente;

    public DateTime FechaRegistro { get; set; } = DateTime.Now;
}
