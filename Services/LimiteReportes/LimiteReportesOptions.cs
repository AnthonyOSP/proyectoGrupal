namespace proyectoGrupal.Services.LimiteReportes;

// Configuración del límite de reportes por usuario (ver Program.cs y README, sección 16).
//   REDIS_CONNECTION: conexión a Redis (opcional). Sin ella, el conteo se guarda en la memoria del servidor.
//   LIMITE_REPORTES y LIMITE_REPORTES_MINUTOS: cuántos reportes se permiten y en cuántos minutos (opcionales).
public class LimiteReportesOptions
{
    public string? RedisConnection { get; set; }

    public int MaximoReportes { get; set; } = 3;

    public int VentanaMinutos { get; set; } = 10;

    public bool UsaRedis => !string.IsNullOrWhiteSpace(RedisConnection);

    public TimeSpan Ventana => TimeSpan.FromMinutes(VentanaMinutos);
}
