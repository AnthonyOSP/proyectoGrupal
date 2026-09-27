using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.ViewModels;

// Formulario "Cambiar contraseña" del perfil. Las contraseñas nunca se vuelven a mostrar en el HTML.
public class CambiarContrasenaViewModel
{
    [Display(Name = "Contraseña actual")]
    [Required(ErrorMessage = "Escribe tu contraseña actual.")]
    [DataType(DataType.Password)]
    public string? ContrasenaActual { get; set; }

    // Mismas reglas que el registro e Identity (Program.cs): mínimo 6 caracteres, una letra y un número.
    [Display(Name = "Nueva contraseña")]
    [Required(ErrorMessage = "Escribe la nueva contraseña.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres.")]
    [RegularExpression(@"^(?=.*[A-Za-zÁÉÍÓÚÜÑáéíóúüñ])(?=.*\d).+$", ErrorMessage = "La contraseña debe incluir al menos una letra y un número.")]
    [DataType(DataType.Password)]
    public string? NuevaContrasena { get; set; }

    [Display(Name = "Confirmar nueva contraseña")]
    [Required(ErrorMessage = "Vuelve a escribir la nueva contraseña.")]
    [Compare(nameof(NuevaContrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    public string? ConfirmarContrasena { get; set; }
}
