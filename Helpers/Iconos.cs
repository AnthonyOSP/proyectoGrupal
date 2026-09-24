using Microsoft.AspNetCore.Html;

namespace proyectoGrupal.Helpers;

// Iconos SVG en línea (estilo Lucide, trazo de 2px) para no depender de librerías externas.
// Uso en una vista: @Iconos.Svg("lightbulb")
public static class Iconos
{
    private static readonly Dictionary<string, string> Trazos = new()
    {
        ["road"] = "<path d=\"M4 20 8 4\"/><path d=\"m20 20-4-16\"/><path d=\"M12 4v2\"/><path d=\"M12 10v3\"/><path d=\"M12 17v3\"/>",
        ["lightbulb"] = "<path d=\"M9 18h6\"/><path d=\"M10 22h4\"/><path d=\"M15.1 14c.2-1 .7-1.7 1.4-2.5A5.5 5.5 0 1 0 7.5 11.5c.7.8 1.2 1.5 1.4 2.5\"/>",
        ["tree"] = "<path d=\"M12 22v-6\"/><path d=\"M7 16h10a4 4 0 0 0 1-7.9A6 6 0 0 0 6 8.1 4 4 0 0 0 7 16Z\"/>",
        ["trash"] = "<path d=\"M3 6h18\"/><path d=\"M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6\"/><path d=\"M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2\"/><path d=\"M10 11v6\"/><path d=\"M14 11v6\"/>",
        ["droplet"] = "<path d=\"M12 22a7 7 0 0 0 7-7c0-2-1-3.9-3-5.5S12.5 5.5 12 2.5C11.5 5.5 10 7.9 8 9.5S5 13 5 15a7 7 0 0 0 7 7Z\"/>",
        ["traffic"] = "<rect x=\"7\" y=\"2\" width=\"10\" height=\"20\" rx=\"3\"/><circle cx=\"12\" cy=\"7\" r=\"1.5\"/><circle cx=\"12\" cy=\"12\" r=\"1.5\"/><circle cx=\"12\" cy=\"17\" r=\"1.5\"/>",
        ["shield"] = "<path d=\"M20 13c0 5-3.5 7.5-7.7 9a1 1 0 0 1-.7 0C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.2-2.7a1.2 1.2 0 0 1 1.6 0C14.5 3.8 17 5 19 5a1 1 0 0 1 1 1Z\"/>",
        ["map-pin"] = "<path d=\"M20 10c0 5-5.5 10.2-7.4 11.8a1 1 0 0 1-1.2 0C9.5 20.2 4 15 4 10a8 8 0 0 1 16 0\"/><circle cx=\"12\" cy=\"10\" r=\"3\"/>",
        ["calendar"] = "<rect x=\"3\" y=\"4\" width=\"18\" height=\"18\" rx=\"2\"/><path d=\"M16 2v4\"/><path d=\"M8 2v4\"/><path d=\"M3 10h18\"/>",
        ["clock"] = "<circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"M12 6v6l4 2\"/>",
        ["search"] = "<circle cx=\"11\" cy=\"11\" r=\"8\"/><path d=\"m21 21-4.3-4.3\"/>",
        ["check-circle"] = "<circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"m9 12 2 2 4-4\"/>",
        ["check"] = "<path d=\"M20 6 9 17l-5-5\"/>",
        ["arrow-right"] = "<path d=\"M5 12h14\"/><path d=\"m12 5 7 7-7 7\"/>",
        ["arrow-left"] = "<path d=\"m12 19-7-7 7-7\"/><path d=\"M19 12H5\"/>",
        ["plus"] = "<path d=\"M5 12h14\"/><path d=\"M12 5v14\"/>",
        ["menu"] = "<path d=\"M4 6h16\"/><path d=\"M4 12h16\"/><path d=\"M4 18h16\"/>",
        ["camera"] = "<path d=\"M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z\"/><circle cx=\"12\" cy=\"13\" r=\"3\"/>",
        ["image"] = "<rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"2\"/><circle cx=\"9\" cy=\"9\" r=\"2\"/><path d=\"m21 15-3.1-3.1a2 2 0 0 0-2.8 0L6 21\"/>",
        ["x"] = "<path d=\"M18 6 6 18\"/><path d=\"m6 6 12 12\"/>",
        ["info"] = "<circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"M12 16v-4\"/><path d=\"M12 8h.01\"/>",
        ["send"] = "<path d=\"M14.5 21.7a.5.5 0 0 0 .9 0L22 2.5a.5.5 0 0 0-.6-.6L2.3 8.6a.5.5 0 0 0 0 .9l8 3.2a2 2 0 0 1 1 1z\"/><path d=\"m21.9 2.1-10.9 10.9\"/>",
        ["list"] = "<path d=\"M8 6h13\"/><path d=\"M8 12h13\"/><path d=\"M8 18h13\"/><path d=\"M3 6h.01\"/><path d=\"M3 12h.01\"/><path d=\"M3 18h.01\"/>",
        ["megaphone"] = "<path d=\"m3 11 18-5v12L3 14v-3z\"/><path d=\"M11.6 16.8a3 3 0 1 1-5.8-1.6\"/>",
        ["users"] = "<path d=\"M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2\"/><circle cx=\"9\" cy=\"7\" r=\"4\"/><path d=\"M22 21v-2a4 4 0 0 0-3-3.9\"/><path d=\"M16 3.1a4 4 0 0 1 0 7.8\"/>",
        ["filter"] = "<path d=\"M22 3H2l8 9.5V19l4 2v-8.5L22 3z\"/>",
        ["pencil"] = "<path d=\"M21.2 6.8a1 1 0 0 0-4-4L3.8 16.2a2 2 0 0 0-.5.8l-1.3 4.4a.5.5 0 0 0 .6.6l4.4-1.3a2 2 0 0 0 .8-.5z\"/>",
    };

    // Devuelve el SVG listo para insertar en la vista.
    // Por defecto es decorativo (aria-hidden) porque siempre va junto a un texto visible.
    public static IHtmlContent Svg(string nombre, int tamano = 20, string clase = "")
    {
        var trazos = Trazos.TryGetValue(nombre, out var t) ? t : Trazos["info"];
        var html =
            $"<svg class=\"icono {clase}\" width=\"{tamano}\" height=\"{tamano}\" viewBox=\"0 0 24 24\" fill=\"none\" " +
            "stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" " +
            $"aria-hidden=\"true\" focusable=\"false\">{trazos}</svg>";
        return new HtmlString(html);
    }
}
