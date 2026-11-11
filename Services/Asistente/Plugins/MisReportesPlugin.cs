using System.ComponentModel;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using proyectoGrupal.Data;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Services.Asistente.Plugins;

// Herramienta del agente: los reportes del vecino que inició sesión y su historial de estados.
// Solo se entrega al modelo si hay sesión (ver AsistenteService). El usuario sale SIEMPRE de la cookie
// de Identity, nunca de un argumento del modelo: así nadie puede pedir los reportes de otra persona.
public sealed class MisReportesPlugin
{
    private const int MaximoReportes = 5;
    private static readonly CultureInfo Peru = new("es-PE");

    private readonly ApplicationDbContext _context;
    private readonly SesionAsistente _sesion;
    private readonly LinkGenerator _enlaces;
    private readonly IHttpContextAccessor _http;

    public MisReportesPlugin(ApplicationDbContext context, SesionAsistente sesion, LinkGenerator enlaces, IHttpContextAccessor http)
    {
        _context = context;
        _sesion = sesion;
        _enlaces = enlaces;
        _http = http;
    }

    [KernelFunction("MisReportes")]
    [Description("Reportes del vecino que inició sesión, con su estado e historial. Sin número: los más recientes; con número: el historial de ese reporte.")]
    public async Task<string> MisReportesAsync(
        [Description("Número del reporte (opcional).")] int? numero = null,
        CancellationToken cancellationToken = default)
    {
        var contexto = _http.HttpContext;
        var usuarioId = contexto?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(usuarioId))
        {
            return "El vecino no ha iniciado sesión: dile que inicie sesión para ver sus reportes en \"Mis incidencias\".";
        }

        // El filtro por dueño está en la consulta: nunca se leen reportes de otra persona.
        var consulta = _context.Incidencias
            .AsNoTracking()
            .Include(i => i.Historial)
            .Where(i => i.UsuarioId == usuarioId);

        if (numero is > 0)
        {
            consulta = consulta.Where(i => i.Id == numero);
        }

        var total = numero is > 0 ? 0 : await consulta.CountAsync(cancellationToken);
        var reportes = await consulta
            .OrderByDescending(i => i.FechaRegistro)
            .Take(MaximoReportes)
            .ToListAsync(cancellationToken);

        if (reportes.Count == 0)
        {
            return numero is > 0
                ? $"El vecino no tiene un reporte con el número {numero} (o no es suyo)."
                : "El vecino todavía no ha registrado reportes.";
        }

        var texto = new StringBuilder(numero is > 0
            ? "Reporte del vecino (se muestra como tarjeta con enlace al seguimiento):"
            : $"El vecino tiene {total} reporte(s). Los {reportes.Count} más recientes (se muestran como tarjetas con enlace al seguimiento):");

        foreach (var incidencia in reportes)
        {
            var seguimiento = SeguimientoViewModel.DesdeEntidad(incidencia, mostrarUsuarios: false);
            var vista = seguimiento.Incidencia;
            var ultima = seguimiento.UltimaActualizacion ?? vista.Fecha;

            _sesion.AgregarIncidencia(new TarjetaIncidencia
            {
                Id = vista.Id,
                Titulo = vista.Titulo,
                Categoria = vista.Categoria.Nombre,
                Estado = vista.Estado.Texto(),
                EstadoClase = vista.Estado.ClaseCss(),
                Ubicacion = vista.Ubicacion,
                Fecha = "Actualizado el " + ultima.ToString("d MMM yyyy", Peru),
                Url = (contexto != null ? _enlaces.GetPathByAction(contexto, "Seguimiento", "Home", new { id = vista.Id }) : null)
                      ?? $"/Home/Seguimiento/{vista.Id}"
            });

            texto.AppendLine().AppendLine();
            texto.AppendLine($"- #{vista.Id} \"{vista.Titulo}\" · {vista.Estado.Texto()} · reportado el {vista.Fecha.ToString("d MMM yyyy", Peru)}");
            var cambios = seguimiento.Historial.Where(h => !h.EsCreacion).ToList();
            texto.Append(cambios.Count == 0
                ? "  Sin cambios de estado todavía."
                : "  Cambios: " + string.Join("; ", cambios.Select(h =>
                    $"{h.EstadoAnterior?.Texto()} → {h.EstadoNuevo.Texto()} el {h.Fecha.ToString("d MMM yyyy", Peru)}")));
        }

        return texto.ToString();
    }
}
