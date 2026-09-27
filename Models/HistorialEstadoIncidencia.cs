using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.Models;

// Un registro por cada cambio de estado de una incidencia (tabla "HistorialEstadosIncidencia").
// Solo lo crea el servidor: al registrar la incidencia y cuando un administrador cambia su estado.
// Ningún formulario envía estos datos.
public class HistorialEstadoIncidencia
{
    public int Id { get; set; }

    public int IncidenciaId { get; set; }

    public Incidencia? Incidencia { get; set; }

    // null en el primer registro: la incidencia se acaba de crear y no tenía estado anterior.
    // Valores de EstadosIncidencia.Todos
    [StringLength(20)]
    public string? EstadoAnterior { get; set; }

    [Required]
    [StringLength(20)]
    public string EstadoNuevo { get; set; } = string.Empty;

    // Hora del servidor (DateTime.Now), nunca del formulario.
    public DateTime FechaCambio { get; set; }

    // Quién hizo el cambio: el ciudadano en el registro inicial, el administrador en los demás.
    // Queda null solo si esa cuenta se elimina (el historial se conserva).
    public string? UsuarioId { get; set; }

    public ApplicationUser? Usuario { get; set; }
}
