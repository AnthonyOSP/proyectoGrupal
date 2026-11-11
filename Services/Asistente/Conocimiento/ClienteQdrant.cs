using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace proyectoGrupal.Services.Asistente.Conocimiento;

// Lo común para hablar con Qdrant Cloud por su API REST: el cliente HTTP con la clave, la revisión de
// errores y la creación de colecciones. Lo usan la base de conocimiento y el índice semántico de incidencias.
// Los vectores los genera Qdrant Cloud Inference: la aplicación envía el texto y el nombre del modelo.
public class ClienteQdrant
{
    public const string NombreClienteHttp = "Qdrant";

    // Nombre del vector dentro de cada punto (las colecciones usan un vector con nombre).
    public const string NombreVector = "texto";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ClienteQdrant> _logger;

    public ClienteQdrant(IOptions<QdrantOptions> opciones, IHttpClientFactory httpFactory, ILogger<ClienteQdrant> logger)
    {
        Opciones = opciones.Value;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public QdrantOptions Opciones { get; }

    public bool EstaConfigurado => Opciones.EstaConfigurado;

    public HttpClient Crear()
    {
        var cliente = _httpFactory.CreateClient(NombreClienteHttp);
        cliente.BaseAddress = new Uri(Opciones.Endpoint!.Trim().TrimEnd('/') + "/");
        cliente.DefaultRequestHeaders.Remove("api-key");
        cliente.DefaultRequestHeaders.Add("api-key", Opciones.ApiKey!.Trim());
        return cliente;
    }

    // Texto para que Qdrant genere el vector. Los modelos e5 esperan "passage: " en lo que se guarda
    // y "query: " en lo que se busca.
    public object Documento(string texto) => new { text = texto, model = Opciones.ModeloEmbedding };

    // Crea la colección (y sus índices para filtrar) si no existe. Devuelve true si la creó.
    public async Task<bool> AsegurarColeccionAsync(string coleccion, IEnumerable<string> camposIndice, CancellationToken cancellationToken)
    {
        using (var existe = await Crear().GetAsync($"collections/{coleccion}", cancellationToken))
        {
            if (existe.IsSuccessStatusCode)
            {
                return false;
            }
            if (existe.StatusCode != HttpStatusCode.NotFound)
            {
                await AsegurarExitoAsync(existe, "revisar la colección", cancellationToken);
            }
        }

        var cuerpo = new
        {
            vectors = new Dictionary<string, object>
            {
                [NombreVector] = new { size = Opciones.Dimensiones, distance = "Cosine" }
            }
        };
        using (var creada = await Crear().PutAsJsonAsync($"collections/{coleccion}", cuerpo, cancellationToken))
        {
            await AsegurarExitoAsync(creada, "crear la colección", cancellationToken);
        }

        // Índices para filtrar por esos campos (Qdrant Cloud los exige para filtrar).
        foreach (var campo in camposIndice)
        {
            using var indice = await Crear().PutAsJsonAsync(
                $"collections/{coleccion}/index?wait=true",
                new { field_name = campo, field_schema = "keyword" }, cancellationToken);
            await AsegurarExitoAsync(indice, $"crear el índice de '{campo}'", cancellationToken);
        }

        _logger.LogInformation("Qdrant | Colección '{Coleccion}' creada ({Modelo}, {Dimensiones} dimensiones).",
            coleccion, Opciones.ModeloEmbedding, Opciones.Dimensiones);
        return true;
    }

    public async Task AsegurarExitoAsync(HttpResponseMessage respuesta, string accion, CancellationToken cancellationToken)
    {
        if (respuesta.IsSuccessStatusCode)
        {
            return;
        }

        var detalle = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        if (detalle.Length > 500)
        {
            detalle = detalle[..500];
        }

        _logger.LogError("Qdrant | Error al {Accion} ({Codigo}): {Detalle}", accion, (int)respuesta.StatusCode, detalle);
        throw new InvalidOperationException($"Qdrant respondió {(int)respuesta.StatusCode} al {accion}.");
    }
}
