using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.ViewModels;

// Datos que el visitante envía desde el formulario "Contáctanos".
// No incluye Id ni FechaRegistro: esos valores los asigna el servidor.
// El controlador normaliza los textos (espacios sobrantes) ANTES de validar.
public class ContactoViewModel
{
    // Límites en un solo lugar: los usan las validaciones, la vista y el contador de caracteres.
    // Los máximos coinciden con las columnas de la tabla MensajesContacto.
    public const int NombreMinimo = 2;
    public const int NombreMaximo = 100;
    public const int EmailMaximo = 256;
    public const int MensajeMinimo = 10;
    public const int MensajeMaximo = 1000;

    [Display(Name = "Nombre")]
    [Required(ErrorMessage = "Escribe tu nombre.")]
    [StringLength(NombreMaximo, MinimumLength = NombreMinimo, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
    public string? Nombre { get; set; }

    [Display(Name = "Correo electrónico")]
    [Required(ErrorMessage = "Escribe tu correo electrónico.")]
    [EmailAddress(ErrorMessage = "Escribe un correo electrónico válido.")]
    [StringLength(EmailMaximo, ErrorMessage = "El correo no puede superar los 256 caracteres.")]
    public string? Email { get; set; }

    [Display(Name = "Mensaje")]
    [Required(ErrorMessage = "Escribe tu mensaje.")]
    [StringLength(MensajeMaximo, MinimumLength = MensajeMinimo, ErrorMessage = "El mensaje debe tener entre 10 y 1000 caracteres.")]
    public string? Mensaje { get; set; }
}
