using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Data;

namespace proyectoGrupal.Services.Asistente.Conocimiento;

// Al iniciar la aplicación, si Qdrant está configurado:
//   - si la colección de conocimiento está vacía (por ejemplo, el primer despliegue en Render), carga los
//     documentos de la carpeta Conocimiento/;
//   - si el índice de incidencias está vacío, carga las incidencias de SQLite.
// Corre en segundo plano: la página no espera a Qdrant para empezar a responder. Después, los cambios se
// cargan con los botones "Sincronizar" del panel (y las incidencias se actualizan solas al crearlas o cambiarlas).
public class SincronizacionInicialConocimiento : BackgroundService
{
    private readonly IBaseConocimientoService _conocimiento;
    private readonly IIncidenciasSemanticasService _incidencias;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SincronizacionInicialConocimiento> _logger;

    public SincronizacionInicialConocimiento(
        IBaseConocimientoService conocimiento,
        IIncidenciasSemanticasService incidencias,
        IServiceScopeFactory scopes,
        ILogger<SincronizacionInicialConocimiento> logger)
    {
        _conocimiento = conocimiento;
        _incidencias = incidencias;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _conocimiento.SincronizarSiEstaVaciaAsync(stoppingToken);

        if (await _incidencias.ContarAsync(stoppingToken) == 0)
        {
            using var scope = _scopes.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var todas = await context.Incidencias.AsNoTracking().ToListAsync(stoppingToken);
            if (todas.Count > 0)
            {
                var resultado = await _incidencias.SincronizarTodoAsync(todas, stoppingToken);
                _logger.LogInformation("Qdrant | {Mensaje}", resultado.Mensaje);
            }
        }
    }
}
