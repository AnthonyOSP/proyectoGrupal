using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Data;
using proyectoGrupal.Helpers;
using proyectoGrupal.Models;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Controllers;

// Panel administrativo: revisar incidencias y cambiar su estado.
// ETAPA 4: todavía no requiere inicio de sesión. En la Etapa 5 se protegerá con [Authorize].
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ApplicationDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: /Admin
    public async Task<IActionResult> Index()
    {
        var ultimas = await _context.Incidencias
            .AsNoTracking()
            .OrderByDescending(i => i.FechaRegistro)
            .Take(5)
            .ToListAsync();

        var modelo = new AdminDashboardViewModel
        {
            Total = await _context.Incidencias.CountAsync(),
            Pendientes = await _context.Incidencias.CountAsync(i => i.Estado == EstadosIncidencia.Pendiente),
            EnRevision = await _context.Incidencias.CountAsync(i => i.Estado == EstadosIncidencia.EnRevision),
            Atendidas = await _context.Incidencias.CountAsync(i => i.Estado == EstadosIncidencia.Atendida),
            UltimasIncidencias = ultimas.Select(IncidenciaViewModel.DesdeEntidad).ToList()
        };

        return View(modelo);
    }

    // GET: /Admin/Incidencias?buscar=poste&categoria=alumbrado&estado=Pendiente
    public async Task<IActionResult> Incidencias(string? buscar, string? categoria, EstadoIncidencia? estado)
    {
        var categoriaElegida = CatalogoCategorias.PorSlug(categoria);

        var incidencias = await _context.Incidencias
            .AsNoTracking()
            .Filtrar(buscar, categoriaElegida, estado)
            .OrderByDescending(i => i.FechaRegistro)
            .ToListAsync();

        var modelo = new IncidenciasListadoViewModel
        {
            Incidencias = incidencias.Select(IncidenciaViewModel.DesdeEntidad).ToList(),
            Categorias = CatalogoCategorias.Todas,
            Buscar = buscar,
            Categoria = categoriaElegida?.Slug,
            Estado = estado,
            Total = await _context.Incidencias.CountAsync()
        };

        return View(modelo);
    }

    // GET: /Admin/Detalle/5
    public async Task<IActionResult> Detalle(int id)
    {
        var incidencia = await _context.Incidencias
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (incidencia == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NoEncontrada");
        }

        return View(IncidenciaViewModel.DesdeEntidad(incidencia));
    }

    // POST: /Admin/CambiarEstado/5
    // Solo recibe el id y el nuevo estado: ningún otro campo de la incidencia puede cambiarse aquí.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, string? estado)
    {
        var incidencia = await _context.Incidencias.FirstOrDefaultAsync(i => i.Id == id);

        if (incidencia == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NoEncontrada");
        }

        // El estado debe ser uno de los permitidos (no se confía en el <select> del HTML).
        if (estado == null || !EstadosIncidencia.Todos.Contains(estado))
        {
            _logger.LogWarning("Estado no permitido \"{Estado}\" para la incidencia {Id}", estado, id);
            ModelState.AddModelError(nameof(estado), "El estado seleccionado no es válido.");
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(nameof(Detalle), IncidenciaViewModel.DesdeEntidad(incidencia));
        }

        if (incidencia.Estado == estado)
        {
            TempData["MensajeAdmin"] = $"La incidencia ya estaba en estado \"{estado}\". No se realizaron cambios.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var estadoAnterior = incidencia.Estado;
        incidencia.Estado = estado;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error al cambiar el estado de la incidencia {Id}", id);
            incidencia.Estado = estadoAnterior;
            ModelState.AddModelError(nameof(estado), "No pudimos actualizar el estado. Inténtalo nuevamente.");
            return View(nameof(Detalle), IncidenciaViewModel.DesdeEntidad(incidencia));
        }

        _logger.LogInformation("Incidencia {Id}: estado cambiado de \"{Anterior}\" a \"{Nuevo}\"", id, estadoAnterior, estado);
        TempData["MensajeAdmin"] = "Estado actualizado correctamente.";
        return RedirectToAction(nameof(Detalle), new { id });
    }
}
