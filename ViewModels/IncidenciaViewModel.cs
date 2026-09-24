using System.Globalization;

namespace proyectoGrupal.ViewModels;

// Datos de una incidencia tal como los necesita la vista.
// En la Etapa 2 se llenará desde la base de datos en lugar de los datos demo.
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

    // Ej: "23 de septiembre de 2026"
    public string FechaTexto => Fecha.ToString("d 'de' MMMM 'de' yyyy", Peru);

    // Ej: "23 sep. 2026"
    public string FechaCorta => Fecha.ToString("d MMM yyyy", Peru);
}
