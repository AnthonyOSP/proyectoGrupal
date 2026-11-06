using proyectoGrupal.Services.Zonas;

namespace proyectoGrupal.ViewModels;

// Página "Zonas críticas" del panel (/Admin/Zonas): incidencias agrupadas por ubicación con K-Means.
public class ZonasAdminViewModel
{
    public const string EstadoAbiertas = "abiertas";
    public const string EstadoTodas = "todas";

    // Filtros (ya validados).
    public string Estado { get; set; } = EstadoAbiertas;
    public string? Categoria { get; set; }          // slug, o null = todas
    public int? NumeroPedido { get; set; }          // null = automático

    public List<CategoriaViewModel> Categorias { get; set; } = new();

    // Incidencias que pasan los filtros, con y sin ubicación en el mapa.
    public int ConUbicacion { get; set; }
    public int SinUbicacion { get; set; }

    public ResultadoZonas Resultado { get; set; } = new(Array.Empty<ZonaCritica>(), 0, true, null);

    public bool HayZonas => Resultado.Zonas.Count > 0;
}
