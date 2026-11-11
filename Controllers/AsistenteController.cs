using System.ClientModel;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.SemanticKernel;
using proyectoGrupal.Services.Asistente;

namespace proyectoGrupal.Controllers;

public class PreguntaAsistente
{
    public string? Mensaje { get; set; }
}

// Asistente virtual (chat). Disponible para cualquier visitante, con o sin sesión.
//   - El historial de la conversación se guarda en la sesión del servidor (no lo envía el navegador),
//     así nadie puede inyectar mensajes falsos del asistente.
//   - Límite de mensajes por persona (política "asistente" en Program.cs) contra el abuso de la cuota de Groq.
//   - La respuesta se transmite en streaming como texto plano (ver ProtocoloAsistente).
public class AsistenteController : Controller
{
    public const string PoliticaLimite = "asistente";

    private const string ClaveHistorial = "Asistente.Historial";
    private const int MaximoMensajesHistorial = 8;
    private const int MaximoCaracteresPregunta = 1000;

    private readonly IAsistenteService _asistente;
    private readonly ILogger<AsistenteController> _logger;

    public AsistenteController(IAsistenteService asistente, ILogger<AsistenteController> logger)
    {
        _asistente = asistente;
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

        var historial = LeerHistorial();

        // La cookie de sesión solo puede enviarse en las cabeceras, que salen con el primer fragmento:
        // la sesión se crea y se guarda ANTES de empezar el streaming.
        GuardarHistorial(historial);
        await HttpContext.Session.CommitAsync(cancellationToken);

        var respuesta = new StringBuilder();
        try
        {
            await foreach (var fragmento in _asistente.ResponderAsync(historial, pregunta, cancellationToken))
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
        if (texto.Length > 0)
        {
            historial.Add(new MensajeAsistente { Rol = "user", Texto = pregunta });
            historial.Add(new MensajeAsistente { Rol = "assistant", Texto = texto });
            GuardarHistorial(historial);

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
    }

    // POST: /Asistente/Limpiar  ("Nueva conversación")
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Limpiar()
    {
        HttpContext.Session.Remove(ClaveHistorial);
        return NoContent();
    }

    private List<MensajeAsistente> LeerHistorial()
    {
        var json = HttpContext.Session.GetString(ClaveHistorial);
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<MensajeAsistente>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void GuardarHistorial(List<MensajeAsistente> historial)
    {
        // Solo los últimos mensajes: suficiente contexto sin gastar demasiados tokens.
        var recientes = historial.Count > MaximoMensajesHistorial
            ? historial.Skip(historial.Count - MaximoMensajesHistorial).ToList()
            : historial;
        HttpContext.Session.SetString(ClaveHistorial, JsonSerializer.Serialize(recientes));
    }
}
