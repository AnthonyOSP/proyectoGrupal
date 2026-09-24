namespace proyectoGrupal.ViewModels;

// Datos de la página de inicio.
public class InicioViewModel
{
    public List<CategoriaViewModel> Categorias { get; set; } = new();

    public List<IncidenciaViewModel> Recientes { get; set; } = new();

    public int TotalReportes { get; set; }
    public int TotalEnRevision { get; set; }
    public int TotalAtendidas { get; set; }
}
