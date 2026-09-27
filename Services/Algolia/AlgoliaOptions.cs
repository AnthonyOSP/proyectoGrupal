namespace proyectoGrupal.Services.Algolia;

// Configuración de Algolia. Se lee de variables de entorno o User Secrets (ver Program.cs y README):
//   ALGOLIA_APPLICATION_ID, ALGOLIA_ADMIN_API_KEY y ALGOLIA_INDEX_NAME (opcional).
// Las claves nunca se escriben en el código, en appsettings.json ni en las vistas.
public class AlgoliaOptions
{
    public const string IndicePorDefecto = "alerta_vecinal_incidencias";

    public string? ApplicationId { get; set; }

    // Clave con permisos de escritura. Solo la usa el servidor; nunca se envía al navegador.
    public string? AdminApiKey { get; set; }

    public string IndexName { get; set; } = IndicePorDefecto;

    // Solo en Development: dirección de un servidor que imita a Algolia (pruebas locales sin internet).
    // En producción se ignora y se usan los servidores oficiales de Algolia.
    public string? HostPruebas { get; set; }

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(ApplicationId) && !string.IsNullOrWhiteSpace(AdminApiKey);
}
