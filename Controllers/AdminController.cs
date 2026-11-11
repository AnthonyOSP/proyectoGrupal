using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Constants;
using proyectoGrupal.Data;
using proyectoGrupal.Helpers;
using proyectoGrupal.Models;
using proyectoGrupal.Services;
using proyectoGrupal.Services.Algolia;
using proyectoGrupal.Services.Asistente;
using proyectoGrupal.Services.Asistente.Conocimiento;
using proyectoGrupal.Services.PieSocket;
using proyectoGrupal.Services.Sentimiento;
using proyectoGrupal.Services.Categorias;
using proyectoGrupal.Services.Zonas;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Controllers;

// Panel administrativo: revisar incidencias, cambiar su estado y eliminarlas.
// Todas las acciones exigen el rol Administrador:
//   - sin sesión        → /Account/Login
//   - sesión sin el rol → /Account/AccessDenied
[Authorize(Roles = RoleNames.Administrador)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAlgoliaIncidenciaService _algolia;
    private readonly IPieSocketRealtimeService _realtime;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAlgoliaIncidenciaService algolia,
        IPieSocketRealtimeService realtime,
        ILogger<AdminController> logger)
    {
        _context = context;
        _userManager = userManager;
        _algolia = algolia;
        _realtime = realtime;
        _logger = logger;
    }

    // GET: /Admin
    public async Task<IActionResult> Index([FromServices] ICategoriaSugeridaService clasificador)
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
            UltimasIncidencias = ultimas.Select(IncidenciaViewModel.DesdeEntidad).ToList(),
            AlgoliaConfigurado = _algolia.EstaConfigurado,
            IndiceBusqueda = _algolia.NombreIndice,
            ClasificadorDisponible = clasificador.EstaDisponible,
            ClasificadorInfo = clasificador.Info
        };

        return View(modelo);
    }

    // POST: /Admin/SincronizarBusqueda
    // Sincronización completa SQLite → Algolia (inicial, o después de un fallo de Algolia).
    // Solo administradores (atributo de la clase) y con token antiforgery. Reemplaza el índice
    // completo, así que se puede repetir sin crear duplicados.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SincronizarBusqueda()
    {
        if (!_algolia.EstaConfigurado)
        {
            TempData["MensajeBusqueda"] = "Algolia no está configurado: la búsqueda usa SQLite y no hay nada que sincronizar.";
            TempData["BusquedaOk"] = false;
            return RedirectToAction(nameof(Index));
        }

        var incidencias = await _context.Incidencias.AsNoTracking().ToListAsync();
        var resultado = await _algolia.SincronizarTodoAsync(incidencias, HttpContext.RequestAborted);

        _logger.LogInformation("Sincronización del índice de búsqueda por {Usuario}: {Resultado}", User.Identity?.Name, resultado.Mensaje);
        TempData["MensajeBusqueda"] = resultado.Mensaje;
        TempData["BusquedaOk"] = resultado.Exito;
        return RedirectToAction(nameof(Index));
    }

    // ---------- Asistente virtual: base de conocimiento ----------

    private const long TamanoMaximoDocumento = 1024 * 1024; // 1 MB de texto es mucho para un documento de ayuda
    private const int LargoMaximoConsulta = 200;

    // GET: /Admin/Asistente?consulta=limite de reportes
    // Estado del asistente, documentos cargados en Qdrant y prueba de búsqueda (solo lectura).
    public async Task<IActionResult> Asistente(string? consulta,
        [FromServices] IBaseConocimientoService conocimiento,
        [FromServices] IAsistenteService asistente)
    {
        var modelo = new AsistenteAdminViewModel
        {
            AsistenteDisponible = asistente.EstaDisponible,
            Modelo = asistente.Modelo,
            UsaQdrant = conocimiento.UsaQdrant,
            Coleccion = conocimiento.Opciones.Coleccion,
            ModeloEmbedding = conocimiento.Opciones.ModeloEmbedding,
            ArchivosCarpeta = conocimiento.ArchivosCarpeta,
            Consulta = BusquedaIncidenciasService.NormalizarTexto(consulta) is { } texto
                ? (texto.Length <= LargoMaximoConsulta ? texto : texto[..LargoMaximoConsulta])
                : null
        };

        if (conocimiento.UsaQdrant)
        {
            try
            {
                modelo.Documentos = await conocimiento.ListarDocumentosAsync(HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                modelo.ErrorDocumentos = ex.Message;
            }
        }

        if (modelo.Consulta != null)
        {
            modelo.Busqueda = await conocimiento.BuscarAsync(modelo.Consulta, 5, HttpContext.RequestAborted);
        }

        return View(modelo);
    }

    // POST: /Admin/SincronizarConocimiento
    // Carga (o vuelve a cargar) en Qdrant los documentos de la carpeta Conocimiento/. Se puede repetir sin duplicar.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SincronizarConocimiento([FromServices] IBaseConocimientoService conocimiento)
    {
        var resultado = await conocimiento.SincronizarCarpetaAsync(HttpContext.RequestAborted);
        _logger.LogInformation("Sincronización de la base de conocimiento por {Usuario}: {Mensaje}", User.Identity?.Name, resultado.Mensaje);

        TempData["MensajeConocimiento"] = resultado.Mensaje;
        TempData["ConocimientoOk"] = resultado.Exito;
        return RedirectToAction(nameof(Asistente));
    }

    // POST: /Admin/CargarConocimiento  (documento extra: Markdown o texto)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> CargarConocimiento(IFormFile? archivo, [FromServices] IBaseConocimientoService conocimiento)
    {
        var extension = Path.GetExtension(archivo?.FileName ?? string.Empty).ToLowerInvariant();
        string? error = archivo switch
        {
            null or { Length: 0 } => "Elige un archivo Markdown (.md) o de texto (.txt).",
            _ when extension is not (".md" or ".markdown" or ".txt") => "Solo se aceptan archivos .md o .txt.",
            { Length: > TamanoMaximoDocumento } => "El archivo supera el límite de 1 MB.",
            _ => null
        };

        if (error == null)
        {
            string texto;
            using (var lector = new StreamReader(archivo!.OpenReadStream(), System.Text.Encoding.UTF8))
            {
                texto = await lector.ReadToEndAsync(HttpContext.RequestAborted);
            }

            var nombre = Path.GetFileName(archivo.FileName);
            var resultado = string.IsNullOrWhiteSpace(texto)
                ? new ResultadoOperacionConocimiento(false, "El archivo está vacío.")
                : await conocimiento.CargarDocumentoAsync(nombre, texto, HttpContext.RequestAborted);

            _logger.LogInformation("Documento de conocimiento '{Archivo}' cargado por {Usuario}: {Mensaje}", nombre, User.Identity?.Name, resultado.Mensaje);
            TempData["MensajeConocimiento"] = resultado.Mensaje;
            TempData["ConocimientoOk"] = resultado.Exito;
        }
        else
        {
            TempData["MensajeConocimiento"] = error;
            TempData["ConocimientoOk"] = false;
        }

        return RedirectToAction(nameof(Asistente));
    }

    // POST: /Admin/EliminarConocimiento
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarConocimiento(string? id, [FromServices] IBaseConocimientoService conocimiento)
    {
        var resultado = string.IsNullOrWhiteSpace(id) || id.Length > 100
            ? new ResultadoOperacionConocimiento(false, "Documento no válido.")
            : await conocimiento.EliminarDocumentoAsync(id, HttpContext.RequestAborted);

        _logger.LogInformation("Documento de conocimiento {Id} eliminado por {Usuario}: {Mensaje}", id, User.Identity?.Name, resultado.Mensaje);
        TempData["MensajeConocimiento"] = resultado.Mensaje;
        TempData["ConocimientoOk"] = resultado.Exito;
        return RedirectToAction(nameof(Asistente));
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

    // GET: /Admin/Seguimiento/5
    // Historial completo de cualquier incidencia, con el correo de quien hizo cada cambio.
    // Solo lectura: el historial solo se escribe en CambiarEstado.
    [HttpGet]
    public async Task<IActionResult> Seguimiento(int id)
    {
        var incidencia = await _context.Incidencias
            .AsNoTracking()
            .Include(i => i.Historial)
                .ThenInclude(h => h.Usuario)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (incidencia == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NoEncontrada");
        }

        return View(SeguimientoViewModel.DesdeEntidad(incidencia, mostrarUsuarios: true));
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

        // El estado debe ser uno de los permitidos (no se confía en los botones del HTML).
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
        var fechaCambio = DateTime.Now;

        // Registro del cambio. El administrador sale de la sesión (Identity) y la fecha del servidor:
        // ninguno de estos datos llega desde el formulario.
        _context.HistorialEstadosIncidencia.Add(new HistorialEstadoIncidencia
        {
            IncidenciaId = incidencia.Id,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = estado,
            FechaCambio = fechaCambio,
            UsuarioId = _userManager.GetUserId(User)
        });

        // Un solo SaveChangesAsync: EF Core guarda el nuevo estado y el historial en la misma transacción.
        // Si falla, no queda ninguno de los dos.
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

        // El cambio ya está guardado en SQLite; ahora se actualiza el índice de búsqueda.
        // Si Algolia falla, el estado NO se deshace: se avisa al administrador para que resincronice.
        var indexado = await _algolia.IndexarAsync(incidencia);
        TempData["MensajeAdmin"] = indexado || !_algolia.EstaConfigurado
            ? "Estado actualizado correctamente."
            : "Estado actualizado correctamente. El índice de búsqueda no se pudo actualizar: puedes resincronizarlo desde el Dashboard.";

        // Aviso en tiempo real (seguimiento del ciudadano y panel). Solo DESPUÉS de confirmar SQLite:
        // si PieSocket falla, el estado y el historial ya están guardados y no se deshacen.
        await _realtime.NotificarEstadoActualizadoAsync(incidencia, estadoAnterior, fechaCambio);

        return RedirectToAction(nameof(Detalle), new { id });
    }

    // POST: /Admin/Eliminar/5
    // Borra la incidencia, su historial (en cascada) y su foto. No se puede deshacer.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id, [FromServices] FotoIncidenciaService fotos)
    {
        var incidencia = await _context.Incidencias.FirstOrDefaultAsync(i => i.Id == id);

        if (incidencia == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NoEncontrada");
        }

        _context.Incidencias.Remove(incidencia);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error al eliminar la incidencia {Id}", id);
            ModelState.AddModelError("eliminar", "No pudimos eliminar la incidencia. Inténtalo nuevamente.");
            return View(nameof(Detalle), IncidenciaViewModel.DesdeEntidad(incidencia));
        }

        _logger.LogInformation("Incidencia {Id} eliminada por {Usuario}", id, User.Identity?.Name);

        // La incidencia ya no está en SQLite: ahora se borra la foto del disco y se quita del índice de búsqueda.
        // Si Algolia falla, no se deshace nada: la búsqueda ignora los Id que ya no existen y se puede resincronizar.
        if (incidencia.FotoUrl != null)
        {
            fotos.Eliminar(incidencia.FotoUrl);
        }
        await _algolia.EliminarAsync(id);

        TempData["MensajeAdmin"] = $"La incidencia #{id} se eliminó correctamente.";
        return RedirectToAction(nameof(Incidencias));
    }

    // GET: /Admin/Mensajes?fecha=2026-11-05&sentimiento=Negativo&buscar=cuenta
    // Reporte de los mensajes de contacto de un día (por defecto, hoy): cuántos son positivos y
    // negativos según el modelo de ML.NET, y la lista de ese día con filtros.
    public async Task<IActionResult> Mensajes(DateOnly? fecha, string? sentimiento, string? buscar,
        [FromServices] ISentimientoService analisis)
    {
        var dia = fecha ?? DateOnly.FromDateTime(DateTime.Now);
        buscar = BusquedaIncidenciasService.NormalizarTexto(buscar);
        // Solo valores conocidos ("Positivo" / "Negativo"); cualquier otro se ignora.
        sentimiento = SentimientosMensaje.Todos.FirstOrDefault(s => s == sentimiento);

        // Mensajes del día: desde las 00:00 hasta antes de las 00:00 del día siguiente (hora del servidor).
        var inicio = dia.ToDateTime(TimeOnly.MinValue);
        var fin = inicio.AddDays(1);
        var delDia = await _context.MensajesContacto
            .AsNoTracking()
            .Where(m => m.FechaRegistro >= inicio && m.FechaRegistro < fin)
            .OrderByDescending(m => m.FechaRegistro)
            .ToListAsync();

        var mensajes = delDia.AsEnumerable();
        if (sentimiento != null)
        {
            mensajes = mensajes.Where(m => m.Sentimiento == sentimiento);
        }

        // La búsqueda se hace en memoria para ignorar mayúsculas Y tildes ("maria" encuentra "María"),
        // algo que SQLite no hace por sí solo. Es suficiente para los mensajes de un día.
        if (buscar != null)
        {
            var comparador = System.Globalization.CultureInfo.InvariantCulture.CompareInfo;
            const System.Globalization.CompareOptions opciones =
                System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace;
            bool Contiene(string texto) => comparador.IndexOf(texto, buscar, opciones) >= 0;

            mensajes = mensajes.Where(m => Contiene(m.Nombre) || Contiene(m.Email) || Contiene(m.Mensaje));
        }

        return View(new MensajesAdminViewModel
        {
            Fecha = dia,
            Buscar = buscar,
            Sentimiento = sentimiento,
            TotalDia = delDia.Count,
            PositivosDia = delDia.Count(m => m.Sentimiento == SentimientosMensaje.Positivo),
            NegativosDia = delDia.Count(m => m.Sentimiento == SentimientosMensaje.Negativo),
            ModeloDisponible = analisis.EstaDisponible,
            InfoModelo = analisis.Info,
            Correcciones = await _context.MensajesContacto.CountAsync(m => m.SentimientoManual),
            Mensajes = mensajes.ToList()
        });
    }

    // POST: /Admin/CambiarSentimiento/5
    // El administrador fija a mano si el mensaje es positivo o negativo (corrige o confirma al modelo).
    // Ese mensaje queda como ejemplo para el próximo "Reentrenar modelo".
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarSentimiento(int id, string? nuevo, DateOnly? fecha, string? sentimiento, string? buscar)
    {
        var volver = new { fecha = fecha?.ToString("yyyy-MM-dd"), sentimiento, buscar };

        // Solo "Positivo" o "Negativo" (no se confía en el HTML del navegador).
        if (!SentimientosMensaje.Todos.Contains(nuevo))
        {
            TempData["MensajeAdmin"] = "Elige un sentimiento válido.";
            TempData["MensajeAdminError"] = true;
            return RedirectToAction(nameof(Mensajes), volver);
        }

        var mensaje = await _context.MensajesContacto.FirstOrDefaultAsync(m => m.Id == id);
        if (mensaje == null)
        {
            TempData["MensajeAdmin"] = $"El mensaje #{id} ya no existe.";
            TempData["MensajeAdminError"] = true;
            return RedirectToAction(nameof(Mensajes), volver);
        }

        var corregido = mensaje.Sentimiento != nuevo;
        mensaje.Sentimiento = nuevo;
        mensaje.SentimientoManual = true;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error al cambiar el sentimiento del mensaje {Id}", id);
            TempData["MensajeAdmin"] = "No pudimos guardar el cambio. Inténtalo nuevamente.";
            TempData["MensajeAdminError"] = true;
            return RedirectToAction(nameof(Mensajes), volver);
        }

        TempData["MensajeAdmin"] = corregido
            ? $"El mensaje de {mensaje.Nombre} ahora es {nuevo!.ToLowerInvariant()}. Se usará al reentrenar el modelo."
            : $"Confirmaste que el mensaje de {mensaje.Nombre} es {nuevo!.ToLowerInvariant()}. Se usará al reentrenar el modelo.";
        return RedirectToAction(nameof(Mensajes), volver);
    }

    // POST: /Admin/ReentrenarModelo
    // Entrena un modelo nuevo con el CSV + los mensajes que el administrador fijó a mano, lo empieza a
    // usar enseguida y vuelve a analizar los mensajes que clasificó el modelo (no los corregidos).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReentrenarModelo(DateOnly? fecha, [FromServices] ISentimientoService analisis)
    {
        var volver = new { fecha = fecha?.ToString("yyyy-MM-dd") };

        var correcciones = await _context.MensajesContacto
            .AsNoTracking()
            .Where(m => m.SentimientoManual && m.Sentimiento != null)
            .Select(m => new EntradaSentimiento { Comentario = m.Mensaje, Label = m.Sentimiento == SentimientosMensaje.Positivo })
            .ToListAsync();

        try
        {
            var info = await analisis.ReentrenarAsync(correcciones);
            var reanalizados = await SentimientoService.AnalizarMensajesAsync(_context, analisis, todosLosAutomaticos: true);

            _logger.LogInformation("Modelo de sentimientos reentrenado por {Usuario}", User.Identity?.Name);
            var marcados = info.Correcciones == 1 ? "1 marcado" : $"{info.Correcciones} marcados";
            TempData["MensajeAdmin"] =
                $"Modelo reentrenado con {info.EjemplosCsv + info.Correcciones} ejemplos ({marcados} a mano por el administrador). " +
                $"Exactitud: {info.Exactitud:P1}. Se volvieron a analizar {reanalizados} mensajes.";
        }
        catch (Exception ex)
        {
            // Por ejemplo: falta el CSV o no hay permiso para escribir en MLModels/. El modelo anterior sigue funcionando.
            _logger.LogError(ex, "Error al reentrenar el modelo de sentimientos");
            TempData["MensajeAdmin"] = "No se pudo reentrenar el modelo. Se sigue usando el anterior. Revisa el log del servidor.";
            TempData["MensajeAdminError"] = true;
        }

        return RedirectToAction(nameof(Mensajes), volver);
    }

    // POST: /Admin/EliminarMensaje/5
    // Borra un mensaje de contacto. No se puede deshacer. Vuelve al mismo día y con los mismos filtros.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarMensaje(int id, DateOnly? fecha, string? sentimiento, string? buscar)
    {
        var volver = new { fecha = fecha?.ToString("yyyy-MM-dd"), sentimiento, buscar };
        var mensaje = await _context.MensajesContacto.FirstOrDefaultAsync(m => m.Id == id);

        if (mensaje == null)
        {
            // Pudo haberlo borrado otro administrador: se vuelve a la bandeja con un aviso.
            TempData["MensajeAdmin"] = $"El mensaje #{id} ya no existe.";
            TempData["MensajeAdminError"] = true;
            return RedirectToAction(nameof(Mensajes), volver);
        }

        _context.MensajesContacto.Remove(mensaje);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error al eliminar el mensaje de contacto {Id}", id);
            TempData["MensajeAdmin"] = "No pudimos eliminar el mensaje. Inténtalo nuevamente.";
            TempData["MensajeAdminError"] = true;
            return RedirectToAction(nameof(Mensajes), volver);
        }

        _logger.LogInformation("Mensaje de contacto {Id} eliminado por {Usuario}", id, User.Identity?.Name);
        TempData["MensajeAdmin"] = $"El mensaje de {mensaje.Nombre} se eliminó correctamente.";
        return RedirectToAction(nameof(Mensajes), volver);
    }

    // GET: /Admin/Zonas?estado=abiertas&categoria=limpieza&zonas=4
    // Clustering K-Means: agrupa las incidencias por ubicación para ver dónde se concentran los problemas.
    public async Task<IActionResult> Zonas(string? estado, string? categoria, int? zonas,
        [FromServices] IZonasCriticasService clustering)
    {
        // Filtros validados: valores desconocidos se ignoran.
        estado = estado == ZonasAdminViewModel.EstadoTodas ? ZonasAdminViewModel.EstadoTodas : ZonasAdminViewModel.EstadoAbiertas;
        var categoriaElegida = CatalogoCategorias.PorSlug(categoria);
        int? numeroPedido = zonas is >= 2 and <= IZonasCriticasService.MaximoZonas ? zonas : null;

        var consulta = _context.Incidencias.AsNoTracking();
        if (estado == ZonasAdminViewModel.EstadoAbiertas)
        {
            consulta = consulta.Where(i => i.Estado != EstadosIncidencia.Atendida);
        }
        if (categoriaElegida != null)
        {
            consulta = consulta.Where(i => i.Categoria == categoriaElegida.Nombre);
        }

        var sinUbicacion = await consulta.CountAsync(i => i.Latitud == null || i.Longitud == null);
        var puntos = (await consulta
                .Where(i => i.Latitud != null && i.Longitud != null)
                .Select(i => new { i.Id, i.Titulo, i.Categoria, i.Estado, i.Latitud, i.Longitud })
                .ToListAsync())
            .Select(i => new PuntoZona(i.Id, i.Titulo, i.Categoria, i.Estado, (double)i.Latitud!.Value, (double)i.Longitud!.Value))
            .ToList();

        return View(new ZonasAdminViewModel
        {
            Estado = estado,
            Categoria = categoriaElegida?.Slug,
            NumeroPedido = numeroPedido,
            Categorias = CatalogoCategorias.Todas,
            ConUbicacion = puntos.Count,
            SinUbicacion = sinUbicacion,
            Resultado = clustering.Calcular(puntos, numeroPedido)
        });
    }

    // POST: /Admin/ReentrenarCategorias
    // Reentrena el clasificador de categorías (multiclase) con el CSV + las incidencias ya registradas
    // (su título y descripción con la categoría que eligió el vecino) y lo empieza a usar enseguida.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReentrenarCategorias([FromServices] ICategoriaSugeridaService clasificador)
    {
        var incidencias = (await _context.Incidencias
                .AsNoTracking()
                .Select(i => new { i.Titulo, i.Descripcion, i.Categoria })
                .ToListAsync())
            .Where(i => CategoriasIncidencia.Todas.Contains(i.Categoria))
            .Select(i => new EntradaCategoria { Texto = $"{i.Titulo}. {i.Descripcion}", Categoria = i.Categoria })
            .ToList();

        try
        {
            var info = await clasificador.ReentrenarAsync(incidencias);
            _logger.LogInformation("Clasificador de categorías reentrenado por {Usuario}", User.Identity?.Name);
            TempData["MensajeCategorias"] =
                $"Clasificador reentrenado con {info.EjemplosCsv + info.EjemplosIncidencias} ejemplos " +
                $"({info.EjemplosIncidencias} de incidencias reales). Exactitud: {info.ExactitudMicro:P1}.";
            TempData["CategoriasOk"] = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al reentrenar el clasificador de categorías");
            TempData["MensajeCategorias"] = "No se pudo reentrenar el clasificador. Se sigue usando el anterior. Revisa el log del servidor.";
            TempData["CategoriasOk"] = false;
        }

        return RedirectToAction(nameof(Index), null, "sugerencia-categorias");
    }
}
