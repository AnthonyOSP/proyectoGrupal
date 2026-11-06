using Microsoft.AspNetCore.Identity;
using proyectoGrupal.Constants;
using proyectoGrupal.Models;

namespace proyectoGrupal.Data;

// Datos iniciales de seguridad. Se ejecuta al iniciar la aplicación (ver Program.cs):
//   1. Crea los roles que falten (Administrador, Ciudadano).
//   2. Crea el administrador inicial con ADMIN_EMAIL y ADMIN_PASSWORD, si están configurados.
//   3. Crea un segundo administrador (opcional, para pruebas) con ADMIN2_EMAIL y ADMIN2_PASSWORD.
//   4. Crea el ciudadano de prueba con USER_EMAIL y USER_PASSWORD, si están configurados.
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
        await CrearAdministradorAsync(userManager, configuration, logger, "ADMIN_EMAIL", "ADMIN_PASSWORD", opcional: false);
        await CrearAdministradorAsync(userManager, configuration, logger, "ADMIN2_EMAIL", "ADMIN2_PASSWORD", opcional: true);
        await CrearUsuarioPruebaAsync(userManager, configuration, logger);
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
    // Se usa para el administrador inicial (ADMIN_*) y para el segundo administrador opcional (ADMIN2_*).
    private static async Task CrearAdministradorAsync(
        UserManager<ApplicationUser> userManager, IConfiguration configuration, ILogger logger,
        string claveEmail, string clavePassword, bool opcional)
    {
        var email = configuration[claveEmail]?.Trim();
        var password = configuration[clavePassword];

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            if (opcional)
            {
                logger.LogInformation("Administrador opcional no configurado ({Email} / {Password}): no se crea.", claveEmail, clavePassword);
            }
            else
            {
                logger.LogWarning(
                    "Administrador inicial no configurado: faltan {Email} y/o {Password}. " +
                    "Nadie podrá entrar a /Admin hasta configurarlas (ver README, sección \"Configuración del administrador inicial\").",
                    claveEmail, clavePassword);
            }
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
                    "No se creó el administrador {Email}: {Errores}. Revisa {ClaveEmail} y {ClavePassword}.",
                    email, Errores(creado), claveEmail, clavePassword);
                return;
            }

            logger.LogInformation("Administrador {Email} creado.", email);
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

    // Cuenta de ciudadano para pruebas y demostraciones (en Render la base se borra en cada reinicio).
    // Igual que el registro normal: correo, contraseña y rol Ciudadano. Las credenciales se leen igual que
    // las del administrador (variables de entorno o User Secrets). Sin ellas no se crea y la app funciona igual.
    private static async Task CrearUsuarioPruebaAsync(
        UserManager<ApplicationUser> userManager, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["USER_EMAIL"]?.Trim();
        var password = configuration["USER_PASSWORD"];

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            logger.LogInformation("Usuario de prueba no configurado (USER_EMAIL / USER_PASSWORD): no se crea.");
            return;
        }

        // Si ya existe (por ejemplo, de un inicio anterior) no se toca: ni su contraseña ni sus roles.
        if (await userManager.FindByEmailAsync(email) != null)
        {
            return;
        }

        var usuario = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var creado = await userManager.CreateAsync(usuario, password);
        if (!creado.Succeeded)
        {
            logger.LogError("No se creó el usuario de prueba {Email}: {Errores}. Revisa USER_EMAIL y USER_PASSWORD.",
                email, Errores(creado));
            return;
        }

        var asignado = await userManager.AddToRoleAsync(usuario, RoleNames.Ciudadano);
        if (!asignado.Succeeded)
        {
            // Sin rol la cuenta quedaría a medias: se elimina y se vuelve a intentar en el próximo inicio.
            logger.LogError("No se pudo asignar el rol {Rol} a {Email}: {Errores}",
                RoleNames.Ciudadano, email, Errores(asignado));
            await userManager.DeleteAsync(usuario);
            return;
        }

        logger.LogInformation("Usuario de prueba {Email} creado.", email);
    }

    private static string Errores(IdentityResult resultado) =>
        string.Join(" ", resultado.Errors.Select(e => e.Description));
}
