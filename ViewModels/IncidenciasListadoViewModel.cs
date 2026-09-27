using proyectoGrupal.Services;

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

    // Cómo se resolvió la búsqueda de texto (solo lo usa la página pública).
    public MotorBusqueda Motor { get; set; } = MotorBusqueda.SinTexto;

    // true si Algolia está configurado pero no respondió y se usó la búsqueda básica.
    public bool BusquedaAvanzadaNoDisponible { get; set; }

    public bool HayFiltros =>
        !string.IsNullOrWhiteSpace(Buscar) || !string.IsNullOrWhiteSpace(Categoria) || Estado != null;
}
