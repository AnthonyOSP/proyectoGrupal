namespace proyectoGrupal.Services.PieSocket;

// Canales de PieSocket usados por la aplicación. Los "private-" solo aceptan la conexión con un JWT
// firmado por nuestro servidor, que lo entrega después de comprobar permisos (RealtimeController).
// Límite de PieSocket: 1-200 caracteres, sin "/" ni "\". Los nombres de aquí están muy por debajo.
public static class CanalesRealtime
{
    // Canal PÚBLICO (sin "private-", no pide JWT): lo escucha el listado /Home/Incidencias, también sin
    // sesión. Solo lleva datos que ya se muestran en ese listado (Id, título, categoría, estado y fecha).
    // Nadie más puede publicar en él porque la mensajería entre clientes está desactivada en PieSocket.
    public const string Publico = "incidencias-publicas";

    // Indica si el canal exige JWT (lo pide el navegador a /Realtime/Autorizar antes de conectarse).
    public static bool EsPrivado(string canal) => canal.StartsWith("private-", StringComparison.Ordinal);

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
