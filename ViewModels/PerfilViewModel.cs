using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.ViewModels;

// Datos que el usuario puede editar en "Mi perfil".
// No incluye Id ni correo: el usuario que se modifica sale siempre de la sesión (Identity).
public class PerfilViewModel
{
    // Letras (con tildes y ñ), espacios, apóstrofo y guion. Sin \p{L} para que funcione igual en el navegador.
    private const string SoloNombre = @"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ' -]+$";

    [Display(Name = "Nombres")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "Los nombres deben tener entre 2 y 60 caracteres.")]
    [RegularExpression(SoloNombre, ErrorMessage = "Los nombres solo pueden contener letras, espacios, apóstrofos y guiones.")]
    public string? Nombres { get; set; }

    [Display(Name = "Apellidos")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "Los apellidos deben tener entre 2 y 60 caracteres.")]
    [RegularExpression(SoloNombre, ErrorMessage = "Los apellidos solo pueden contener letras, espacios, apóstrofos y guiones.")]
    public string? Apellidos { get; set; }

    // Opcional. Ej: "987 654 321", "+51 987654321", "01-4567890".
    [Display(Name = "Teléfono")]
    [StringLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
    [RegularExpression(@"^\s*\+?[0-9][0-9 -]{5,18}[0-9]\s*$",ErrorMessage = "Escribe un teléfono válido: solo números, espacios, guiones y un + inicial.")]
    public string? Telefono { get; set; }
}
