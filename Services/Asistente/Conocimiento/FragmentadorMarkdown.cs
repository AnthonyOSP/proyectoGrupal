using System.Text;
using System.Text.RegularExpressions;

namespace proyectoGrupal.Services.Asistente.Conocimiento;

// Un fragmento de un documento de ayuda: una sección (## Título) con su texto.
public record FragmentoConocimiento(string Documento, string Seccion, string Contenido)
{
    // Texto que se convierte en vector y que recibe el modelo: incluye el documento y la sección,
    // así el fragmento se entiende solo aunque llegue sin el resto del documento.
    public string TextoCompleto => Seccion.Length == 0 || Seccion == Documento
        ? $"{Documento}\n\n{Contenido}"
        : $"{Documento} — {Seccion}\n\n{Contenido}";
}

// Divide un documento en fragmentos para la búsqueda semántica.
//   - Markdown: un fragmento por sección "## ". El título "# " es el nombre del documento.
//   - Texto sin títulos: fragmentos por párrafos.
// Una sección muy larga se divide por párrafos para que cada fragmento sea específico.
public static class FragmentadorMarkdown
{
    public const int MaximoCaracteres = 1500;

    private static readonly Regex TituloDocumento = new(@"^#\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex TituloSeccion = new(@"^##\s+(.+)$", RegexOptions.Compiled);

    public static List<FragmentoConocimiento> Fragmentar(string texto, string nombrePorDefecto)
    {
        var lineas = (texto ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var documento = nombrePorDefecto;
        var seccion = string.Empty;
        var cuerpo = new StringBuilder();
        var fragmentos = new List<FragmentoConocimiento>();

        void Cerrar()
        {
            foreach (var parte in DividirLargo(cuerpo.ToString().Trim()))
            {
                fragmentos.Add(new FragmentoConocimiento(documento, seccion, parte));
            }
            cuerpo.Clear();
        }

        foreach (var linea in lineas)
        {
            if (TituloDocumento.Match(linea) is { Success: true } doc)
            {
                Cerrar();
                documento = doc.Groups[1].Value.Trim();
                seccion = string.Empty;
                continue;
            }

            if (TituloSeccion.Match(linea) is { Success: true } sec)
            {
                Cerrar();
                seccion = sec.Groups[1].Value.Trim();
                continue;
            }

            cuerpo.AppendLine(linea);
        }

        Cerrar();
        return fragmentos;
    }

    // Junta párrafos hasta MaximoCaracteres. Un párrafo más largo que eso se corta por oraciones.
    private static IEnumerable<string> DividirLargo(string texto)
    {
        if (texto.Length == 0)
        {
            yield break;
        }

        if (texto.Length <= MaximoCaracteres)
        {
            yield return texto;
            yield break;
        }

        var actual = new StringBuilder();
        foreach (var parrafo in texto.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var pedazo in CortarParrafo(parrafo))
            {
                if (actual.Length > 0 && actual.Length + pedazo.Length + 2 > MaximoCaracteres)
                {
                    yield return actual.ToString();
                    actual.Clear();
                }

                if (actual.Length > 0)
                {
                    actual.Append("\n\n");
                }
                actual.Append(pedazo);
            }
        }

        if (actual.Length > 0)
        {
            yield return actual.ToString();
        }
    }

    private static IEnumerable<string> CortarParrafo(string parrafo)
    {
        if (parrafo.Length <= MaximoCaracteres)
        {
            yield return parrafo;
            yield break;
        }

        var actual = new StringBuilder();
        foreach (var oracion in Regex.Split(parrafo, @"(?<=[.!?])\s+"))
        {
            if (actual.Length > 0 && actual.Length + oracion.Length + 1 > MaximoCaracteres)
            {
                yield return actual.ToString();
                actual.Clear();
            }

            // Una "oración" sin puntos más larga que el máximo se corta sin más.
            var resto = oracion;
            while (resto.Length > MaximoCaracteres)
            {
                yield return resto[..MaximoCaracteres];
                resto = resto[MaximoCaracteres..];
            }

            if (actual.Length > 0)
            {
                actual.Append(' ');
            }
            actual.Append(resto);
        }

        if (actual.Length > 0)
        {
            yield return actual.ToString();
        }
    }
}
