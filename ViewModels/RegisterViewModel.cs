using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.ViewModels;

// Datos del formulario "Crear cuenta".
// No tiene campo de rol: el rol Ciudadano lo asigna siempre el servidor.
public class RegisterViewModel
{
    [Display(Name = "Correo electrónico")]
    [Required(ErrorMessage = "Escribe tu correo electrónico.")]
    [EmailAddress(ErrorMessage = "Escribe un correo electrónico válido.")]
    [StringLength(256, ErrorMessage = "El correo no puede superar los 256 caracteres.")]
    public string? Email { get; set; }

    // Mismas reglas que Identity (Program.cs): mínimo 6 caracteres, una letra y un número.
    [Display(Name = "Contraseña")]
    [Required(ErrorMessage = "Escribe una contraseña.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres.")]
    // Sin \p{L}: la validación del navegador (JavaScript) no lo interpreta igual que .NET.
    [RegularExpression(@"^(?=.*[A-Za-zÁÉÍÓÚÜÑáéíóúüñ])(?=.*\d).+$",ErrorMessage = "La contraseña debe incluir al menos una letra y un número.")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Display(Name = "Confirmar contraseña")]
    [Required(ErrorMessage = "Vuelve a escribir la contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    public string? ConfirmPassword { get; set; }

    // Página a la que volver después de crear la cuenta (se valida en el controlador).
    public string? ReturnUrl { get; set; }
}
