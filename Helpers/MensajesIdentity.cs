using Microsoft.AspNetCore.Identity;

namespace proyectoGrupal.Helpers;

// Traduce al español los mensajes de error de Identity que puede ver un vecino al registrarse.
// Se registra en Program.cs con AddErrorDescriber<MensajesIdentity>().
public class MensajesIdentity : IdentityErrorDescriber
{
    // El nombre de usuario es el correo, así que ambos duplicados muestran el mismo mensaje.
    private const string CorreoEnUso = "Ya existe una cuenta con este correo electrónico.";

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = CorreoEnUso };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = CorreoEnUso };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "El correo electrónico no es válido." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "El correo electrónico contiene caracteres no permitidos." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"La contraseña debe tener al menos {length} caracteres." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "La contraseña actual no es correcta." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "La contraseña debe incluir al menos un número." };

    public override IdentityError DuplicateRoleName(string role) =>
        new() { Code = nameof(DuplicateRoleName), Description = $"El rol \"{role}\" ya existe." };

    public override IdentityError UserAlreadyInRole(string role) =>
        new() { Code = nameof(UserAlreadyInRole), Description = $"El usuario ya tiene el rol \"{role}\"." };
}
