using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using proyectoGrupal.Models;

namespace proyectoGrupal.Services.PieSocket;

// Implementación según la documentación oficial de PieSocket (protocolo V4):
//   - Publicar desde el servidor: POST https://CLUSTER_ID.piesocket.com/api/v4/publish
//     con { key, secret, roomId, message }.
//   - Canales "private-": el navegador se conecta con un JWT HS256 firmado con el API secret,
//     con payload { sub: canal, iat, exp }.
// Se registra como singleton (la pausa tras fallos se comparte en toda la aplicación).
public partial class PieSocketRealtimeService : IPieSocketRealtimeService
{
    public const string NombreClienteHttp = "PieSocket";

    // Duración del JWT: solo sirve para conectarse; al reconectar, el navegador pide uno nuevo.
    private static readonly TimeSpan DuracionJwt = TimeSpan.FromMinutes(10);

    // Pausa tras un fallo: no se vuelve a llamar a PieSocket durante este tiempo,
    // para que un PieSocket caído no haga esperar cada reporte o cambio de estado.
    private static readonly TimeSpan Pausa = TimeSpan.FromSeconds(30);

    private readonly PieSocketOptions _opciones;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PieSocketRealtimeService> _logger;
    private readonly Uri? _urlPublicar;
    private long _pausaHastaTicks;

    public PieSocketRealtimeService(
        IOptions<PieSocketOptions> opciones, IHttpClientFactory httpFactory,
        IWebHostEnvironment entorno, ILogger<PieSocketRealtimeService> logger)
    {
        _opciones = opciones.Value;
        _httpFactory = httpFactory;
        _logger = logger;

        if (!_opciones.EstaConfigurado)
        {
            _logger.LogWarning(
                "PieSocket no está configurado (faltan PIESOCKET_API_KEY, PIESOCKET_API_SECRET y/o PIESOCKET_CLUSTER_ID). " +
                "La aplicación funciona sin actualización en tiempo real.");
            return;
        }

        // Servidor de pruebas local (solo en Development). En producción siempre se usa PieSocket.
        if (entorno.IsDevelopment() && Uri.TryCreate(_opciones.HostPruebas, UriKind.Absolute, out var pruebas))
        {
            _urlPublicar = new Uri(pruebas, "/api/v4/publish");
            UrlWebSocket = $"{(pruebas.Scheme == "https" ? "wss" : "ws")}://{pruebas.Authority}/v4";
            _logger.LogWarning("PieSocket usa el servidor de pruebas {Host} (solo Development).", pruebas.Authority);
        }
        else if (FormatoCluster().IsMatch(_opciones.ClusterId!))
        {
            _urlPublicar = new Uri($"https://{_opciones.ClusterId}.piesocket.com/api/v4/publish");
            UrlWebSocket = $"wss://{_opciones.ClusterId}.piesocket.com/v4";
        }
        else
        {
            // Evita armar URLs con un valor inesperado. No se muestra el valor (podría ser un secreto mal copiado).
            _logger.LogError("PIESOCKET_CLUSTER_ID no tiene un formato válido: el tiempo real queda desactivado.");
            return;
        }

        ApiKeyPublica = _opciones.ApiKey;
        _logger.LogInformation("PieSocket configurado (protocolo V4).");
    }

    public bool EstaConfigurado => _urlPublicar != null;

    public string? UrlWebSocket { get; }

    public string? ApiKeyPublica { get; }

    public string CrearJwt(string canal)
    {
        if (!EstaConfigurado)
        {
            throw new InvalidOperationException("PieSocket no está configurado.");
        }

        var ahora = DateTimeOffset.UtcNow;
        var encabezado = new { alg = "HS256", typ = "JWT" };
        var datos = new { sub = canal, iat = ahora.ToUnixTimeSeconds(), exp = ahora.Add(DuracionJwt).ToUnixTimeSeconds() };

        var sinFirma = $"{Base64Url(JsonSerializer.SerializeToUtf8Bytes(encabezado))}.{Base64Url(JsonSerializer.SerializeToUtf8Bytes(datos))}";
        var firma = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_opciones.ApiSecret!), Encoding.UTF8.GetBytes(sinFirma));
        return $"{sinFirma}.{Base64Url(firma)}";
    }

    public Task<bool> NotificarIncidenciaCreadaAsync(Incidencia incidencia, CancellationToken cancellationToken = default)
    {
        var evento = new IncidenciaCreadaEvento(
            incidencia.Id, incidencia.Titulo, incidencia.Categoria, incidencia.Estado, FormatoFecha(incidencia.FechaRegistro));

        return PublicarAsync(CanalesRealtime.Administracion, EventosRealtime.IncidenciaCreada, evento, incidencia.Id, cancellationToken);
    }

    public async Task<bool> NotificarEstadoActualizadoAsync(
        Incidencia incidencia, string estadoAnterior, DateTime fecha, CancellationToken cancellationToken = default)
    {
        var evento = new EstadoActualizadoEvento(incidencia.Id, estadoAnterior, incidencia.Estado, FormatoFecha(fecha));

        // Al canal de la incidencia (seguimiento del ciudadano y del administrador) y al de administración (listados).
        var alSeguimiento = await PublicarAsync(
            CanalesRealtime.Incidencia(incidencia.Id), EventosRealtime.EstadoActualizado, evento, incidencia.Id, cancellationToken);
        var alPanel = await PublicarAsync(
            CanalesRealtime.Administracion, EventosRealtime.EstadoActualizado, evento, incidencia.Id, cancellationToken);
        return alSeguimiento && alPanel;
    }

    private async Task<bool> PublicarAsync(string canal, string nombreEvento, object datos, int incidenciaId, CancellationToken cancellationToken)
    {
        if (!EstaConfigurado)
        {
            return false;
        }

        if (DateTime.UtcNow.Ticks < Interlocked.Read(ref _pausaHastaTicks))
        {
            _logger.LogWarning(
                "Incidencia {Id} guardada correctamente, pero no se publicó el evento {Evento}: PieSocket en pausa tras un fallo reciente.",
                incidenciaId, nombreEvento);
            return false;
        }

        // Cuerpo según la documentación oficial. Contiene el secret: nunca se escribe en el log.
        var cuerpo = new
        {
            key = _opciones.ApiKey,
            secret = _opciones.ApiSecret,
            roomId = canal,
            message = new MensajeRealtime(nombreEvento, datos)
        };

        try
        {
            var cliente = _httpFactory.CreateClient(NombreClienteHttp);
            using var respuesta = await cliente.PostAsJsonAsync(_urlPublicar, cuerpo, cancellationToken);

            if (respuesta.IsSuccessStatusCode)
            {
                return true;
            }

            // Solo el código HTTP: el cuerpo de la respuesta no se registra.
            RegistrarFallo(incidenciaId, nombreEvento, canal, $"HTTP {(int)respuesta.StatusCode}");
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            // Tipo de error, sin el mensaje completo (podría incluir la URL o detalles internos).
            RegistrarFallo(incidenciaId, nombreEvento, canal, ex is TaskCanceledException ? "tiempo de espera agotado" : ex.GetType().Name);
            return false;
        }
    }

    private void RegistrarFallo(int incidenciaId, string nombreEvento, string canal, string motivo)
    {
        Interlocked.Exchange(ref _pausaHastaTicks, DateTime.UtcNow.Add(Pausa).Ticks);
        _logger.LogWarning(
            "Incidencia {Id} guardada correctamente, pero no se pudo publicar el evento {Evento} en el canal {Canal}: {Motivo}.",
            incidenciaId, nombreEvento, canal, motivo);
    }

    private static string FormatoFecha(DateTime fecha) => fecha.ToString("yyyy-MM-ddTHH:mm:ss");

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // Id de cluster de PieSocket: letras, números, puntos y guiones (ej. "s12345.nyc1" o "demo").
    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,62}$", RegexOptions.IgnoreCase)]
    private static partial Regex FormatoCluster();
}
