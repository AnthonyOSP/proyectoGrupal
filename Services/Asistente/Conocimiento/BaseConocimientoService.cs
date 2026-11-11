using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace proyectoGrupal.Services.Asistente.Conocimiento;

// Base de conocimiento con Qdrant Cloud (API REST) y búsqueda local de respaldo.
// Se registra como singleton. Qdrant Cloud Inference genera los vectores: la aplicación envía el texto
// y el nombre del modelo, y Qdrant calcula el embedding al guardar y al buscar.
public class BaseConocimientoService : IBaseConocimientoService
{
    public const string NombreClienteHttp = "Qdrant";
    public const string CarpetaConocimiento = "Conocimiento";
    public const string OrigenCarpeta = "carpeta";
    public const string OrigenSubido = "subido";

    private const string NombreVector = "texto";
    private const int PuntosPorLote = 32;

    // Tiempos máximos: una búsqueda lenta no debe hacer esperar al chat.
    private static readonly TimeSpan LimiteBusqueda = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan LimiteEscritura = TimeSpan.FromMinutes(2);

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<BaseConocimientoService> _logger;
    private readonly string _carpeta;

    // Fragmentos de la carpeta Conocimiento/ para la búsqueda local (se leen una vez y al sincronizar).
    private volatile List<FragmentoConocimiento>? _fragmentosLocales;

    public BaseConocimientoService(
        IOptions<QdrantOptions> opciones,
        IHttpClientFactory httpFactory,
        IWebHostEnvironment entorno,
        ILogger<BaseConocimientoService> logger)
    {
        Opciones = opciones.Value;
        _httpFactory = httpFactory;
        _logger = logger;
        _carpeta = Path.Combine(entorno.ContentRootPath, CarpetaConocimiento);

        if (!UsaQdrant)
        {
            _logger.LogWarning(
                "Qdrant no está configurado (faltan QDRANT_ENDPOINT y/o QDRANT_API_KEY). " +
                "El asistente buscará en los archivos de {Carpeta} (búsqueda básica).", CarpetaConocimiento);
        }
    }

    public QdrantOptions Opciones { get; }

    public bool UsaQdrant => Opciones.EstaConfigurado;

    public IReadOnlyList<string> ArchivosCarpeta =>
        Directory.Exists(_carpeta)
            ? Directory.GetFiles(_carpeta, "*.md").Select(Path.GetFileName).OfType<string>().Order().ToList()
            : [];

    // ---------- Búsqueda ----------

    public async Task<BusquedaConocimiento> BuscarAsync(string consulta, int limite, CancellationToken cancellationToken = default)
    {
        consulta = consulta.Trim();
        if (consulta.Length == 0)
        {
            return new BusquedaConocimiento([], UsaQdrant ? MotorConocimiento.Qdrant : MotorConocimiento.Local);
        }

        if (UsaQdrant)
        {
            try
            {
                using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tiempo.CancelAfter(LimiteBusqueda);

                var resultados = await BuscarEnQdrantAsync(consulta, limite, tiempo.Token);
                return new BusquedaConocimiento(resultados, MotorConocimiento.Qdrant);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException
                                       && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "No se pudo consultar Qdrant; el asistente usa la búsqueda local.");
            }
        }

        return new BusquedaConocimiento(BuscarLocal(consulta, limite), MotorConocimiento.Local);
    }

    private async Task<List<ResultadoConocimiento>> BuscarEnQdrantAsync(string consulta, int limite, CancellationToken cancellationToken)
    {
        var cuerpo = new Dictionary<string, object>
        {
            // Los modelos e5 esperan el prefijo "query: " en las preguntas y "passage: " en los documentos.
            ["query"] = new { text = "query: " + consulta, model = Opciones.ModeloEmbedding },
            ["using"] = NombreVector,
            ["limit"] = limite,
            ["with_payload"] = true
        };

        using var respuesta = await Cliente().PostAsJsonAsync(
            $"collections/{Opciones.Coleccion}/points/query", cuerpo, cancellationToken);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            // La colección todavía no existe: se responde con la búsqueda local.
            throw new InvalidOperationException($"La colección '{Opciones.Coleccion}' no existe en Qdrant.");
        }

        await AsegurarExitoAsync(respuesta, "buscar en la base de conocimiento", cancellationToken);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(cancellationToken));
        var resultados = new List<ResultadoConocimiento>();

        foreach (var punto in json.RootElement.GetProperty("result").GetProperty("points").EnumerateArray())
        {
            if (!punto.TryGetProperty("payload", out var payload))
            {
                continue;
            }

            resultados.Add(new ResultadoConocimiento(
                Texto(payload, "documento"),
                Texto(payload, "seccion"),
                Texto(payload, "contenido"),
                punto.TryGetProperty("score", out var score) ? score.GetDouble() : 0));
        }

        return resultados;
    }

    // Búsqueda básica: cuenta cuántas palabras de la consulta aparecen en cada fragmento
    // (sin tildes ni mayúsculas y comparando el inicio de la palabra, así "reportar" encuentra "reporte").
    private List<ResultadoConocimiento> BuscarLocal(string consulta, int limite)
    {
        var palabras = Palabras(consulta).Distinct().ToList();
        if (palabras.Count == 0)
        {
            return [];
        }

        return FragmentosLocales()
            .Select(f =>
            {
                var enTitulo = Palabras($"{f.Documento} {f.Seccion}").ToHashSet();
                var enTexto = Palabras(f.Contenido).ToHashSet();
                double puntaje = 0;
                foreach (var palabra in palabras)
                {
                    if (enTitulo.Contains(palabra)) puntaje += 2;
                    if (enTexto.Contains(palabra)) puntaje += 1;
                }
                return (Fragmento: f, Puntaje: puntaje / (palabras.Count * 3));
            })
            .Where(r => r.Puntaje > 0)
            .OrderByDescending(r => r.Puntaje)
            .Take(limite)
            .Select(r => new ResultadoConocimiento(r.Fragmento.Documento, r.Fragmento.Seccion, r.Fragmento.Contenido, r.Puntaje))
            .ToList();
    }

    private static readonly HashSet<string> PalabrasVacias = new(StringComparer.Ordinal)
    {
        "como", "cual", "cuales", "cuando", "donde", "para", "porque", "pued", "puedo", "puede", "quiero", "tengo",
        "esta", "este", "esto", "estos", "esas", "eso", "las", "los", "del", "una", "uno", "unos", "que", "por",
        "con", "sin", "mis", "tus", "sus", "hay", "ser", "hace", "hacer", "sobre", "pagina", "alerta", "vecinal"
    };

    // Palabras normalizadas y recortadas a sus primeras 5 letras (una "raíz" sencilla).
    private static IEnumerable<string> Palabras(string texto)
    {
        var sinTildes = new StringBuilder();
        foreach (var c in texto.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sinTildes.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }
        }

        return sinTildes.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length >= 3 && !PalabrasVacias.Contains(p))
            .Select(p => p.Length > 5 ? p[..5] : p);
    }

    private List<FragmentoConocimiento> FragmentosLocales()
    {
        if (_fragmentosLocales is { } listos)
        {
            return listos;
        }

        var fragmentos = new List<FragmentoConocimiento>();
        foreach (var (archivo, texto) in LeerCarpeta())
        {
            fragmentos.AddRange(FragmentadorMarkdown.Fragmentar(texto, Path.GetFileNameWithoutExtension(archivo)));
        }

        _fragmentosLocales = fragmentos;
        return fragmentos;
    }

    private List<(string Archivo, string Texto)> LeerCarpeta()
    {
        var archivos = new List<(string, string)>();
        foreach (var nombre in ArchivosCarpeta)
        {
            try
            {
                archivos.Add((nombre, File.ReadAllText(Path.Combine(_carpeta, nombre), Encoding.UTF8)));
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "No se pudo leer el documento de conocimiento {Archivo}.", nombre);
            }
        }
        return archivos;
    }

    // ---------- Carga de documentos ----------

    public async Task<ResultadoOperacionConocimiento> SincronizarCarpetaAsync(CancellationToken cancellationToken = default)
    {
        // Vuelve a leer la carpeta también para la búsqueda local.
        _fragmentosLocales = null;
        var archivos = LeerCarpeta();

        if (!UsaQdrant)
        {
            var total = FragmentosLocales().Count;
            return new(false, $"Qdrant no está configurado. El asistente usa la búsqueda local con {archivos.Count} documento(s) y {total} sección(es) de la carpeta {CarpetaConocimiento}/.");
        }

        if (archivos.Count == 0)
        {
            return new(false, $"La carpeta {CarpetaConocimiento}/ no tiene archivos .md.");
        }

        try
        {
            using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tiempo.CancelAfter(LimiteEscritura);

            await AsegurarColeccionAsync(tiempo.Token);

            // Se borran los documentos de la carpeta y se vuelven a cargar: así un archivo
            // borrado o renombrado no deja fragmentos viejos, y repetir no crea duplicados.
            await EliminarPorFiltroAsync("origen", OrigenCarpeta, tiempo.Token);

            var fragmentos = 0;
            foreach (var (archivo, texto) in archivos)
            {
                fragmentos += await CargarAsync($"{OrigenCarpeta}:{archivo}", archivo, texto, OrigenCarpeta, tiempo.Token);
            }

            _logger.LogInformation("Qdrant | Carpeta {Carpeta}/ sincronizada: {Documentos} documentos, {Fragmentos} fragmentos.",
                CarpetaConocimiento, archivos.Count, fragmentos);
            return new(true, $"Base de conocimiento sincronizada: {archivos.Count} documento(s) y {fragmentos} fragmento(s) cargados en Qdrant.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
        {
            _logger.LogError(ex, "Qdrant | No se pudo sincronizar la carpeta {Carpeta}/.", CarpetaConocimiento);
            return new(false, "No se pudo sincronizar con Qdrant. Revisa la configuración y el log del servidor.");
        }
    }

    public async Task SincronizarSiEstaVaciaAsync(CancellationToken cancellationToken = default)
    {
        if (!UsaQdrant)
        {
            return;
        }

        try
        {
            using var respuesta = await Cliente().PostAsJsonAsync(
                $"collections/{Opciones.Coleccion}/points/count", new { exact = true }, cancellationToken);

            if (respuesta.IsSuccessStatusCode)
            {
                using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(cancellationToken));
                if (json.RootElement.GetProperty("result").GetProperty("count").GetInt64() > 0)
                {
                    return;
                }
            }
            else if (respuesta.StatusCode != HttpStatusCode.NotFound)
            {
                await AsegurarExitoAsync(respuesta, "contar los fragmentos de la colección", cancellationToken);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Qdrant | No se pudo revisar la colección al iniciar.");
            return;
        }

        _logger.LogInformation("Qdrant | La colección '{Coleccion}' está vacía: se cargan los documentos de {Carpeta}/.",
            Opciones.Coleccion, CarpetaConocimiento);
        var resultado = await SincronizarCarpetaAsync(cancellationToken);
        _logger.LogInformation("Qdrant | {Mensaje}", resultado.Mensaje);
    }

    public async Task<ResultadoOperacionConocimiento> CargarDocumentoAsync(string nombreArchivo, string texto, CancellationToken cancellationToken = default)
    {
        if (!UsaQdrant)
        {
            return new(false, "Qdrant no está configurado: no se pueden subir documentos. Agrega el archivo a la carpeta Conocimiento/ del proyecto.");
        }

        try
        {
            using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tiempo.CancelAfter(LimiteEscritura);

            await AsegurarColeccionAsync(tiempo.Token);
            var fragmentos = await CargarAsync($"{OrigenSubido}:{Guid.NewGuid():N}", nombreArchivo, texto, OrigenSubido, tiempo.Token);

            return fragmentos == 0
                ? new(false, "El documento no tiene texto para cargar.")
                : new(true, $"'{nombreArchivo}' cargado en Qdrant: {fragmentos} fragmento(s).");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
        {
            _logger.LogError(ex, "Qdrant | No se pudo cargar el documento {Archivo}.", nombreArchivo);
            return new(false, "No se pudo cargar el documento en Qdrant. Revisa la configuración y el log del servidor.");
        }
    }

    // Fragmenta el texto y lo guarda en Qdrant. Devuelve cuántos fragmentos se guardaron.
    private async Task<int> CargarAsync(string documentoId, string archivo, string texto, string origen, CancellationToken cancellationToken)
    {
        var fragmentos = FragmentadorMarkdown.Fragmentar(texto, Path.GetFileNameWithoutExtension(archivo));
        if (fragmentos.Count == 0)
        {
            return 0;
        }

        var fecha = DateTime.UtcNow;
        var puntos = fragmentos.Select((f, indice) => new
        {
            id = Guid.NewGuid().ToString(),
            vector = new Dictionary<string, object>
            {
                [NombreVector] = new { text = "passage: " + f.TextoCompleto, model = Opciones.ModeloEmbedding }
            },
            payload = new
            {
                documentoId,
                documento = f.Documento,
                archivo,
                seccion = f.Seccion,
                contenido = f.Contenido,
                origen,
                fragmento = indice,
                caracteres = f.Contenido.Length,
                fechaCarga = fecha
            }
        });

        foreach (var lote in puntos.Chunk(PuntosPorLote))
        {
            using var respuesta = await Cliente().PutAsJsonAsync(
                $"collections/{Opciones.Coleccion}/points?wait=true", new { points = lote }, cancellationToken);
            await AsegurarExitoAsync(respuesta, $"cargar '{archivo}'", cancellationToken);
        }

        return fragmentos.Count;
    }

    // ---------- Listado y eliminación ----------

    public async Task<IReadOnlyList<DocumentoConocimiento>> ListarDocumentosAsync(CancellationToken cancellationToken = default)
    {
        if (!UsaQdrant)
        {
            return [];
        }

        var documentos = new Dictionary<string, DocumentoConocimiento>(StringComparer.Ordinal);
        try
        {
            object? offset = null;
            do
            {
                var cuerpo = new Dictionary<string, object>
                {
                    ["limit"] = 256,
                    ["with_payload"] = new[] { "documentoId", "documento", "archivo", "origen", "caracteres", "fechaCarga" },
                    ["with_vector"] = false
                };
                if (offset != null)
                {
                    cuerpo["offset"] = offset;
                }

                using var respuesta = await Cliente().PostAsJsonAsync(
                    $"collections/{Opciones.Coleccion}/points/scroll", cuerpo, cancellationToken);
                if (respuesta.StatusCode == HttpStatusCode.NotFound)
                {
                    return [];
                }
                await AsegurarExitoAsync(respuesta, "listar los documentos", cancellationToken);

                using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(cancellationToken));
                var resultado = json.RootElement.GetProperty("result");

                foreach (var punto in resultado.GetProperty("points").EnumerateArray())
                {
                    if (!punto.TryGetProperty("payload", out var payload) || Texto(payload, "documentoId") is not { Length: > 0 } id)
                    {
                        continue;
                    }

                    if (!documentos.TryGetValue(id, out var documento))
                    {
                        documento = new DocumentoConocimiento
                        {
                            Id = id,
                            Nombre = Texto(payload, "documento"),
                            Archivo = Texto(payload, "archivo"),
                            Origen = Texto(payload, "origen"),
                            FechaCarga = payload.TryGetProperty("fechaCarga", out var f) && f.TryGetDateTime(out var fecha)
                                ? fecha.ToLocalTime()
                                : default
                        };
                        documentos[id] = documento;
                    }

                    documento.Fragmentos++;
                    documento.Caracteres += payload.TryGetProperty("caracteres", out var c) && c.TryGetInt32(out var n) ? n : 0;
                }

                // El offset de la página siguiente puede ser un texto (UUID) o un número.
                offset = resultado.TryGetProperty("next_page_offset", out var siguiente) && siguiente.ValueKind != JsonValueKind.Null
                    ? siguiente.Clone()
                    : null;
            }
            while (offset != null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Qdrant | No se pudieron listar los documentos.");
            throw new InvalidOperationException("No se pudieron listar los documentos de Qdrant. Revisa la configuración y el log del servidor.", ex);
        }

        return documentos.Values
            .OrderBy(d => d.Origen)
            .ThenBy(d => d.Archivo, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ResultadoOperacionConocimiento> EliminarDocumentoAsync(string documentoId, CancellationToken cancellationToken = default)
    {
        if (!UsaQdrant)
        {
            return new(false, "Qdrant no está configurado.");
        }

        try
        {
            await EliminarPorFiltroAsync("documentoId", documentoId, cancellationToken);
            return new(true, "Documento eliminado de la base de conocimiento.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogError(ex, "Qdrant | No se pudo eliminar el documento {DocumentoId}.", documentoId);
            return new(false, "No se pudo eliminar el documento. Revisa el log del servidor.");
        }
    }

    // ---------- Colección ----------

    private async Task AsegurarColeccionAsync(CancellationToken cancellationToken)
    {
        using (var existe = await Cliente().GetAsync($"collections/{Opciones.Coleccion}", cancellationToken))
        {
            if (existe.IsSuccessStatusCode)
            {
                return;
            }
            if (existe.StatusCode != HttpStatusCode.NotFound)
            {
                await AsegurarExitoAsync(existe, "revisar la colección", cancellationToken);
            }
        }

        var coleccion = new
        {
            vectors = new Dictionary<string, object>
            {
                [NombreVector] = new { size = Opciones.Dimensiones, distance = "Cosine" }
            }
        };
        using (var creada = await Cliente().PutAsJsonAsync($"collections/{Opciones.Coleccion}", coleccion, cancellationToken))
        {
            await AsegurarExitoAsync(creada, "crear la colección", cancellationToken);
        }

        // Índices para borrar por documento o por origen (Qdrant Cloud los exige para filtrar).
        foreach (var campo in new[] { "documentoId", "origen" })
        {
            using var indice = await Cliente().PutAsJsonAsync(
                $"collections/{Opciones.Coleccion}/index?wait=true",
                new { field_name = campo, field_schema = "keyword" }, cancellationToken);
            await AsegurarExitoAsync(indice, $"crear el índice de '{campo}'", cancellationToken);
        }

        _logger.LogInformation("Qdrant | Colección '{Coleccion}' creada ({Modelo}, {Dimensiones} dimensiones).",
            Opciones.Coleccion, Opciones.ModeloEmbedding, Opciones.Dimensiones);
    }

    private async Task EliminarPorFiltroAsync(string campo, string valor, CancellationToken cancellationToken)
    {
        var filtro = new { filter = new { must = new[] { new { key = campo, match = new { value = valor } } } } };
        using var respuesta = await Cliente().PostAsJsonAsync(
            $"collections/{Opciones.Coleccion}/points/delete?wait=true", filtro, cancellationToken);

        if (respuesta.StatusCode != HttpStatusCode.NotFound)
        {
            await AsegurarExitoAsync(respuesta, "eliminar fragmentos", cancellationToken);
        }
    }

    // ---------- Utilidades ----------

    private HttpClient Cliente()
    {
        var cliente = _httpFactory.CreateClient(NombreClienteHttp);
        cliente.BaseAddress = new Uri(Opciones.Endpoint!.Trim().TrimEnd('/') + "/");
        cliente.DefaultRequestHeaders.Remove("api-key");
        cliente.DefaultRequestHeaders.Add("api-key", Opciones.ApiKey!.Trim());
        return cliente;
    }

    private async Task AsegurarExitoAsync(HttpResponseMessage respuesta, string accion, CancellationToken cancellationToken)
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

    private static string Texto(JsonElement payload, string propiedad) =>
        payload.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString() ?? string.Empty
            : string.Empty;
}
