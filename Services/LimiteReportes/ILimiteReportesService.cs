namespace proyectoGrupal.Services.LimiteReportes;

// Límite de reportes por usuario contra el spam (y contra el abuso de Google Vision, que cobra por imagen).
// Ejemplo: máximo 3 reportes cada 10 minutos. El conteo vive en Redis si está configurado;
// si no (o si Redis falla), en la memoria del servidor. Nunca lanza excepciones por fallos de Redis.
public interface ILimiteReportesService
{
    // true si el conteo se guarda en Redis; false si usa la memoria del servidor.
    bool UsaRedis { get; }

    // Cuántos reportes lleva el usuario en la ventana actual y si puede enviar otro.
    // Se consulta ANTES de procesar el reporte (así no se gasta Google Vision si ya llegó al límite).
    Task<EstadoLimite> ConsultarAsync(string usuarioId, CancellationToken cancellationToken = default);

    // Suma un reporte. Se llama solo DESPUÉS de guardar la incidencia: un error no le quita intentos al vecino.
    Task RegistrarAsync(string usuarioId, CancellationToken cancellationToken = default);
}

// Resultado de ConsultarAsync. Espera = cuánto falta para que se reinicie el conteo.
public record EstadoLimite(bool Permitido, int Usados, int Maximo, TimeSpan Ventana, TimeSpan Espera)
{
    // Mensaje para el vecino cuando llegó al límite.
    public string Mensaje()
    {
        var minutos = Math.Max(1, (int)Math.Ceiling(Espera.TotalMinutes));
        var cuando = minutos == 1 ? "1 minuto" : $"{minutos} minutos";
        return $"Llegaste al límite de {Maximo} reportes cada {(int)Ventana.TotalMinutes} minutos. " +
               $"Podrás enviar otro reporte en {cuando}.";
    }
}
