using Microsoft.AspNetCore.Identity;
using proyectoGrupal.Constants;
using proyectoGrupal.Models;

namespace proyectoGrupal.Data;

// Datos iniciales de seguridad. Se ejecuta al iniciar la aplicación (ver Program.cs):
//   1. Crea los roles que falten (Administrador, Ciudadano).
//   2. Crea el administrador inicial con ADMIN_EMAIL y ADMIN_PASSWORD, si están configurados.
// Es seguro ejecutarlo en cada inicio: nunca crea duplicados.
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));

        await CrearRolesAsync(roleManager, logger);
        await CrearAdministradorAsync(userManager, configuration, logger);
    }

    private static async Task CrearRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        foreach (var rol in RoleNames.Todos)
        {
            if (await roleManager.RoleExistsAsync(rol))
            {
                continue;
            }

            var resultado = await roleManager.CreateAsync(new IdentityRole(rol));
            if (!resultado.Succeeded)
            {
                throw new InvalidOperationException($"No se pudo crear el rol \"{rol}\": {Errores(resultado)}");
            }

            logger.LogInformation("Rol \"{Rol}\" creado.", rol);
        }
    }

    // Las credenciales se leen de variables de entorno o User Secrets, nunca del código ni de appsettings.json.
    private static async Task CrearAdministradorAsync(
        UserManager<ApplicationUser> userManager, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["ADMIN_EMAIL"]?.Trim();
        var password = configuration["ADMIN_PASSWORD"];

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            logger.LogWarning(
                "Administrador inicial no configurado: faltan ADMIN_EMAIL y/o ADMIN_PASSWORD. " +
                "Nadie podrá entrar a /Admin hasta configurarlas (ver README, sección \"Configuración del administrador inicial\").");
            return;
        }

        var admin = await userManager.FindByEmailAsync(email);

        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                // Lo configura quien administra el servidor, no hace falta verificar el correo.
                EmailConfirmed = true
            };

            // Identity valida la contraseña con las mismas reglas del registro y guarda solo su hash.
            var creado = await userManager.CreateAsync(admin, password);
            if (!creado.Succeeded)
            {
                logger.LogError(
                    "No se creó el administrador inicial {Email}: {Errores}. Revisa ADMIN_EMAIL y ADMIN_PASSWORD.",
                    email, Errores(creado));
                return;
            }

            logger.LogInformation("Administrador inicial {Email} creado.", email);
        }

        // Si la cuenta ya existía (por ejemplo, registrada como ciudadano), solo se asegura el rol.
        // La contraseña de una cuenta existente nunca se cambia desde aquí.
        if (!await userManager.IsInRoleAsync(admin, RoleNames.Administrador))
        {
            var asignado = await userManager.AddToRoleAsync(admin, RoleNames.Administrador);
            if (!asignado.Succeeded)
            {
                logger.LogError("No se pudo asignar el rol {Rol} a {Email}: {Errores}",
                    RoleNames.Administrador, email, Errores(asignado));
                return;
            }

            logger.LogInformation("Rol {Rol} asignado a {Email}.", RoleNames.Administrador, email);
        }
    }

    private static string Errores(IdentityResult resultado) =>
        string.Join(" ", resultado.Errors.Select(e => e.Description));
}
