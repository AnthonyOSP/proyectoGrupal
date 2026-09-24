using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Models;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Helpers;

// Filtros de búsqueda compartidos por el sitio público y el panel administrativo.
// Se aplican sobre la consulta, así que el filtrado lo hace SQLite (no la memoria).
public static class FiltrosIncidencias
{
    public static IQueryable<Incidencia> Filtrar(
        this IQueryable<Incidencia> consulta, string? buscar, CategoriaViewModel? categoria, EstadoIncidencia? estado)
    {
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            consulta = consulta.Where(i =>
                EF.Functions.Like(i.Titulo, patron) ||
                EF.Functions.Like(i.Descripcion, patron) ||
                EF.Functions.Like(i.Ubicacion, patron));
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
}
