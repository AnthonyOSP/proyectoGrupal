using proyectoGrupal.Models;

namespace proyectoGrupal.ViewModels;

// Estados posibles de una incidencia.
// El orden importa: se usa para dibujar la barra de progreso del detalle.
public enum EstadoIncidencia
{
    Pendiente = 0,
    EnRevision = 1,
    Atendida = 2
}

public static class EstadoIncidenciaExtensions
{
    // Texto que se muestra al usuario y que se guarda en la base de datos.
    public static string Texto(this EstadoIncidencia estado) => estado switch
    {
        EstadoIncidencia.Pendiente => EstadosIncidencia.Pendiente,
        EstadoIncidencia.EnRevision => EstadosIncidencia.EnRevision,
        EstadoIncidencia.Atendida => EstadosIncidencia.Atendida,
        _ => estado.ToString()
    };

    // Convierte el texto guardado en SQLite ("En revisión") al enum.
    public static EstadoIncidencia DesdeTexto(string texto) => texto switch
    {
        EstadosIncidencia.EnRevision => EstadoIncidencia.EnRevision,
        EstadosIncidencia.Atendida => EstadoIncidencia.Atendida,
        _ => EstadoIncidencia.Pendiente
    };

    // Clase CSS que define el color del estado (ver site.css).
    public static string ClaseCss(this EstadoIncidencia estado) => estado switch
    {
        EstadoIncidencia.Pendiente => "estado--pendiente",
        EstadoIncidencia.EnRevision => "estado--revision",
        EstadoIncidencia.Atendida => "estado--atendida",
        _ => ""
    };

    // Icono que acompaña al color, para no depender solo del color.
    public static string Icono(this EstadoIncidencia estado) => estado switch
    {
        EstadoIncidencia.Pendiente => "clock",
        EstadoIncidencia.EnRevision => "search",
        EstadoIncidencia.Atendida => "check-circle",
        _ => "clock"
    };
}
