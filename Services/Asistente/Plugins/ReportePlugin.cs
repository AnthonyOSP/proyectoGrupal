using System.ComponentModel;
using Microsoft.SemanticKernel;
using proyectoGrupal.Helpers;
using proyectoGrupal.Models;
using proyectoGrupal.Services.Categorias;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Services.Asistente.Plugins;

// Herramienta del agente: prepara un reporte a partir de la conversación.
// NO crea la incidencia: genera un enlace al formulario /Home/Reportar ya completado. Así la foto,
// el pixelado de rostros, el límite de reportes y todas las validaciones siguen pasando por el
// formulario de siempre, y el vecino revisa y envía el reporte él mismo.
// También avisa si ya hay incidencias abiertas parecidas en el mismo lugar (en la misma llamada:
// así el modelo no tiene que encadenar dos herramientas, lo que gasta más tokens y falla más).
public sealed class ReportePlugin
{
    // La misma confianza mínima que usa el formulario para sugerir la categoría.
    private const float ConfianzaMinima = 0.40f;

    private readonly ICategoriaSugeridaService _clasificador;
    private readonly IncidenciasPlugin _incidencias;
    private readonly SesionAsistente _sesion;
    private readonly LinkGenerator _enlaces;
    private readonly IHttpContextAccessor _http;

    public ReportePlugin(
        ICategoriaSugeridaService clasificador,
        IncidenciasPlugin incidencias,
        SesionAsistente sesion,
        LinkGenerator enlaces,
        IHttpContextAccessor http)
    {
        _clasificador = clasificador;
        _incidencias = incidencias;
        _sesion = sesion;
        _enlaces = enlaces;
        _http = http;
    }

    [KernelFunction("PrepararReporte")]
    [Description(
        "Prepara el reporte (no lo envía): la interfaz muestra un botón que abre el formulario ya completado. " +
        "También avisa si ya hay reportes parecidos en el mismo lugar. Úsala apenas sepas qué pasa y dónde.")]
    public async Task<string> PrepararReporteAsync(
        [Description("Título corto del problema, 5 a 100 caracteres.")] string titulo,
        [Description("Descripción con lo que contó el vecino (qué pasa, desde cuándo, a quién afecta), en primera persona, 15 a 1000 caracteres.")] string descripcion,
        [Description("Dónde ocurre: dirección, cruce o referencia.")] string ubicacion,
        [Description("Categoría que elijas tú (no la preguntes): Pistas y veredas (baches, veredas), Alumbrado público (postes, luz), Parques y áreas verdes, Limpieza pública (basura, desmonte), Agua y desagüe (fugas, buzones), Tránsito y señalización (semáforos, señales), Seguridad (zonas inseguras) u Otros.")] string? categoria = null,
        CancellationToken cancellationToken = default)
    {
        var tituloLimpio = TextoFormulario.Recortar(TextoFormulario.UnaLinea(titulo), CrearIncidenciaViewModel.TituloMaximo);
        var descripcionLimpia = TextoFormulario.Recortar(TextoFormulario.TextoLargo(descripcion), CrearIncidenciaViewModel.DescripcionMaximo);
        var ubicacionLimpia = TextoFormulario.Recortar(TextoFormulario.UnaLinea(ubicacion), CrearIncidenciaViewModel.UbicacionMaximo);

        // Los mismos mínimos del formulario: si falta algo, el modelo se lo pide al vecino.
        var faltan = new List<string>();
        if ((tituloLimpio?.Length ?? 0) < CrearIncidenciaViewModel.TituloMinimo)
            faltan.Add($"un título de al menos {CrearIncidenciaViewModel.TituloMinimo} caracteres");
        if ((descripcionLimpia?.Length ?? 0) < CrearIncidenciaViewModel.DescripcionMinimo)
            faltan.Add($"una descripción de al menos {CrearIncidenciaViewModel.DescripcionMinimo} caracteres");
        if ((ubicacionLimpia?.Length ?? 0) < CrearIncidenciaViewModel.UbicacionMinimo)
            faltan.Add("la ubicación (dirección, cruce de calles o una referencia)");

        if (faltan.Count > 0)
        {
            return $"No se preparó el reporte. Falta: {string.Join("; ", faltan)}. Pídeselo al vecino en una frase.";
        }

        // Categoría: la que propuso el modelo de lenguaje (entiende toda la conversación); si no dio una válida,
        // la del modelo de ML.NET del formulario; si tampoco, la elige el vecino. El formulario respeta la categoría
        // que llega marcada (reportar.js la trata como elegida a mano y solo muestra su propia sugerencia).
        var elegida = IncidenciasPlugin.BuscarCategoria(categoria) ?? SugerirCategoria($"{tituloLimpio}. {descripcionLimpia}");

        var contexto = _http.HttpContext;
        var valores = new { titulo = tituloLimpio, descripcion = descripcionLimpia, ubicacion = ubicacionLimpia, categoria = elegida?.Slug };
        var url = (contexto != null ? _enlaces.GetPathByAction(contexto, "Reportar", "Home", valores) : null)
                  ?? _enlaces.GetPathByAction("Reportar", "Home", valores)
                  ?? "/Home/Reportar";

        var requiereSesion = contexto?.User.Identity?.IsAuthenticated != true;

        _sesion.PonerBorrador(new BorradorReporte
        {
            Titulo = tituloLimpio!,
            Descripcion = descripcionLimpia!,
            Ubicacion = ubicacionLimpia!,
            Categoria = elegida?.Nombre,
            Url = url,
            RequiereSesion = requiereSesion
        });

        var parecidas = await _incidencias.BuscarParecidasAsync(ubicacionLimpia!, tituloLimpio!, elegida?.Nombre, cancellationToken);

        return "Reporte preparado: la interfaz muestra una tarjeta con los datos y el botón 'Abrir formulario'. " +
               (elegida == null ? "Sin categoría: el vecino la elige en el formulario. " : $"Categoría: {elegida.Nombre}. ") +
               (requiereSesion ? "No ha iniciado sesión: el formulario le pedirá entrar (los datos se conservan). " : "") +
               (parecidas.Length > 0
                   ? parecidas + "\nDile que revise esas tarjetas: si es el mismo problema no hace falta reportarlo otra vez. "
                   : "No hay reportes abiertos parecidos en ese lugar. ") +
               "Responde en 1 a 3 frases: que revise los datos, puede marcar el punto en el mapa y agregar una foto, y pulse 'Enviar reporte'. No repitas la ficha.";
    }

    private CategoriaViewModel? SugerirCategoria(string texto)
    {
        if (_clasificador.Sugerir(texto) is { } sugerencia &&
            sugerencia.Confianza >= ConfianzaMinima &&
            CategoriasIncidencia.Todas.Contains(sugerencia.Categoria))
        {
            return CatalogoCategorias.PorNombre(sugerencia.Categoria);
        }

        return null;
    }
}
