using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Data;
using proyectoGrupal.Helpers;
using proyectoGrupal.Models;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HomeController> _logger;

    // ASP.NET entrega el DbContext y el logger por inyección de dependencias (ver Program.cs).
    public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: /
    public async Task<IActionResult> Index()
    {
        var recientes = await _context.Incidencias
            .AsNoTracking()
            .OrderByDescending(i => i.FechaRegistro)
            .Take(3)
            .ToListAsync();

        var modelo = new InicioViewModel
        {
            Categorias = CatalogoCategorias.Todas,
            Recientes = recientes.Select(IncidenciaViewModel.DesdeEntidad).ToList(),
            TotalReportes = await _context.Incidencias.CountAsync(),
            TotalEnRevision = await _context.Incidencias.CountAsync(i => i.Estado == EstadosIncidencia.EnRevision),
            TotalAtendidas = await _context.Incidencias.CountAsync(i => i.Estado == EstadosIncidencia.Atendida)
        };

        return View(modelo);
    }

    // GET: /Home/Reportar?categoria=alumbrado
    public IActionResult Reportar(string? categoria)
    {
        // El parámetro "categoria" (slug de la URL) queda en ModelState y el <select> lo usaría
        // en lugar del modelo. Se limpia para que se aplique el nombre completo de abajo.
        ModelState.Clear();

        // Permite llegar desde una tarjeta de categoría con la categoría ya elegida.
        var modelo = new CrearIncidenciaViewModel
        {
            Categoria = CatalogoCategorias.PorSlug(categoria)?.Nombre
        };

        return View(modelo);
    }

    // POST: /Home/Reportar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reportar(CrearIncidenciaViewModel modelo)
    {
        // La categoría debe ser una de la lista (no se confía en el HTML del navegador).
        if (!CategoriasIncidencia.Todas.Contains(modelo.Categoria))
        {
            ModelState.AddModelError(nameof(modelo.Categoria), "Elige una categoría de la lista.");
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // Id, Estado y FechaRegistro los decide el servidor, nunca el formulario.
        var incidencia = new Incidencia
        {
            Titulo = modelo.Titulo!.Trim(),
            Descripcion = modelo.Descripcion!.Trim(),
            Categoria = modelo.Categoria!,
            Ubicacion = modelo.Ubicacion!.Trim(),
            FotoUrl = string.IsNullOrWhiteSpace(modelo.FotoUrl) ? null : modelo.FotoUrl.Trim(),
            Estado = EstadosIncidencia.Pendiente,
            FechaRegistro = DateTime.Now
        };

        try
        {
            _context.Incidencias.Add(incidencia);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // El detalle técnico queda en el log; el vecino solo ve un mensaje amigable.
            _logger.LogError(ex, "Error al guardar la incidencia \"{Titulo}\"", incidencia.Titulo);
            ModelState.AddModelError(string.Empty, "No pudimos registrar tu incidencia. Inténtalo nuevamente.");
            return View(modelo);
        }

        // TempData sobrevive a la redirección y se muestra una sola vez.
        TempData["ReporteCreadoId"] = incidencia.Id;
        return RedirectToAction(nameof(Incidencias));
    }

    // GET: /Home/Incidencias?buscar=poste&categoria=alumbrado&estado=EnRevision
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

    // GET: /Home/Detalle/5
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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
