using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace proyectoGrupal.Services;

// Resultado de procesar la foto de una incidencia.
public record ResultadoFoto(bool Exito, string? Url, string? Error, int RostrosPixelados)
{
    public static ResultadoFoto Ok(string url, int rostros) => new(true, url, null, rostros);
    public static ResultadoFoto Fallo(string error) => new(false, null, error, 0);
}

// Protege y guarda la fotografía de una incidencia.
// Principio de privacidad: la foto ORIGINAL nunca se guarda. Solo se escribe en disco
// la versión procesada (rostros pixelados, sin metadatos como la ubicación GPS).
public class FotoIncidenciaService
{
    public const long TamanoMaximo = 5 * 1024 * 1024; // 5 MB
    public const string CarpetaRelativa = "/uploads/incidencias/";

    private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png" };
    private const int LadoMaximo = 2048;               // se reduce si la foto es más grande
    private const long PixelesMaximos = 50_000_000;    // evita imágenes gigantes (ej. 10000 x 5000)

    private readonly IFaceDetectionService _detector;
    private readonly IWebHostEnvironment _entorno;
    private readonly ILogger<FotoIncidenciaService> _logger;

    public FotoIncidenciaService(IFaceDetectionService detector, IWebHostEnvironment entorno, ILogger<FotoIncidenciaService> logger)
    {
        _detector = detector;
        _entorno = entorno;
        _logger = logger;
    }

    // Validación rápida (tamaño y extensión) antes de procesar. Devuelve null si es válida.
    public static string? ValidarArchivo(IFormFile foto)
    {
        if (foto.Length == 0)
        {
            return "El archivo de la fotografía está vacío.";
        }

        if (foto.Length > TamanoMaximo)
        {
            return "La fotografía pesa más de 5 MB. Elige una más liviana.";
        }

        var extension = Path.GetExtension(foto.FileName).ToLowerInvariant();
        if (!ExtensionesPermitidas.Contains(extension))
        {
            return "Solo se permiten fotografías JPG o PNG.";
        }

        return null;
    }

    public async Task<ResultadoFoto> ProcesarYGuardarAsync(IFormFile foto, CancellationToken cancellationToken = default)
    {
        var error = ValidarArchivo(foto);
        if (error != null)
        {
            return ResultadoFoto.Fallo(error);
        }

        // 1. Leer la foto en memoria (no se escribe en ninguna carpeta del proyecto).
        byte[] original;
        try
        {
            using var memoria = new MemoryStream();
            await foto.CopyToAsync(memoria, cancellationToken);
            original = memoria.ToArray();
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "No se pudo leer la fotografía enviada");
            return ResultadoFoto.Fallo("No pudimos leer la fotografía. Inténtalo nuevamente.");
        }

        // 2. Comprobar el contenido real (no solo la extensión) y abrir la imagen.
        Image imagen;
        IImageFormat formato;
        try
        {
            formato = Image.DetectFormat(original);
            if (formato is not JpegFormat && formato is not PngFormat)
            {
                return ResultadoFoto.Fallo("El archivo no es una imagen JPG o PNG válida.");
            }

            var info = Image.Identify(original);
            if ((long)info.Width * info.Height > PixelesMaximos)
            {
                return ResultadoFoto.Fallo("La fotografía tiene una resolución demasiado grande.");
            }

            imagen = Image.Load(original);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Archivo de imagen inválido: {Nombre}", foto.FileName);
            return ResultadoFoto.Fallo("El archivo no es una imagen JPG o PNG válida.");
        }

        using (imagen)
        {
            // 3. Normalizar: orientación correcta, tamaño razonable y sin metadatos (GPS, cámara, etc.).
            //    Vision recibe exactamente esta versión, así sus coordenadas coinciden con nuestros píxeles.
            Normalizar(imagen);
            IImageEncoder codificador = formato is PngFormat
                ? new PngEncoder()
                : new JpegEncoder { Quality = 85 };
            var extension = formato is PngFormat ? ".png" : ".jpg";

            byte[] normalizada;
            using (var memoria = new MemoryStream())
            {
                await imagen.SaveAsync(memoria, codificador, cancellationToken);
                normalizada = memoria.ToArray();
            }

            // 4. Detectar rostros con Google Cloud Vision.
            //    Si falla, NO se guarda nada: nunca queremos almacenar una foto sin proteger.
            IReadOnlyList<RegionRostro> rostros;
            try
            {
                rostros = await _detector.DetectarRostrosAsync(normalizada, cancellationToken);
            }
            catch (FaceDetectionException)
            {
                return ResultadoFoto.Fallo(
                    "No fue posible procesar la fotografía para proteger la privacidad. " +
                    "Inténtalo nuevamente en unos minutos o envía el reporte sin foto.");
            }

            // 5. Pixelar localmente cada rostro detectado.
            int pixelados;
            try
            {
                pixelados = PixeladoRostros.Pixelar(imagen, rostros);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al pixelar la fotografía");
                return ResultadoFoto.Fallo("No pudimos procesar la fotografía. Inténtalo nuevamente.");
            }

            // 6. Guardar SOLO la versión protegida, con un nombre aleatorio (nunca el nombre del usuario).
            var nombre = $"{Guid.NewGuid():N}{extension}";
            var carpeta = Path.Combine(_entorno.WebRootPath, "uploads", "incidencias");
            try
            {
                Directory.CreateDirectory(carpeta);
                await using var archivo = new FileStream(Path.Combine(carpeta, nombre), FileMode.CreateNew, FileAccess.Write);
                await imagen.SaveAsync(archivo, codificador, cancellationToken);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "No se pudo guardar la fotografía protegida");
                return ResultadoFoto.Fallo("No pudimos guardar la fotografía. Inténtalo nuevamente.");
            }

            _logger.LogInformation("Fotografía protegida guardada: {Nombre} ({Rostros} rostro(s) pixelado(s))", nombre, pixelados);
            return ResultadoFoto.Ok(CarpetaRelativa + nombre, pixelados);
        }
    }

    // Borra una foto guardada (por ejemplo, si después falla el guardado de la incidencia).
    public void Eliminar(string url)
    {
        // Solo se aceptan rutas generadas por este servicio.
        if (!url.StartsWith(CarpetaRelativa))
        {
            return;
        }

        var nombre = Path.GetFileName(url);
        var ruta = Path.Combine(_entorno.WebRootPath, "uploads", "incidencias", nombre);
        try
        {
            File.Delete(ruta);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar la fotografía {Nombre}", nombre);
        }
    }

    private static void Normalizar(Image imagen)
    {
        imagen.Mutate(ctx =>
        {
            ctx.AutoOrient();

            if (imagen.Width > LadoMaximo || imagen.Height > LadoMaximo)
            {
                ctx.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(LadoMaximo, LadoMaximo) });
            }
        });

        imagen.Metadata.ExifProfile = null;
        imagen.Metadata.IptcProfile = null;
        imagen.Metadata.XmpProfile = null;
    }
}
