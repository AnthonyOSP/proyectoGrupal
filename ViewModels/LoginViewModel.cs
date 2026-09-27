using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.ViewModels;

// Datos del formulario "Iniciar sesión".
public class LoginViewModel
{
    [Display(Name = "Correo electrónico")]
    [Required(ErrorMessage = "Escribe tu correo electrónico.")]
    [EmailAddress(ErrorMessage = "Escribe un correo electrónico válido.")]
    public string? Email { get; set; }

    [Display(Name = "Contraseña")]
    [Required(ErrorMessage = "Escribe tu contraseña.")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Display(Name = "Recordarme")]
    public bool RememberMe { get; set; }

    // Página que el usuario quería abrir antes de que se le pidiera iniciar sesión.
    // Se valida en el controlador para evitar redirecciones a sitios externos (open redirect).
    public string? ReturnUrl { get; set; }
}
