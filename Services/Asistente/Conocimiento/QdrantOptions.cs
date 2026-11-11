namespace proyectoGrupal.Services.Asistente.Conocimiento;

// Configuración de la base de conocimiento en Qdrant Cloud (ver Program.cs y README, sección 17).
//   QDRANT_ENDPOINT:   URL del clúster, por ejemplo https://xxxx.us-east-1-1.aws.cloud.qdrant.io
//   QDRANT_API_KEY:    clave de la API del clúster.
//   QDRANT_COLLECTION: nombre de la colección (opcional).
// Los vectores (embeddings) los genera Qdrant Cloud Inference con un modelo multilingüe:
// la aplicación solo envía el texto, así que no hace falta otro proveedor de embeddings.
// Sin Endpoint o ApiKey, el asistente busca en los archivos de la carpeta Conocimiento/ (búsqueda básica).
public class QdrantOptions
{
    public string? Endpoint { get; set; }

    public string? ApiKey { get; set; }

    public string Coleccion { get; set; } = "alerta_vecinal_conocimiento";

    // Colección con las incidencias (búsqueda por significado y detección de reportes parecidos).
    // Opcional: QDRANT_COLLECTION_INCIDENCIAS.
    public string ColeccionIncidencias { get; set; } = "alerta_vecinal_incidencias";

    // Modelo de embeddings de Qdrant Cloud Inference. multilingual-e5-small entiende bien el español
    // y genera vectores de 384 dimensiones. Si se cambia el modelo, hay que cambiar Dimensiones
    // y usar una colección nueva (los vectores de modelos distintos no se pueden mezclar).
    public string ModeloEmbedding { get; set; } = "intfloat/multilingual-e5-small";

    public int Dimensiones { get; set; } = 384;

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ApiKey);
}
