namespace proyectoGrupal.Services.Asistente.Conocimiento;

// Al iniciar la aplicación, si Qdrant está configurado y la colección está vacía (por ejemplo, el primer
// despliegue en Render), carga los documentos de la carpeta Conocimiento/. Corre en segundo plano:
// la página no espera a Qdrant para empezar a responder. Después, los cambios se cargan con el botón
// "Sincronizar carpeta" del panel.
public class SincronizacionInicialConocimiento : BackgroundService
{
    private readonly IBaseConocimientoService _conocimiento;

    public SincronizacionInicialConocimiento(IBaseConocimientoService conocimiento)
    {
        _conocimiento = conocimiento;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _conocimiento.SincronizarSiEstaVaciaAsync(stoppingToken);
}
