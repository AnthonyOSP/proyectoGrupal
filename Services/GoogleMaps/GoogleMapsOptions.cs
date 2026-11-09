namespace proyectoGrupal.Services.GoogleMaps;

// Configuración de Google Maps (mapas, autocompletado de direcciones y dirección de un punto).
// Se lee de variables de entorno o User Secrets (ver Program.cs y README):
//   GOOGLE_MAPS_API_KEY (obligatoria para ver mapas) y GOOGLE_MAPS_MAP_ID (opcional).
// La API key es pública por diseño (el navegador la necesita para cargar el mapa): se protege
// restringiéndola en Google Cloud a los dominios de la app y a las APIs de Maps que usa.
public class GoogleMapsOptions
{
    // ID de mapa de prueba que da Google: permite marcadores avanzados sin crear un Map ID propio.
    public const string MapIdPruebas = "DEMO_MAP_ID";

    public string? ApiKey { get; set; }

    public string MapId { get; set; } = MapIdPruebas;

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(ApiKey);
}
