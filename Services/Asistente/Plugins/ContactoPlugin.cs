using System.ComponentModel;
using Microsoft.SemanticKernel;
using proyectoGrupal.Helpers;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Services.Asistente.Plugins;

// Herramienta del agente: "pasar a una persona". Prepara un mensaje para el equipo del distrito y da un
// enlace al formulario de Contacto ya completado. No lo envía: el vecino lo revisa, completa su nombre y
// correo y lo envía (y el mensaje pasa por el análisis de sentimiento de siempre).
public sealed class ContactoPlugin
{
    private readonly SesionAsistente _sesion;
    private readonly LinkGenerator _enlaces;
    private readonly IHttpContextAccessor _http;

    public ContactoPlugin(SesionAsistente sesion, LinkGenerator enlaces, IHttpContextAccessor http)
    {
        _sesion = sesion;
        _enlaces = enlaces;
        _http = http;
    }

    [KernelFunction("PrepararMensajeContacto")]
    [Description("Prepara un mensaje para el equipo del distrito (no lo envía): la interfaz muestra un botón que abre Contacto ya completado. Úsala solo si el vecino acepta escribir al equipo.")]
    public string PrepararMensajeContacto(
        [Description("Mensaje en primera persona, como si lo escribiera el vecino, con su consulta o problema (10 a 1000 caracteres). Sin nombre, correo ni datos inventados: el formulario los pide aparte.")] string mensaje)
    {
        var limpio = TextoFormulario.Recortar(TextoFormulario.TextoLargo(mensaje), ContactoViewModel.MensajeMaximo);
        if ((limpio?.Length ?? 0) < ContactoViewModel.MensajeMinimo)
        {
            return "No se preparó el mensaje: es muy corto. Pregúntale al vecino qué quiere decirle al equipo.";
        }

        var contexto = _http.HttpContext;
        var url = (contexto != null ? _enlaces.GetPathByAction(contexto, "Contacto", "Home", new { mensaje = limpio }) : null)
                  ?? "/Home/Contacto";

        _sesion.PonerContacto(new BorradorContacto { Mensaje = limpio!, Url = url });

        return "Mensaje preparado: la interfaz muestra una tarjeta con el texto y el botón 'Abrir Contacto'. " +
               "Dile en una frase que lo revise, complete su nombre y correo, y pulse 'Enviar mensaje'; el equipo le responderá a su correo.";
    }
}
