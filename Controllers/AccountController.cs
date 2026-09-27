using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using proyectoGrupal.Constants;
using proyectoGrupal.Models;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Controllers;

// Registro, inicio y cierre de sesión con ASP.NET Core Identity.
// Identity se encarga del hash de las contraseñas y de la cookie de sesión.
[AllowAnonymous]
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    // GET: /Account/AccessDenied
    // Identity redirige aquí cuando un usuario con sesión no tiene el rol necesario.
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
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
