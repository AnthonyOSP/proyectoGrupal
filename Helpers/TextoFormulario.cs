namespace proyectoGrupal.Helpers;

// Normalización de los textos que escriben los vecinos (formularios y asistente virtual).
public static class TextoFormulario
{
    // Título, ubicación, nombre: una sola línea, sin espacios al inicio/final ni repetidos.
    // No cambia mayúsculas ni el resto del texto. Si solo había espacios, queda null.
    public static string? UnaLinea(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        return string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    // Descripción, mensaje: se respeta lo escrito (incluidos los saltos de línea); solo se quitan los
    // espacios del inicio y el final, y los saltos "\r\n" se guardan como "\n" (1 carácter, igual que
    // en el contador del formulario).
    public static string? TextoLargo(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        return texto.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }

    // Corta el texto al máximo permitido (sin dejar espacios al final).
    public static string? Recortar(string? texto, int maximo) =>
        texto == null || texto.Length <= maximo ? texto : texto[..maximo].TrimEnd();
}
