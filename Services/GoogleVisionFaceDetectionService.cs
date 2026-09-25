using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Cloud.Vision.V1;

namespace proyectoGrupal.Services;

// Detección de rostros con Google Cloud Vision (Face Detection).
// Credenciales: se leen de la variable de entorno GOOGLE_APPLICATION_CREDENTIALS
// (ver README.md). Nunca se escriben en el código ni en appsettings.json.
public class GoogleVisionFaceDetectionService : IFaceDetectionService
{
    private static readonly TimeSpan TiempoMaximo = TimeSpan.FromSeconds(20);

    private readonly ILogger<GoogleVisionFaceDetectionService> _logger;

    // El cliente se crea la primera vez que se usa, así la aplicación arranca
    // aunque las credenciales todavía no estén configuradas.
    private ImageAnnotatorClient? _cliente;

    public GoogleVisionFaceDetectionService(ILogger<GoogleVisionFaceDetectionService> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<RegionRostro>> DetectarRostrosAsync(byte[] imagen, CancellationToken cancellationToken = default)
    {
        try
        {
            _cliente ??= await ImageAnnotatorClient.CreateAsync(cancellationToken);

            var opciones = CallSettings.FromExpiration(Expiration.FromTimeout(TiempoMaximo))
                .WithCancellationToken(cancellationToken);

            // La imagen viaja solo en memoria; Vision no la guarda como parte de esta petición.
            var rostros = await _cliente.DetectFacesAsync(Image.FromBytes(imagen), maxResults: 50, callSettings: opciones);

            // BoundingPoly cubre la cabeza completa (más amplio que FdBoundingPoly, que solo marca la piel).
            return rostros
                .Select(r => r.BoundingPoly.Vertices)
                .Where(v => v.Count > 0)
                .Select(v =>
                {
                    int minX = v.Min(p => p.X), maxX = v.Max(p => p.X);
                    int minY = v.Min(p => p.Y), maxY = v.Max(p => p.Y);
                    return new RegionRostro(minX, minY, maxX - minX, maxY - minY);
                })
                .Where(r => r.Ancho > 0 && r.Alto > 0)
                .ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Credenciales ausentes, API deshabilitada, sin conexión, tiempo agotado, cuota, etc.
            _logger.LogError(ex, "No se pudo completar la detección de rostros con Google Cloud Vision");
            throw new FaceDetectionException("No se pudo completar la detección de rostros.", ex);
        }
    }
}
