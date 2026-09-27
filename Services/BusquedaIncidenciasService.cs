using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Data;
using proyectoGrupal.Helpers;
using proyectoGrupal.Models;
using proyectoGrupal.Services.Algolia;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Services;

// Qué resolvió la búsqueda de texto.
public enum MotorBusqueda
{
    SinTexto,   // no se escribió nada: solo filtros (SQLite)
    Algolia,    // búsqueda avanzada
    Sqlite      // búsqueda básica (Algolia no configurado o no disponible)
}

public record ResultadoBusquedaIncidencias(
    List<Incidencia> Incidencias,
    MotorBusqueda Motor,
    bool BusquedaAvanzadaNoDisponible);

// Búsqueda pública de incidencias (/Home/Incidencias).
//   - Los filtros exactos (categoría, estado) siempre se aplican en SQLite.
//   - Con texto: Algolia devuelve solo los Id; las incidencias reales se leen de SQLite (fuente de verdad).
//   - Si Algolia no está configurado o falla, se usa la búsqueda básica de SQLite con los mismos filtros.
public class BusquedaIncidenciasService
{
    public const int LargoMaximoTexto = 100;

    private readonly ApplicationDbContext _context;
    private readonly IAlgoliaIncidenciaService _algolia;

    public BusquedaIncidenciasService(ApplicationDbContext context, IAlgoliaIncidenciaService algolia)
    {
        _context = context;
        _algolia = algolia;
    }

    public async Task<ResultadoBusquedaIncidencias> BuscarAsync(
        string? texto, CategoriaViewModel? categoria, EstadoIncidencia? estado, CancellationToken cancellationToken = default)
    {
        // Solo incidencias públicas (las mismas que ya muestra el listado) con los filtros exactos.
        var consulta = _context.Incidencias.AsNoTracking().Filtrar(null, categoria, estado);

        if (string.IsNullOrWhiteSpace(texto))
        {
            var todas = await consulta.OrderByDescending(i => i.FechaRegistro).ToListAsync(cancellationToken);
            return new ResultadoBusquedaIncidencias(todas, MotorBusqueda.SinTexto, false);
        }

        var noDisponible = false;
        if (_algolia.EstaConfigurado)
        {
            var resultado = await _algolia.BuscarIdsAsync(texto, categoria?.Nombre, estado?.Texto(), cancellationToken);
            if (resultado.Exito)
            {
                // Una sola consulta a SQLite con todos los Id. Se vuelven a aplicar los filtros:
                // si el índice estuviera desactualizado, manda lo que dice la base de datos.
                var ids = resultado.Ids;
                var encontradas = ids.Count == 0
                    ? new List<Incidencia>()
                    : await consulta.Where(i => ids.Contains(i.Id)).ToListAsync(cancellationToken);

                // Mismo orden que Algolia (relevancia).
                var posicion = ids.Select((id, indice) => (id, indice)).ToDictionary(p => p.id, p => p.indice);
                encontradas.Sort((a, b) => posicion[a.Id].CompareTo(posicion[b.Id]));

                return new ResultadoBusquedaIncidencias(encontradas, MotorBusqueda.Algolia, false);
            }

            noDisponible = true;
        }

        var basicas = await consulta.Filtrar(texto, null, null)
            .OrderByDescending(i => i.FechaRegistro)
            .ToListAsync(cancellationToken);
        return new ResultadoBusquedaIncidencias(basicas, MotorBusqueda.Sqlite, noDisponible);
    }

    // Limpia el texto recibido por la URL: sin espacios extremos y con un largo razonable.
    public static string? NormalizarTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var limpio = string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return limpio.Length <= LargoMaximoTexto ? limpio : limpio[..LargoMaximoTexto].TrimEnd();
    }
}
