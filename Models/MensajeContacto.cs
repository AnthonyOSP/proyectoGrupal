using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.Models;

// Un mensaje enviado desde la página "Contáctanos" (tabla "MensajesContacto").
// Cualquier visitante puede escribir, tenga o no una cuenta.
public class MensajeContacto
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Mensaje { get; set; } = string.Empty;

    // Hora del servidor (DateTime.Now), nunca del formulario. No se muestra al visitante.
    public DateTime FechaRegistro { get; set; }
}
