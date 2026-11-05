using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace proyectoGrupal.Services.LimiteReportes;

// Implementación con StackExchange.Redis y respaldo en memoria.
// Se registra como singleton: una sola conexión a Redis reutilizada por toda la aplicación.
//
// Cómo cuenta (ventana fija por usuario): el primer reporte crea la clave
// "alertavecinal:limite-reportes:<usuarioId>" con valor 1 y una caducidad de N minutos; cada reporte
// siguiente suma 1. Cuando la clave caduca, el conteo vuelve a cero. Redis borra la clave solo.
public class LimiteReportesService : ILimiteReportesService
{
    private const string PrefijoClave = "alertavecinal:limite-reportes:";

    // INCR y PEXPIRE en un solo paso (un script Lua es atómico en Redis): la clave nunca queda sin caducidad.
    private const string ScriptSumar = """
        local usados = redis.call('INCR', KEYS[1])
        if usados == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return usados
        """;

    private readonly LimiteReportesOptions _opciones;
    private readonly ILogger<LimiteReportesService> _logger;
    private readonly ConnectionMultiplexer? _redis;

    // Respaldo cuando Redis no está configurado o no responde. Solo vale para esta instancia del servidor.
    private readonly Dictionary<string, (int Usados, DateTime Vence)> _memoria = new();
    private readonly object _candado = new();

    public LimiteReportesService(IOptions<LimiteReportesOptions> opciones, ILogger<LimiteReportesService> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;

        if (!_opciones.UsaRedis)
        {
            _logger.LogInformation(
                "Redis no está configurado (REDIS_CONNECTION). El límite de reportes ({Maximo} cada {Minutos} min) usará la memoria del servidor.",
                _opciones.MaximoReportes, _opciones.VentanaMinutos);
            return;
        }

        try
        {
            var configuracion = CrearConfiguracion(_opciones.RedisConnection!);
            // No detiene la aplicación si Redis no está disponible al iniciar: se reconecta solo.
            configuracion.AbortOnConnectFail = false;
            configuracion.ConnectTimeout = 3000;
            configuracion.SyncTimeout = 2000;
            configuracion.AsyncTimeout = 2000;
            configuracion.ClientName = "alerta-vecinal";

            _redis = ConnectionMultiplexer.Connect(configuracion);

            // Solo el servidor, nunca la contraseña.
            _logger.LogInformation(
                "Redis configurado ({Servidor}, conectado: {Conectado}). Límite de reportes: {Maximo} cada {Minutos} min.",
                string.Join(", ", configuracion.EndPoints), _redis.IsConnected, _opciones.MaximoReportes, _opciones.VentanaMinutos);
        }
        catch (Exception ex) when (ex is RedisException or ArgumentException or FormatException)
        {
            _logger.LogError("REDIS_CONNECTION no es válida ({Tipo}): el límite de reportes usará la memoria del servidor.", ex.GetType().Name);
        }
    }

    public bool UsaRedis => _redis != null;

    public async Task<EstadoLimite> ConsultarAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        if (_redis != null)
        {
            try
            {
                var db = _redis.GetDatabase();
                var clave = (RedisKey)(PrefijoClave + usuarioId);
                var valor = db.StringGetAsync(clave);
                var vida = db.KeyTimeToLiveAsync(clave);
                await Task.WhenAll(valor, vida);

                var usados = valor.Result.HasValue ? (int)valor.Result : 0;
                return Estado(usados, vida.Result ?? TimeSpan.Zero);
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                _logger.LogWarning("Redis no respondió al consultar el límite ({Tipo}): se usa la memoria del servidor.", ex.GetType().Name);
            }
        }

        lock (_candado)
        {
            if (_memoria.TryGetValue(usuarioId, out var registro) && registro.Vence > DateTime.UtcNow)
            {
                return Estado(registro.Usados, registro.Vence - DateTime.UtcNow);
            }

            return Estado(0, TimeSpan.Zero);
        }
    }

    public async Task RegistrarAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        if (_redis != null)
        {
            try
            {
                await _redis.GetDatabase().ScriptEvaluateAsync(
                    ScriptSumar,
                    new RedisKey[] { PrefijoClave + usuarioId },
                    new RedisValue[] { (long)_opciones.Ventana.TotalMilliseconds });
                return;
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                _logger.LogWarning("Redis no respondió al registrar un reporte ({Tipo}): se usa la memoria del servidor.", ex.GetType().Name);
            }
        }

        lock (_candado)
        {
            var ahora = DateTime.UtcNow;

            // Limpieza de usuarios cuya ventana ya terminó (el diccionario no crece sin fin).
            foreach (var vencido in _memoria.Where(r => r.Value.Vence <= ahora).Select(r => r.Key).ToList())
            {
                _memoria.Remove(vencido);
            }

            _memoria[usuarioId] = _memoria.TryGetValue(usuarioId, out var registro)
                ? (registro.Usados + 1, registro.Vence)
                : (1, ahora + _opciones.Ventana);
        }
    }

    private EstadoLimite Estado(int usados, TimeSpan espera) =>
        new(usados < _opciones.MaximoReportes, usados, _opciones.MaximoReportes, _opciones.Ventana, espera);

    // Acepta el formato de StackExchange.Redis ("host:6379,password=...,ssl=True")
    // y también una URL "redis://" o "rediss://" (con TLS), como la que entregan Upstash o Redis Cloud.
    private static ConfigurationOptions CrearConfiguracion(string conexion)
    {
        conexion = conexion.Trim();
        if (!conexion.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) &&
            !conexion.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            return ConfigurationOptions.Parse(conexion);
        }

        var url = new Uri(conexion);
        var configuracion = new ConfigurationOptions
        {
            Ssl = url.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)
        };
        configuracion.EndPoints.Add(url.Host, url.Port > 0 ? url.Port : 6379);

        if (!string.IsNullOrEmpty(url.UserInfo))
        {
            var partes = url.UserInfo.Split(':', 2);
            var usuario = Uri.UnescapeDataString(partes[0]);
            if (partes.Length == 2)
            {
                configuracion.Password = Uri.UnescapeDataString(partes[1]);
                if (!string.IsNullOrEmpty(usuario) && usuario != "default")
                {
                    configuracion.User = usuario;
                }
            }
            else
            {
                configuracion.Password = usuario;
            }
        }

        return configuracion;
    }
}
