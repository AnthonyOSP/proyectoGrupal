using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using proyectoGrupal.Mock;
using proyectoGrupal.Models;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Controllers;

public class HomeController : Controller
{
    // GET: /
    public IActionResult Index()
    {
        var incidencias = DatosDemo.Incidencias;

        var modelo = new InicioViewModel
        {
            Categorias = DatosDemo.Categorias,
            Recientes = incidencias.OrderByDescending(i => i.Fecha).Take(3).ToList(),
            TotalReportes = incidencias.Count,
            TotalEnRevision = incidencias.Count(i => i.Estado == EstadoIncidencia.EnRevision),
            TotalAtendidas = incidencias.Count(i => i.Estado == EstadoIncidencia.Atendida)
        };

        return View(modelo);
    }

    // GET: /Home/Reportar?categoria=alumbrado
    public IActionResult Reportar(string? categoria)
    {
        ViewBag.Categorias = DatosDemo.Categorias;

        // Permite llegar desde una tarjeta de categoría con la categoría ya elegida.
        var modelo = new ReporteFormViewModel
        {
            Categoria = DatosDemo.BuscarCategoria(categoria)?.Slug
        };

        return View(modelo);
    }

    // POST: /Home/Reportar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Reportar(ReporteFormViewModel modelo)
    {
        if (DatosDemo.BuscarCategoria(modelo.Categoria) == null)
        {
            ModelState.AddModelError(nameof(modelo.Categoria), "Elige una categoría de la lista.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categorias = DatosDemo.Categorias;
            return View(modelo);
        }

        // ETAPA 1: el reporte todavía NO se guarda.
        // En la Etapa 2 aquí se guardará en SQLite con Entity Framework.
        TempData["ReporteEnviado"] = modelo.Titulo;
        return RedirectToAction(nameof(Reportar));
    }

    // GET: /Home/Incidencias?buscar=poste&categoria=alumbrado&estado=EnRevision
    public IActionResult Incidencias(string? buscar, string? categoria, EstadoIncidencia? estado)
    {
        IEnumerable<IncidenciaViewModel> resultado = DatosDemo.Incidencias;

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim();
            resultado = resultado.Where(i =>
                i.Titulo.Contains(texto, StringComparison.CurrentCultureIgnoreCase) ||
                i.Descripcion.Contains(texto, StringComparison.CurrentCultureIgnoreCase) ||
                i.Ubicacion.Contains(texto, StringComparison.CurrentCultureIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            resultado = resultado.Where(i => i.Categoria.Slug == categoria);
        }

        if (estado != null)
        {
            resultado = resultado.Where(i => i.Estado == estado);
        }

        var modelo = new IncidenciasListadoViewModel
        {
            Incidencias = resultado.OrderByDescending(i => i.Fecha).ToList(),
            Categorias = DatosDemo.Categorias,
            Buscar = buscar,
            Categoria = categoria,
            Estado = estado,
            Total = DatosDemo.Incidencias.Count
        };

        return View(modelo);
    }

    // GET: /Home/Detalle/2
    public IActionResult Detalle(int id)
    {
        var incidencia = DatosDemo.Incidencias.FirstOrDefault(i => i.Id == id);

        if (incidencia == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NoEncontrada");
        }

        return View(incidencia);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
