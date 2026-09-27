using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using proyectoGrupal.Constants;
using proyectoGrupal.Models;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Controllers;

// Registro, inicio y cierre de sesión con ASP.NET Core Identity, y el perfil del usuario.
// Identity se encarga del hash de las contraseñas y de la cookie de sesión.
// [AllowAnonymous] va en cada acción pública y no en la clase: en la clase anularía el [Authorize] del perfil.
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    // GET: /Account/Register
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirigirLocal(returnUrl);
        }

        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    // POST: /Account/Register
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var email = modelo.Email!.Trim();

        // El correo se usa también como nombre de usuario.
        var usuario = new ApplicationUser { UserName = email, Email = email };

        // Identity valida la contraseña y guarda solo su hash.
        var creado = await _userManager.CreateAsync(usuario, modelo.Password!);
        if (!creado.Succeeded)
        {
            // Distinct: un correo repetido genera dos errores con el mismo mensaje (usuario y correo).
            foreach (var error in creado.Errors.Select(e => e.Description).Distinct())
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(modelo);
        }

        // El rol lo decide siempre el servidor: toda cuenta nueva es Ciudadano.
        var rolAsignado = await _userManager.AddToRoleAsync(usuario, RoleNames.Ciudadano);
        if (!rolAsignado.Succeeded)
        {
            _logger.LogError("No se pudo asignar el rol {Rol} a {Email}: {Errores}",
                RoleNames.Ciudadano, email, string.Join(" ", rolAsignado.Errors.Select(e => e.Description)));

            // Sin rol la cuenta quedaría a medias: se elimina para que pueda intentarlo otra vez.
            await _userManager.DeleteAsync(usuario);
            ModelState.AddModelError(string.Empty, "No pudimos crear tu cuenta. Inténtalo nuevamente.");
            return View(modelo);
        }

        _logger.LogInformation("Nueva cuenta de ciudadano: {Email}", email);

        await _signInManager.SignInAsync(usuario, isPersistent: false);
        return RedirigirLocal(modelo.ReturnUrl);
    }

    // GET: /Account/Login?returnUrl=/Home/Reportar
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirigirLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    // POST: /Account/Login
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // UserName = Email, así que se inicia sesión con el correo.
        // lockoutOnFailure: tras 5 intentos fallidos la cuenta se bloquea unos minutos.
        var resultado = await _signInManager.PasswordSignInAsync(
            modelo.Email!.Trim(), modelo.Password!, modelo.RememberMe, lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            return RedirigirLocal(modelo.ReturnUrl);
        }

        if (resultado.IsLockedOut)
        {
            _logger.LogWarning("Cuenta bloqueada temporalmente por intentos fallidos: {Email}", modelo.Email);
            ModelState.AddModelError(string.Empty,
                "Demasiados intentos fallidos. Espera unos minutos antes de volver a intentarlo.");
        }
        else
        {
            // Mismo mensaje si no existe el correo o si la contraseña es incorrecta,
            // para no revelar qué correos tienen cuenta.
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
        }

        return View(modelo);
    }

    // POST: /Account/Logout
    // Solo por POST con token antiforgery: un enlace externo no puede cerrar la sesión de nadie.
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    // GET: /Account/AccessDenied
    // Identity redirige aquí cuando un usuario con sesión no tiene el rol necesario.
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    // GET: /Account/Profile
    // Siempre el perfil del usuario de la sesión: la acción no recibe ningún Id.
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
        {
            return await CerrarSesionYVolverAlLoginAsync();
        }

        return View(CrearPagina(usuario));
    }

    // POST: /Account/Profile
    // Solo recibe Nombres, Apellidos y Telefono (prefijo "Perfil."). El usuario sale de Identity,
    // así que un "UserId" o "Email" agregado al formulario se ignora.
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile([Bind(Prefix = "Perfil")] PerfilViewModel perfil)
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
        {
            return await CerrarSesionYVolverAlLoginAsync();
        }

        if (!ModelState.IsValid)
        {
            return View(CrearPagina(usuario, perfil));
        }

        usuario.Nombres = Normalizar(perfil.Nombres);
        usuario.Apellidos = Normalizar(perfil.Apellidos);

        // El teléfono se guarda en PhoneNumber (columna que Identity ya tiene), sin duplicarlo.
        var telefono = Normalizar(perfil.Telefono);
        if (usuario.PhoneNumber != telefono)
        {
            usuario.PhoneNumber = telefono;
            usuario.PhoneNumberConfirmed = false;
        }

        var resultado = await _userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            _logger.LogError("No se pudo actualizar el perfil de {Email}: {Errores}",
                usuario.Email, string.Join(" ", resultado.Errors.Select(e => e.Description)));
            ModelState.AddModelError("Perfil", "No pudimos guardar tus datos. Inténtalo nuevamente.");
            return View(CrearPagina(usuario, perfil));
        }

        TempData["MensajePerfil"] = "Tus datos se guardaron correctamente.";
        return RedirectToAction(nameof(Profile));
    }

    // POST: /Account/ChangePassword
    // Usa UserManager.ChangePasswordAsync: Identity comprueba la contraseña actual, aplica las reglas
    // de contraseña y guarda solo el nuevo hash. Las contraseñas nunca se registran en el log.
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Contrasena")] CambiarContrasenaViewModel modelo)
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
        {
            return await CerrarSesionYVolverAlLoginAsync();
        }

        if (ModelState.IsValid && modelo.NuevaContrasena == modelo.ContrasenaActual)
        {
            ModelState.AddModelError("Contrasena.NuevaContrasena", "La nueva contraseña debe ser diferente de la actual.");
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Profile), CrearPagina(usuario));
        }

        var resultado = await _userManager.ChangePasswordAsync(usuario, modelo.ContrasenaActual!, modelo.NuevaContrasena!);
        if (!resultado.Succeeded)
        {
            // Contraseña actual incorrecta → en su campo; reglas de la nueva contraseña → en el campo nuevo.
            foreach (var error in resultado.Errors)
            {
                var campo = error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)
                    ? "Contrasena.ContrasenaActual"
                    : "Contrasena.NuevaContrasena";
                ModelState.AddModelError(campo, error.Description);
            }

            _logger.LogWarning("Intento fallido de cambio de contraseña para {Email}", usuario.Email);
            return View(nameof(Profile), CrearPagina(usuario));
        }

        // ChangePasswordAsync renueva el "security stamp" (así se invalidan las sesiones de otros dispositivos).
        // Se renueva también la cookie de esta sesión para que el usuario no tenga que volver a entrar.
        await _signInManager.RefreshSignInAsync(usuario);

        _logger.LogInformation("Contraseña cambiada para {Email}", usuario.Email);
        TempData["MensajeContrasena"] = "Tu contraseña se cambió correctamente.";
        return RedirectToAction(nameof(Profile), "Account", null, "seguridad");
    }

    // Datos de la página "Mi perfil". Si se pasa "perfil", se muestran los valores enviados (con errores).
    private PerfilPaginaViewModel CrearPagina(ApplicationUser usuario, PerfilViewModel? perfil = null) => new()
    {
        Email = usuario.Email ?? "",
        EsAdministrador = User.IsInRole(RoleNames.Administrador),
        Perfil = perfil ?? new PerfilViewModel
        {
            Nombres = usuario.Nombres,
            Apellidos = usuario.Apellidos,
            Telefono = usuario.PhoneNumber
        }
    };

    // Quita espacios sobrantes ("  Ana   María " → "Ana María"). Un texto vacío se guarda como null.
    private static string? Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        return string.Join(' ', texto.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    // La cookie es válida pero la cuenta ya no existe (por ejemplo, se reinició la base de datos).
    private async Task<IActionResult> CerrarSesionYVolverAlLoginAsync()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login), new { returnUrl = Url.Action(nameof(Profile)) });
    }

    // Solo redirige a direcciones de este mismo sitio (evita open redirect);
    // cualquier otra cosa lleva al inicio.
    private IActionResult RedirigirLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }
}
