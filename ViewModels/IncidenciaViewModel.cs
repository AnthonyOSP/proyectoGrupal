using System.Globalization;
using proyectoGrupal.Helpers;
using proyectoGrupal.Models;

namespace proyectoGrupal.ViewModels;

// Datos de una incidencia tal como los necesita la vista (tarjetas y detalle).
public class IncidenciaViewModel
{
    private static readonly CultureInfo Peru = new("es-PE");

    public int Id { get; set; }

    public string Titulo { get; set; } = "";

    public string Descripcion { get; set; } = "";

    public CategoriaViewModel Categoria { get; set; } = new();

    public string Ubicacion { get; set; } = "";

    public DateTime Fecha { get; set; }

    public EstadoIncidencia Estado { get; set; }

    // Ruta de la foto. Si es null se muestra un espacio reservado.
    public string? FotoUrl { get; set; }

    // Convierte la entidad de la base de datos en el ViewModel de la vista.
    public static IncidenciaViewModel DesdeEntidad(Incidencia incidencia) => new()
    {
        Id = incidencia.Id,
        Titulo = incidencia.Titulo,
        Descripcion = incidencia.Descripcion,
        Categoria = CatalogoCategorias.PorNombre(incidencia.Categoria),
        Ubicacion = incidencia.Ubicacion,
        Fecha = incidencia.FechaRegistro,
        Estado = EstadoIncidenciaExtensions.DesdeTexto(incidencia.Estado),
        FotoUrl = incidencia.FotoUrl
    };

    // Ej: "23 de septiembre de 2026"
    public string FechaTexto => Fecha.ToString("d 'de' MMMM 'de' yyyy", Peru);

    // Ej: "23 sep. 2026"
    public string FechaCorta => Fecha.ToString("d MMM yyyy", Peru);

    // Reportada en la última semana (para mostrar la fecha relativa).
    public bool EsReciente => (DateTime.Now - Fecha).TotalDays < 7;

    // Ej: "Hace unos minutos", "Hace 3 horas", "Ayer". Si es antigua, muestra la fecha.
    public string FechaRelativa
    {
        get
        {
            var diferencia = DateTime.Now - Fecha;

            if (diferencia.TotalMinutes < 1) return "Hace un momento";
            if (diferencia.TotalMinutes < 60) return "Hace unos minutos";
            if (diferencia.TotalHours < 24)
            {
                var horas = (int)diferencia.TotalHours;
                return horas == 1 ? "Hace 1 hora" : $"Hace {horas} horas";
            }
            if (diferencia.TotalDays < 2) return "Ayer";
            if (diferencia.TotalDays < 7) return $"Hace {(int)diferencia.TotalDays} días";

            return FechaCorta;
        }
    }
}
