using Microsoft.AspNetCore.Identity;

namespace proyectoGrupal.Helpers;

// Regla extra de contraseña: al menos una letra (Identity solo trae mayúscula/minúscula por separado).
// Se aplica a TODAS las cuentas, también al administrador inicial creado por el seeder.
public class ContrasenaConLetraValidator<TUser> : IPasswordValidator<TUser> where TUser : class
{
    public Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user, string? password)
    {
        if (password != null && password.Any(char.IsLetter))
        {
            return Task.FromResult(IdentityResult.Success);
        }

        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = "PasswordRequiresLetter",
            Description = "La contraseña debe incluir al menos una letra."
        }));
    }
}
