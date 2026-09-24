namespace proyectoGrupal.ViewModels;

// Datos de la página "Incidencias": resultados + filtros elegidos.
public class IncidenciasListadoViewModel
{
    public List<IncidenciaViewModel> Incidencias { get; set; } = new();

    public List<CategoriaViewModel> Categorias { get; set; } = new();

    // Filtros actuales (vienen de la URL).
    public string? Buscar { get; set; }
    public string? Categoria { get; set; }
    public EstadoIncidencia? Estado { get; set; }

    public int Total { get; set; }

    public bool HayFiltros =>
        !string.IsNullOrWhiteSpace(Buscar) || !string.IsNullOrWhiteSpace(Categoria) || Estado != null;
}
