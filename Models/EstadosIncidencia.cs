namespace proyectoGrupal.Models;

// Valores permitidos para Incidencia.Estado.
// Se usan constantes para no escribir el texto a mano en cada lugar.
public static class EstadosIncidencia
{
    public const string Pendiente = "Pendiente";
    public const string EnRevision = "En revisión";
    public const string Atendida = "Atendida";

    public static readonly string[] Todos = { Pendiente, EnRevision, Atendida };
}
