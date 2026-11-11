using proyectoGrupal.Models;
using proyectoGrupal.Services.Sentimiento;

namespace proyectoGrupal.ViewModels;

// Bandeja de mensajes de contacto del panel administrativo (/Admin/Mensajes):
// reporte de sentimientos del día elegido + la lista de mensajes de ese día.
public class MensajesAdminViewModel
{
    // Día del reporte (por defecto, hoy).
    public DateOnly Fecha { get; set; }
    public bool EsHoy => Fecha == DateOnly.FromDateTime(DateTime.Now);

    // true si se abrió sin fecha y hoy no hay mensajes: se muestra el último día que tiene mensajes.
    public bool UltimoDiaConMensajes { get; set; }

    // Filtros de la lista (ya validados), o null si no se usan.
    public string? Buscar { get; set; }
    public string? Sentimiento { get; set; }
    public bool HayFiltros => Buscar != null || Sentimiento != null;

    // Reporte del día: se cuenta sobre TODOS los mensajes del día, sin los filtros de la lista.
    public int TotalDia { get; set; }
    public int PositivosDia { get; set; }
    public int NegativosDia { get; set; }
    public int SinAnalizarDia => TotalDia - PositivosDia - NegativosDia;

    // false si el modelo de ML.NET no se cargó (los mensajes nuevos se guardan sin sentimiento).
    public bool ModeloDisponible { get; set; }

    // Datos del último entrenamiento del modelo (null si no se conocen).
    public InfoEntrenamiento? InfoModelo { get; set; }

    // Mensajes (de cualquier día) cuyo sentimiento fijó el administrador: se usan al reentrenar.
    public int Correcciones { get; set; }

    // Mensajes del día que pasan los filtros, del más reciente al más antiguo.
    public List<MensajeContacto> Mensajes { get; set; } = new();

    // Porcentaje respecto al total del día (para la barra de distribución).
    public int Porcentaje(int cantidad) => TotalDia == 0 ? 0 : (int)Math.Round(cantidad * 100.0 / TotalDia);
}
