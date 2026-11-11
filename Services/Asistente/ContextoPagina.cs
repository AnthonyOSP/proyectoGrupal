using System.Text.RegularExpressions;

namespace proyectoGrupal.Services.Asistente;

// Describe para el modelo en qué página está el vecino, a partir de la ruta que envía asistente.js
// (location.pathname). Solo se reconocen rutas conocidas: el texto de la ruta nunca llega tal cual al modelo.
public static partial class ContextoPagina
{
    [GeneratedRegex(@"^/Home/(Detalle|Seguimiento)/(\d{1,9})/?$", RegexOptions.IgnoreCase)]
    private static partial Regex ConNumero();

    private static readonly Dictionary<string, string> Paginas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/"] = "la página de inicio",
        ["/Home"] = "la página de inicio",
        ["/Home/Index"] = "la página de inicio",
        ["/Home/Reportar"] = "el formulario para reportar una incidencia (título, categoría, descripción, ubicación, mapa y foto)",
        ["/Home/Incidencias"] = "el listado público de incidencias, con buscador y filtros",
        ["/Home/MisIncidencias"] = "\"Mis incidencias\", la lista de sus propios reportes",
        ["/Home/Contacto"] = "el formulario de Contacto",
        ["/Account/Login"] = "la página para iniciar sesión",
        ["/Account/Register"] = "la página para crear una cuenta",
        ["/Account/Profile"] = "\"Mi perfil\" (datos de la cuenta y cambio de contraseña)"
    };

    // null si la ruta no es una página conocida.
    public static string? Describir(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta) || ruta.Length > 100)
        {
            return null;
        }

        ruta = ruta.Trim();
        if (ConNumero().Match(ruta) is { Success: true } m)
        {
            return m.Groups[1].Value.Equals("Detalle", StringComparison.OrdinalIgnoreCase)
                ? $"el detalle público de la incidencia #{m.Groups[2].Value}"
                : $"el seguimiento de su propio reporte #{m.Groups[2].Value}";
        }

        var sinBarraFinal = ruta.Length > 1 ? ruta.TrimEnd('/') : ruta;
        return Paginas.TryGetValue(sinBarraFinal, out var descripcion) ? descripcion : null;
    }
}
