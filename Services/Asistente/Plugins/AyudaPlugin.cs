using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using proyectoGrupal.Services.Asistente.Conocimiento;

namespace proyectoGrupal.Services.Asistente.Plugins;

// Herramienta del agente: busca en los documentos de ayuda de Alerta Vecinal (Qdrant o búsqueda local).
public sealed class AyudaPlugin
{
    private const int Fragmentos = 3;

    private readonly IBaseConocimientoService _conocimiento;
    private readonly SesionAsistente _sesion;
    private readonly ILogger<AyudaPlugin> _logger;

    public AyudaPlugin(IBaseConocimientoService conocimiento, SesionAsistente sesion, ILogger<AyudaPlugin> logger)
    {
        _conocimiento = conocimiento;
        _sesion = sesion;
        _logger = logger;
    }

    [KernelFunction("BuscarAyuda")]
    [Description("Busca en la ayuda oficial de Alerta Vecinal (reportar, límites, categorías, estados, fotos, cuenta, contacto, etc.).")]
    public async Task<string> BuscarAyudaAsync(
        [Description("Tema a buscar, en español. Ej: 'límite de reportes'.")] string consulta,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consulta))
        {
            return "Indica qué tema se debe buscar en la ayuda.";
        }

        var busqueda = await _conocimiento.BuscarAsync(consulta, Fragmentos, cancellationToken);
        _logger.LogDebug("Asistente | BuscarAyuda '{Consulta}': {Cantidad} fragmentos ({Motor}).",
            consulta, busqueda.Resultados.Count, busqueda.Motor);

        if (busqueda.Resultados.Count == 0)
        {
            return "La ayuda no tiene información sobre ese tema. No inventes una respuesta: dilo y sugiere escribir por la página de Contacto.";
        }

        var texto = new StringBuilder("Secciones de la ayuda de Alerta Vecinal relacionadas con la consulta:");
        foreach (var resultado in busqueda.Resultados)
        {
            _sesion.AgregarFuente(resultado.Documento);
            texto.AppendLine().AppendLine();
            texto.AppendLine(resultado.Seccion.Length > 0 ? $"### {resultado.Documento} — {resultado.Seccion}" : $"### {resultado.Documento}");
            texto.Append(resultado.Contenido);
        }

        return texto.ToString();
    }
}
