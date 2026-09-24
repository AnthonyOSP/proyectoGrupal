namespace proyectoGrupal.ViewModels;

// Datos del dashboard administrativo (/Admin). Todos los números salen de SQLite.
public class AdminDashboardViewModel
{
    public int Total { get; set; }
    public int Pendientes { get; set; }
    public int EnRevision { get; set; }
    public int Atendidas { get; set; }

    public List<IncidenciaViewModel> UltimasIncidencias { get; set; } = new();

    // Porcentaje de un estado respecto al total (para la barra de distribución).
    public int Porcentaje(int cantidad) => Total == 0 ? 0 : (int)Math.Round(cantidad * 100.0 / Total);
}
