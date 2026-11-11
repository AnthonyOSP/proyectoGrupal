using Microsoft.SemanticKernel;

namespace proyectoGrupal.Services.Asistente;

// Filtro de Semantic Kernel: deja en el log cada herramienta que el modelo decide usar,
// con sus argumentos y el largo del resultado. Sirve para entender (y mostrar) qué hace el agente.
public sealed class RegistroHerramientas : IAutoFunctionInvocationFilter
{
    private readonly ILogger _logger;

    public RegistroHerramientas(ILogger logger)
    {
        _logger = logger;
    }

    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
    {
        var argumentos = context.Arguments is null
            ? string.Empty
            : string.Join(", ", context.Arguments
                .Where(a => a.Value is not CancellationToken)
                .Select(a => $"{a.Key}=\"{Recortar(a.Value?.ToString(), 80)}\""));

        await next(context);

        var resultado = context.Result.GetValue<object>()?.ToString() ?? string.Empty;
        _logger.LogInformation("Asistente | Herramienta {Funcion}({Argumentos}) → {Caracteres} caracteres (vuelta {Vuelta}).",
            context.Function.Name, argumentos, resultado.Length, context.RequestSequenceIndex + 1);
    }

    private static string Recortar(string? texto, int maximo) =>
        texto == null ? string.Empty : texto.Length <= maximo ? texto : texto[..maximo] + "…";
}
