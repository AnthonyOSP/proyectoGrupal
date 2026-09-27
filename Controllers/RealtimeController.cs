using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Constants;
using proyectoGrupal.Data;
using proyectoGrupal.Models;
using proyectoGrupal.Services.PieSocket;

namespace proyectoGrupal.Controllers;

// Autorización de canales privados de PieSocket (no es una API general: una sola acción).
// El navegador pide aquí un JWT antes de conectarse; PieSocket rechaza la conexión sin un JWT válido.
// Es el "authEndpoint" de la documentación oficial: recibe channel_name y responde { "auth": "JWT" }.
[Authorize]
public class RealtimeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPieSocketRealtimeService _realtime;
    private readonly ILogger<RealtimeController> _logger;

    public RealtimeController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPieSocketRealtimeService realtime,
        ILogger<RealtimeController> logger)
    {
        _context = context;
        _userManager = userManager;
        _realtime = realtime;
        _logger = logger;
    }

    // POST: /Realtime/Autorizar (channel_name en el formulario, con token antiforgery)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Autorizar([FromForm(Name = "channel_name")] string? canal)
    {
        if (!_realtime.EstaConfigurado)
        {
            return NotFound();
        }

        if (!await PuedeEscucharAsync(canal))
        {
            // 403 directo (Forbid() redirigiría a la página de acceso denegado).
            _logger.LogWarning("Canal de tiempo real denegado a {Usuario}: {Canal}", User.Identity?.Name, canal);
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        // Respuesta pequeña y privada: el JWT sirve solo para ese canal y por unos minutos.
        Response.Headers.CacheControl = "no-store";
        return Json(new { auth = _realtime.CrearJwt(canal!) });
    }

    // Reglas de acceso (se comprueban en el servidor, no solo ocultando el nombre del canal):
    //   - canal de administración: solo administradores;
    //   - canal de una incidencia: su dueño o un administrador (las mismas reglas que /Home/Seguimiento).
    private async Task<bool> PuedeEscucharAsync(string? canal)
    {
        var esAdministrador = User.IsInRole(RoleNames.Administrador);

        if (canal == CanalesRealtime.Administracion)
        {
            return esAdministrador;
        }

        if (!CanalesRealtime.EsCanalDeIncidencia(canal, out var incidenciaId))
        {
            return false;
        }

        if (esAdministrador)
        {
            return await _context.Incidencias.AnyAsync(i => i.Id == incidenciaId);
        }

        var usuarioId = _userManager.GetUserId(User);
        return !string.IsNullOrEmpty(usuarioId)
            && await _context.Incidencias.AnyAsync(i => i.Id == incidenciaId && i.UsuarioId == usuarioId);
    }
}
