using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Models;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Helpers;

// Filtros de búsqueda compartidos por el sitio público y el panel administrativo.
// Se aplican sobre la consulta, así que el filtrado lo hace SQLite (no la memoria).
// También es la búsqueda de respaldo cuando Algolia no está configurado o no responde.
public static class FiltrosIncidencias
{
    // Límite de palabras que se buscan (evita consultas enormes con textos muy largos).
    private const int MaximoPalabras = 8;

    public static IQueryable<Incidencia> Filtrar(
        this IQueryable<Incidencia> consulta, string? buscar, CategoriaViewModel? categoria, EstadoIncidencia? estado)
    {
        // Texto libre: cada palabra debe aparecer en el título, la descripción, la ubicación o la categoría.
        // Ej: "poste apagado" encuentra "Poste de alumbrado apagado".
        // LIKE de SQLite no distingue mayúsculas/minúsculas (sin tildes). % y _ se escapan: se buscan como texto.
        foreach (var palabra in Palabras(buscar))
        {
            var patron = $"%{EscaparLike(palabra)}%";
            consulta = consulta.Where(i =>
                EF.Functions.Like(i.Titulo, patron, "\\") ||
                EF.Functions.Like(i.Descripcion, patron, "\\") ||
                EF.Functions.Like(i.Ubicacion, patron, "\\") ||
                EF.Functions.Like(i.Categoria, patron, "\\"));
        }

        if (categoria != null)
        {
            consulta = consulta.Where(i => i.Categoria == categoria.Nombre);
        }

        if (estado != null)
        {
            var estadoTexto = estado.Value.Texto();
            consulta = consulta.Where(i => i.Estado == estadoTexto);
        }

        return consulta;
    }

    private static IEnumerable<string> Palabras(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? Enumerable.Empty<string>()
            : texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Distinct().Take(MaximoPalabras);

    private static string EscaparLike(string texto) =>
        texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
