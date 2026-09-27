using System.Globalization;
using proyectoGrupal.Models;

namespace proyectoGrupal.ViewModels;

// Datos de solo lectura de la página "Seguimiento" (ciudadano y administrador).
// No se usa para recibir formularios: el historial solo lo escribe el servidor.
public class SeguimientoViewModel
{
    public IncidenciaViewModel Incidencia { get; set; } = new();

    // Registros ordenados del más antiguo al más reciente.
    public List<HistorialEstadoViewModel> Historial { get; set; } = new();

    // true solo en el panel administrativo: muestra el correo de quien hizo cada cambio.
    public bool MostrarUsuarios { get; set; }

    // Fecha del último registro del historial (null si la incidencia no tiene historial).
    public DateTime? UltimaActualizacion => Historial.Count > 0 ? Historial[^1].Fecha : null;

    // Incidencias creadas antes de la Etapa 8: su historial no empieza con el registro de creación.
    public bool HistorialIncompleto => Historial.Count > 0 && !Historial[0].EsCreacion;

    // Convierte la incidencia (con su Historial cargado) en el ViewModel.
    // Para leer el correo, la consulta debe incluir también Historial.Usuario (solo en el panel administrativo).
    public static SeguimientoViewModel DesdeEntidad(Incidencia incidencia, bool mostrarUsuarios) => new()
    {
        Incidencia = IncidenciaViewModel.DesdeEntidad(incidencia),
        MostrarUsuarios = mostrarUsuarios,
        Historial = incidencia.Historial
            .OrderBy(h => h.FechaCambio)
            .ThenBy(h => h.Id)
            .Select(h => new HistorialEstadoViewModel
            {
                EstadoAnterior = h.EstadoAnterior == null ? null : EstadoIncidenciaExtensions.DesdeTexto(h.EstadoAnterior),
                EstadoNuevo = EstadoIncidenciaExtensions.DesdeTexto(h.EstadoNuevo),
                Fecha = h.FechaCambio,
                RealizadoPor = mostrarUsuarios ? h.Usuario?.Email ?? "Cuenta eliminada" : null
            })
            .ToList()
    };
}

// Un registro de la línea de tiempo.
public class HistorialEstadoViewModel
{
    private static readonly CultureInfo Peru = new("es-PE");

    public EstadoIncidencia? EstadoAnterior { get; set; }

    public EstadoIncidencia EstadoNuevo { get; set; }

    public DateTime Fecha { get; set; }

    // Correo de quien hizo el cambio. Solo se llena para administradores; para el ciudadano es null.
    public string? RealizadoPor { get; set; }

    // El primer registro (sin estado anterior) corresponde a la creación del reporte.
    public bool EsCreacion => EstadoAnterior == null;

    // Ej: "27/09/2026 10:30"
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy HH:mm", Peru);
}
