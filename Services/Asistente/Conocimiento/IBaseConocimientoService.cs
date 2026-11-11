namespace proyectoGrupal.Services.Asistente.Conocimiento;

// Qué resolvió una búsqueda en la base de conocimiento.
public enum MotorConocimiento
{
    Qdrant,     // búsqueda semántica (por significado) en Qdrant Cloud
    Local       // búsqueda básica por palabras en los archivos de Conocimiento/
}

public record ResultadoConocimiento(string Documento, string Seccion, string Contenido, double Puntaje);

public record BusquedaConocimiento(IReadOnlyList<ResultadoConocimiento> Resultados, MotorConocimiento Motor);

// Documento cargado en Qdrant (agrupa sus fragmentos).
public class DocumentoConocimiento
{
    public string Id { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Archivo { get; set; } = string.Empty;

    // "carpeta" (archivos de Conocimiento/, se reemplazan al sincronizar) o "subido" (desde el panel).
    public string Origen { get; set; } = string.Empty;

    public int Fragmentos { get; set; }

    public int Caracteres { get; set; }

    public DateTime FechaCarga { get; set; }
}

public record ResultadoOperacionConocimiento(bool Exito, string Mensaje);

// Base de conocimiento del asistente: los documentos de ayuda de Alerta Vecinal.
//   - Con Qdrant configurado: los fragmentos se guardan en Qdrant y se buscan por significado.
//   - Sin Qdrant (o si falla): se buscan por palabras en los archivos de la carpeta Conocimiento/.
// Nunca lanza excepciones por fallos de Qdrant: devuelve "sin éxito" y deja el detalle en el log.
public interface IBaseConocimientoService
{
    bool UsaQdrant { get; }

    QdrantOptions Opciones { get; }

    // Archivos .md de la carpeta Conocimiento/ (los que carga "Sincronizar carpeta").
    IReadOnlyList<string> ArchivosCarpeta { get; }

    Task<BusquedaConocimiento> BuscarAsync(string consulta, int limite, CancellationToken cancellationToken = default);

    // Reemplaza en Qdrant todos los documentos de la carpeta Conocimiento/ (se puede repetir sin duplicar).
    Task<ResultadoOperacionConocimiento> SincronizarCarpetaAsync(CancellationToken cancellationToken = default);

    // Sincroniza la carpeta solo si la colección no existe o está vacía (primer arranque).
    Task SincronizarSiEstaVaciaAsync(CancellationToken cancellationToken = default);

    // Agrega un documento subido desde el panel (Markdown o texto).
    Task<ResultadoOperacionConocimiento> CargarDocumentoAsync(string nombreArchivo, string texto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentoConocimiento>> ListarDocumentosAsync(CancellationToken cancellationToken = default);

    Task<ResultadoOperacionConocimiento> EliminarDocumentoAsync(string documentoId, CancellationToken cancellationToken = default);
}
