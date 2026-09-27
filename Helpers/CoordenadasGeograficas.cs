using System.Globalization;

namespace proyectoGrupal.Helpers;

// Lectura y validación de coordenadas enviadas por el navegador (grados decimales).
// No se confía en el formulario: solo se aceptan números simples con punto decimal
// ("-12.0464", "77.03"), dentro de rango y en pareja. Se rechazan "NaN", "Infinity",
// notación científica ("1e5"), comas y cualquier otro texto.
public static class CoordenadasGeograficas
{
    public const decimal LatitudMinima = -90m, LatitudMaxima = 90m;
    public const decimal LongitudMinima = -180m, LongitudMaxima = 180m;

    // Largo máximo del texto recibido (un número de 17 dígitos significativos cabe de sobra).
    public const int LargoMaximoTexto = 40;

    // Vista inicial del mapa del formulario (Lima) antes de que el vecino marque un punto.
    public const string CentroInicialLatitud = "-12.0464";
    public const string CentroInicialLongitud = "-77.0428";
    public const int ZoomInicial = 12;

    private const NumberStyles Formato = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    // Resultado: ambas null (sin punto en el mapa) o ambas con valor. Error != null si no es válido.
    public record Resultado(decimal? Latitud, decimal? Longitud, string? Campo, string? Error)
    {
        public bool EsValido => Error == null;
    }

    public static Resultado Leer(string? latitudTexto, string? longitudTexto)
    {
        var latitud = Limpiar(latitudTexto);
        var longitud = Limpiar(longitudTexto);

        // Sin punto en el mapa: permitido (las coordenadas son opcionales).
        if (latitud == null && longitud == null)
        {
            return new Resultado(null, null, null, null);
        }

        if (latitud == null)
        {
            return Fallo("Latitud", "Falta la latitud del punto. Vuelve a marcarlo en el mapa o quita el punto.");
        }

        if (longitud == null)
        {
            return Fallo("Longitud", "Falta la longitud del punto. Vuelve a marcarlo en el mapa o quita el punto.");
        }

        if (latitud.Length > LargoMaximoTexto || !decimal.TryParse(latitud, Formato, CultureInfo.InvariantCulture, out var lat))
        {
            return Fallo("Latitud", "La latitud no es un número válido.");
        }

        if (longitud.Length > LargoMaximoTexto || !decimal.TryParse(longitud, Formato, CultureInfo.InvariantCulture, out var lng))
        {
            return Fallo("Longitud", "La longitud no es un número válido.");
        }

        if (lat < LatitudMinima || lat > LatitudMaxima)
        {
            return Fallo("Latitud", "La latitud debe estar entre -90 y 90.");
        }

        if (lng < LongitudMinima || lng > LongitudMaxima)
        {
            return Fallo("Longitud", "La longitud debe estar entre -180 y 180.");
        }

        return new Resultado(lat, lng, null, null);
    }

    // Formato para atributos HTML y JavaScript: siempre con punto decimal, sin depender del idioma del servidor.
    public static string Formatear(decimal valor) => valor.ToString(CultureInfo.InvariantCulture);

    // Formato para mostrar a las personas: 6 decimales (≈ 10 cm).
    public static string Mostrar(decimal valor) => valor.ToString("0.000000", CultureInfo.InvariantCulture);

    private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static Resultado Fallo(string campo, string error) => new(null, null, campo, error);
}
