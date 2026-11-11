using System.ClientModel;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using proyectoGrupal.Data;
using proyectoGrupal.Models;
using proyectoGrupal.Services.Asistente;

namespace proyectoGrupal.Controllers;

public class PreguntaAsistente
{
    public string? Mensaje { get; set; }

    // Ruta de la página en la que está el vecino (location.pathname). Solo se usan rutas conocidas.
    public string? Pagina { get; set; }
}

public class ValoracionPedido
{
    public string? Id { get; set; }

    public bool Util { get; set; }
}

// Asistente virtual (chat). Disponible para cualquier visitante, con o sin sesión.
//   - El historial de la conversación se guarda en la sesión del servidor (no lo envía el navegador),
//     así nadie puede inyectar mensajes falsos del asistente.
//   - Límite de mensajes por persona (política "asistente" en Program.cs) contra el abuso de la cuota de Groq.
//   - La respuesta se transmite en streaming como texto plano (ver ProtocoloAsistente).
//   - Cada respuesta tiene un Id: el vecino la puede valorar (👍/👎) y el panel muestra las que no sirvieron.
public class AsistenteController : Controller
{
    public const string PoliticaLimite = "asistente";

    private const string ClaveHistorial = "Asistente.Historial";
    private const string ClaveRespuestas = "Asistente.Respuestas";
    private const int MaximoMensajesHistorial = 8;
    private const int MaximoRespuestasValorables = 10;
    private const int MaximoCaracteresPregunta = 1000;

    private readonly IAsistenteService _asistente;
    private readonly SesionAsistente _sesion;
    private readonly ILogger<AsistenteController> _logger;

    public AsistenteController(IAsistenteService asistente, SesionAsistente sesion, ILogger<AsistenteController> logger)
    {
        _asistente = asistente;
        _sesion = sesion;
        _logger = logger;
    }

    // POST: /Asistente/Preguntar   (lo llama asistente.js con el token antiforgery en la cabecera)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(PoliticaLimite)]
    public async Task Preguntar([FromBody] PreguntaAsistente? peticion, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/plain; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-store";
        // Evita que un proxy junte la respuesta antes de enviarla (el streaming se vería de golpe).
        Response.Headers["X-Accel-Buffering"] = "no";

        if (!_asistente.EstaDisponible)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await Response.WriteAsync("El asistente no está disponible en este momento.", cancellationToken);
            return;
        }

        var pregunta = Helpers.TextoFormulario.TextoLargo(peticion?.Mensaje) ?? string.Empty;
        if (pregunta.Length == 0)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("Escribe tu pregunta.", cancellationToken);
            return;
        }
        if (pregunta.Length > MaximoCaracteresPregunta)
        {
            pregunta = pregunta[..MaximoCaracteresPregunta];
        }

        var historial = Leer<MensajeAsistente>(ClaveHistorial);

        // Para la valoración: si la pregunta es muy corta ("sí, por favor"), se guarda junto con la anterior.
        var preguntaAnterior = historial.LastOrDefault(m => m.Rol == "user")?.Texto;
        var preguntaConContexto = pregunta.Length < 40 && preguntaAnterior != null ? $"{preguntaAnterior} → {pregunta}" : pregunta;

        // La cookie de sesión solo puede enviarse en las cabeceras, que salen con el primer fragmento:
        // la sesión se crea y se guarda ANTES de empezar el streaming.
        Guardar(ClaveHistorial, Ultimos(historial, MaximoMensajesHistorial));
        await HttpContext.Session.CommitAsync(cancellationToken);

        var respuesta = new StringBuilder();
        try
        {
            await foreach (var fragmento in _asistente.ResponderAsync(historial, pregunta, peticion?.Pagina, cancellationToken))
            {
                respuesta.Append(fragmento);
                await Response.WriteAsync(fragmento, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return; // el vecino cerró la página o canceló la pregunta
        }
        catch (Exception ex)
        {
            var (log, mensaje) = ex switch
            {
                HttpOperationException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } or ClientResultException { Status: 429 }
                    => ("Groq respondió 429 (límite de uso del modelo).", "El asistente está recibiendo muchas preguntas. Espera un minuto y vuelve a intentarlo."),
                HttpOperationException { StatusCode: System.Net.HttpStatusCode.Unauthorized } or ClientResultException { Status: 401 }
                    => ("Groq rechazó la clave (401). Revisa GROQ_API_KEY.", "El asistente no está disponible en este momento."),
                _ => ("Error al responder en el asistente.", "Lo siento, ocurrió un error al generar la respuesta. Inténtalo nuevamente.")
            };
            _logger.LogError(ex, "Asistente | {Detalle}", log);

            // Si ya se envió parte de la respuesta, se agrega el aviso al final.
            await Response.WriteAsync((respuesta.Length > 0 ? "\n\n" : "") + mensaje, CancellationToken.None);
            return;
        }

        var texto = ProtocoloAsistente.Limpiar(respuesta.ToString()).Trim();
        if (texto.Length == 0)
        {
            return;
        }

        historial.Add(new MensajeAsistente { Rol = "user", Texto = pregunta });
        historial.Add(new MensajeAsistente { Rol = "assistant", Texto = texto });
        Guardar(ClaveHistorial, Ultimos(historial, MaximoMensajesHistorial));

        // Copia de la pregunta y la respuesta para poder valorarla después (el navegador solo envía el Id).
        var valorables = Leer<RespuestaValorable>(ClaveRespuestas);
        valorables.Add(new RespuestaValorable(_sesion.RespuestaId, preguntaConContexto, texto, string.Join(" · ", _sesion.Fuentes)));
        Guardar(ClaveRespuestas, Ultimos(valorables, MaximoRespuestasValorables));

        try
        {
            await HttpContext.Session.CommitAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            // La respuesta ya se envió: perder el historial no debe cortar el chat.
            _logger.LogWarning(ex, "Asistente | No se pudo guardar el historial en la sesión.");
        }
    }

    // POST: /Asistente/Valorar   {"id": "...", "util": true}
    // Guarda (o cambia) el 👍/👎 de una respuesta de ESTA sesión. La pregunta y la respuesta salen de la
    // sesión del servidor, no del navegador: así nadie puede llenar la tabla con textos inventados.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(PoliticaLimite)]
    public async Task<IActionResult> Valorar([FromBody] ValoracionPedido? pedido, [FromServices] ApplicationDbContext context)
    {
        var original = Leer<RespuestaValorable>(ClaveRespuestas).FirstOrDefault(r => r.Id == pedido?.Id);
        if (original == null)
        {
            return NotFound();
        }

        var valoracion = await context.ValoracionesAsistente.FirstOrDefaultAsync(v => v.RespuestaId == original.Id);
        if (valoracion == null)
        {
            valoracion = new ValoracionAsistente
            {
                RespuestaId = original.Id,
                Pregunta = Recortar(original.Pregunta, ValoracionAsistente.PreguntaMaximo),
                Respuesta = Recortar(original.Respuesta, ValoracionAsistente.RespuestaMaximo),
                Fuentes = Recortar(original.Fuentes, ValoracionAsistente.FuentesMaximo)
            };
            context.ValoracionesAsistente.Add(valoracion);
        }

        valoracion.Util = pedido!.Util;
        valoracion.Fecha = DateTime.Now;

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Asistente | No se pudo guardar la valoración {Id}.", original.Id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return NoContent();
    }

    // POST: /Asistente/Limpiar  ("Nueva conversación")
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Limpiar()
    {
        HttpContext.Session.Remove(ClaveHistorial);
        return NoContent();
    }

    private record RespuestaValorable(string Id, string Pregunta, string Respuesta, string Fuentes);

    private List<T> Leer<T>(string clave)
    {
        var json = HttpContext.Session.GetString(clave);
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Guardar<T>(string clave, List<T> lista) =>
        HttpContext.Session.SetString(clave, JsonSerializer.Serialize(lista));

    // Solo los últimos elementos: suficiente contexto sin gastar demasiados tokens ni memoria.
    private static List<T> Ultimos<T>(List<T> lista, int maximo) =>
        lista.Count > maximo ? lista.Skip(lista.Count - maximo).ToList() : lista;

    private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}
