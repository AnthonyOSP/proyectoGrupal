namespace proyectoGrupal.Services.PieSocket;

// Configuración de PieSocket (tiempo real). Se lee de variables de entorno o User Secrets (ver Program.cs y README):
//   PIESOCKET_API_KEY, PIESOCKET_API_SECRET y PIESOCKET_CLUSTER_ID.
// La API key es pública por diseño (el navegador la necesita para conectarse);
// el API secret firma los JWT y publica eventos: solo lo usa el servidor.
public class PieSocketOptions
{
    public string? ApiKey { get; set; }

    // Nunca se envía al navegador ni se escribe en el log.
    public string? ApiSecret { get; set; }

    // Ej: "s12345.nyc1" → wss://s12345.nyc1.piesocket.com
    public string? ClusterId { get; set; }

    // Solo en Development: dirección de un servidor que imita a PieSocket (pruebas locales).
    // En producción se ignora y se usan los servidores de PieSocket.
    public string? HostPruebas { get; set; }

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ApiSecret) &&
        !string.IsNullOrWhiteSpace(ClusterId);
}
