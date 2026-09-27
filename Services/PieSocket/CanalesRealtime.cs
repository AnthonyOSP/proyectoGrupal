namespace proyectoGrupal.Services.PieSocket;

// Canales de PieSocket usados por la aplicación. Son "private-": PieSocket solo acepta la conexión con
// un JWT firmado por nuestro servidor, que lo entrega después de comprobar permisos (RealtimeController).
// Límite de PieSocket: 1-200 caracteres, sin "/" ni "\". Los nombres de aquí están muy por debajo.
public static class CanalesRealtime
{
    // Nuevas incidencias y cambios de estado: solo administradores.
    public const string Administracion = "private-admin-incidencias";

    private const string PrefijoIncidencia = "private-incidencia-";

    // Cambios de estado de una incidencia: su dueño o un administrador.
    public static string Incidencia(int incidenciaId) => PrefijoIncidencia + incidenciaId;

    // Reconoce "private-incidencia-{id}" y devuelve el id. Cualquier otro formato se rechaza.
    public static bool EsCanalDeIncidencia(string? canal, out int incidenciaId)
    {
        incidenciaId = 0;
        return canal != null
            && canal.StartsWith(PrefijoIncidencia, StringComparison.Ordinal)
            && int.TryParse(canal.AsSpan(PrefijoIncidencia.Length), System.Globalization.NumberStyles.None, null, out incidenciaId)
            && incidenciaId > 0
            && canal == Incidencia(incidenciaId);
    }
}
