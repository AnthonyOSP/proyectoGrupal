namespace proyectoGrupal.Services;

// Zona de un rostro detectado, en píxeles de la imagen enviada.
public record RegionRostro(int X, int Y, int Ancho, int Alto);

// Detecta DÓNDE hay rostros en una imagen (no identifica a las personas).
public interface IFaceDetectionService
{
    // Devuelve una lista vacía si no hay rostros.
    // Lanza FaceDetectionException si no fue posible completar la detección.
    Task<IReadOnlyList<RegionRostro>> DetectarRostrosAsync(byte[] imagen, CancellationToken cancellationToken = default);
}

// Error al comunicarse con el servicio de detección (credenciales, red, cuota, etc.).
public class FaceDetectionException : Exception
{
    public FaceDetectionException(string mensaje, Exception? interna = null) : base(mensaje, interna)
    {
    }
}
