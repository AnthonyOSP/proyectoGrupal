using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Data;
using proyectoGrupal.Helpers;
using proyectoGrupal.Models;
using proyectoGrupal.Services;
using proyectoGrupal.Services.Algolia;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly FotoIncidenciaService _fotos;
    private readonly BusquedaIncidenciasService _busqueda;
    private readonly IAlgoliaIncidenciaService _algolia;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<HomeController> _logger;

    // ASP.NET entrega estas dependencias por inyección de dependencias (ver Program.cs).
    public HomeController(
        ApplicationDbContext context,
        FotoIncidenciaService fotos,
        BusquedaIncidenciasService busqueda,
        IAlgoliaIncidenciaService algolia,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<HomeController> logger)
    {
        _context = context;
        _fotos = fotos;
        _busqueda = busqueda;
        _algolia = algolia;
        _userManager = userManager;
        _signInManager = signInManager;
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
    // Requiere sesión iniciada: un visitante es enviado a /Account/Login y vuelve aquí al entrar.
    [Authorize]
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
    // El límite de 20 MB permite responder con un mensaje amigable a fotos de hasta 20 MB;
    // la foto en sí se valida con un máximo de 5 MB.
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
    public async Task<IActionResult> Reportar(CrearIncidenciaViewModel modelo)
    {
        // El dueño del reporte es el usuario de la sesión (cookie de Identity), nunca un dato del formulario.
        // Si la cuenta ya no existe (por ejemplo, se reinició la base de datos), se cierra la sesión.
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(AccountController.Login), "Account",
                new { returnUrl = Url.Action(nameof(Reportar)) });
        }

        // Normalizar los textos y volver a validar: así "   abcd   " no pasa como título de 5+ caracteres
        // y los saltos de línea cuentan igual que en el contador del navegador.
        modelo.Titulo = UnaLinea(modelo.Titulo);
        modelo.Ubicacion = UnaLinea(modelo.Ubicacion);
        modelo.Descripcion = TextoLargo(modelo.Descripcion);
        ModelState.Clear();
        TryValidateModel(modelo);

        // La categoría debe ser una de la lista (no se confía en el HTML del navegador).
        if (modelo.Categoria != null && !CategoriasIncidencia.Todas.Contains(modelo.Categoria))
        {
            ModelState.AddModelError(nameof(modelo.Categoria), "Elige una categoría de la lista.");
        }

        // Validación rápida de la foto (tamaño, extensión y tipo MIME) junto con los demás campos.
        // El contenido real de la imagen se comprueba después, en ProcesarYGuardarAsync.
        if (modelo.Foto != null)
        {
            var errorFoto = FotoIncidenciaService.ValidarArchivo(modelo.Foto);
            if (errorFoto != null)
            {
                ModelState.AddModelError(nameof(modelo.Foto), errorFoto);
            }
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // Proteger la foto: Google Vision detecta rostros, se pixelan y SOLO se guarda la versión protegida.
        // Si algo falla, no se guarda ninguna foto y el vecino puede intentarlo otra vez.
        string? fotoUrl = null;
        if (modelo.Foto != null)
        {
            var resultado = await _fotos.ProcesarYGuardarAsync(modelo.Foto, HttpContext.RequestAborted);
            if (!resultado.Exito)
            {
                ModelState.AddModelError(nameof(modelo.Foto), resultado.Error!);
                return View(modelo);
            }

            fotoUrl = resultado.Url;
        }

        // Id, Estado, FechaRegistro y UsuarioId los decide el servidor, nunca el formulario.
        var ahora = DateTime.Now;
        var incidencia = new Incidencia
        {
            Titulo = modelo.Titulo!,
            Descripcion = modelo.Descripcion!,
            Categoria = modelo.Categoria!,
            Ubicacion = modelo.Ubicacion!,
            FotoUrl = fotoUrl,
            Estado = EstadosIncidencia.Pendiente,
            FechaRegistro = ahora,
            UsuarioId = usuario.Id
        };

        // Primer registro del historial: la creación del reporte (sin estado anterior).
        // Se guarda en el mismo SaveChangesAsync que la incidencia: se guardan los dos o ninguno.
        incidencia.Historial.Add(new HistorialEstadoIncidencia
        {
            EstadoAnterior = null,
            EstadoNuevo = EstadosIncidencia.Pendiente,
            FechaCambio = ahora,
            UsuarioId = usuario.Id
        });

        try
        {
            _context.Incidencias.Add(incidencia);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // El detalle técnico queda en el log; el vecino solo ve un mensaje amigable.
            _logger.LogError(ex, "Error al guardar la incidencia \"{Titulo}\"", incidencia.Titulo);

            // Si la incidencia no se guardó, tampoco debe quedar su foto en el servidor.
            if (fotoUrl != null)
            {
                _fotos.Eliminar(fotoUrl);
            }

            ModelState.AddModelError(string.Empty, "No pudimos registrar tu incidencia. Inténtalo nuevamente.");
            return View(modelo);
        }

        // La incidencia ya está en SQLite: ahora se agrega al índice de búsqueda.
        // Si Algolia falla, la incidencia NO se deshace; el servicio registra el problema y un
        // administrador puede resincronizar el índice desde el panel.
        await _algolia.IndexarAsync(incidencia);

        // TempData sobrevive a la redirección y se muestra una sola vez.
        // Los datos de la confirmación salen de la incidencia guardada, no del formulario.
        TempData["ReporteCreadoId"] = incidencia.Id;
        TempData["ReporteCreadoTitulo"] = incidencia.Titulo;
        TempData["ReporteCreadoEstado"] = incidencia.Estado;
        TempData["ReporteCreadoFecha"] = incidencia.FechaRegistro.ToString("dd/MM/yyyy HH:mm");
        return RedirectToAction(nameof(Incidencias));
    }

    // Título y ubicación: una sola línea, sin espacios al inicio/final ni repetidos.
    // No cambia mayúsculas ni el resto del texto. Si solo había espacios, queda null (campo obligatorio).
    private static string? UnaLinea(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        return string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    // Descripción: se respeta lo escrito (incluidos los saltos de línea); solo se quitan los espacios
    // del inicio y el final, y los saltos "\r\n" del navegador se guardan como "\n" (1 carácter,
    // igual que en el contador del formulario).
    private static string? TextoLargo(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        return texto.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }

    // GET: /Home/Incidencias?buscar=poste&categoria=alumbrado&estado=EnRevision
    public async Task<IActionResult> Incidencias(string? buscar, string? categoria, EstadoIncidencia? estado)
    {
        // Parámetros de la URL validados: categoría solo si existe en el catálogo, estado solo si es válido
        // (el model binding deja null un valor desconocido) y texto limpio con un largo máximo.
        var categoriaElegida = CatalogoCategorias.PorSlug(categoria);
        buscar = BusquedaIncidenciasService.NormalizarTexto(buscar);

        // Algolia (si está configurado) o SQLite; las incidencias mostradas siempre salen de SQLite.
        var resultado = await _busqueda.BuscarAsync(buscar, categoriaElegida, estado, HttpContext.RequestAborted);

        var modelo = new IncidenciasListadoViewModel
        {
            Incidencias = resultado.Incidencias.Select(IncidenciaViewModel.DesdeEntidad).ToList(),
            Categorias = CatalogoCategorias.Todas,
            Buscar = buscar,
            Categoria = categoriaElegida?.Slug,
            Estado = estado,
            Total = await _context.Incidencias.CountAsync(),
            Motor = resultado.Motor,
            BusquedaAvanzadaNoDisponible = resultado.BusquedaAvanzadaNoDisponible
        };

        return View(modelo);
    }

    // GET: /Home/MisIncidencias
    // Solo las incidencias del usuario de la sesión. No recibe parámetros: el Id del usuario
    // sale de la cookie de Identity, así que nadie puede pedir las incidencias de otra persona.
    [Authorize]
    public async Task<IActionResult> MisIncidencias()
    {
        var usuarioId = _userManager.GetUserId(User);

        // Sin este control, un Id nulo filtraría "UsuarioId IS NULL" y mostraría las incidencias antiguas.
        if (string.IsNullOrEmpty(usuarioId))
        {
            return Challenge();
        }

        var incidencias = await _context.Incidencias
            .AsNoTracking()
            .Where(i => i.UsuarioId == usuarioId)
            .OrderByDescending(i => i.FechaRegistro)
            .ToListAsync();

        return View(incidencias.Select(IncidenciaViewModel.DesdeEntidad).ToList());
    }

    // GET: /Home/Seguimiento/5
    // Historial de estados de una incidencia PROPIA. Si la incidencia no existe o es de otra persona,
    // se responde 404 igual en ambos casos, para no revelar qué incidencias existen.
    // Solo lectura: [HttpGet] rechaza cualquier POST (405).
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Seguimiento(int id)
    {
        var usuarioId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(usuarioId))
        {
            return Challenge();
        }

        // El filtro por dueño está en la consulta: nunca se cargan datos de incidencias ajenas.
        // No se incluye Historial.Usuario: el ciudadano no necesita ver quién hizo cada cambio.
        var incidencia = await _context.Incidencias
            .AsNoTracking()
            .Include(i => i.Historial)
            .FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == usuarioId);

        if (incidencia == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NoEncontrada");
        }

        return View(SeguimientoViewModel.DesdeEntidad(incidencia, mostrarUsuarios: false));
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
