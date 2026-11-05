// Entrena el modelo de análisis de sentimientos (positivo / negativo) con ML.NET.
//
// Uso (desde la raíz del repositorio):
//   dotnet run --project ML/sentimentAnalysisNLP/Entrenamiento
//
// Lee  ML/sentimentAnalysisNLP/trainingData/comentarios_entrenamiento.csv  (Comentario,Label)
// y guarda el modelo en  MLModels/SentimientoModel.zip, que la aplicación web carga al iniciar.
//
// Este programa usa SOLO el CSV. Las correcciones que hace el administrador en el panel están en la
// base de datos: para incluirlas usa el botón "Reentrenar modelo" de Panel administrativo → Mensajes.
// El pipeline es el mismo en los dos casos (Services/Sentimiento/EntrenadorSentimiento.cs).
using Microsoft.ML;
using proyectoGrupal.Services.Sentimiento;

var raiz = BuscarRaizDelRepositorio();
var dataPath = Path.Combine(raiz, "ML", "sentimentAnalysisNLP", "trainingData", "comentarios_entrenamiento.csv");
var modelPath = Path.Combine(raiz, "MLModels", "SentimientoModel.zip");

// Contexto de ML.NET. La semilla fija hace que el resultado sea el mismo en cada ejecución.
var ctx = new MLContext(seed: 1);

Console.WriteLine($"Datos: {dataPath}");
var (modelo, esquema, info) = EntrenadorSentimiento.Entrenar(ctx, dataPath, Array.Empty<EntradaSentimiento>());

Console.WriteLine();
Console.WriteLine($"Ejemplos de entrenamiento: {info.EjemplosCsv}");
Console.WriteLine("Evaluación con el 20% de los datos (no usados al entrenar):");
Console.WriteLine($"  Exactitud (Accuracy): {info.Exactitud:P1}");
Console.WriteLine($"  F1:                   {info.F1:P1}");

EntrenadorSentimiento.Guardar(ctx, modelo, esquema, info, modelPath);
Console.WriteLine();
Console.WriteLine($"Modelo guardado en: {modelPath}");

// Probar el modelo guardado con algunos mensajes (consumo, igual que en la aplicación).
var motor = ctx.Model.CreatePredictionEngine<EntradaSentimiento, PrediccionSentimiento>(ctx.Model.Load(modelPath, out _));
string[] ejemplos =
{
    "Excelente servicio, todo perfecto.",
    "Muy mala experiencia, no volveré a usarlo.",
    "Muy mal, nadie me responde mis incidencias",
    "me parece genial que borren los rostros al subir las imagenes",
    "Gracias, ya arreglaron el poste de mi calle",
    "Reporté la basura hace dos semanas y siguen sin recogerla"
};
Console.WriteLine();
Console.WriteLine("Pruebas:");
foreach (var texto in ejemplos)
{
    var prediccion = motor.Predict(new EntradaSentimiento { Comentario = texto });
    Console.WriteLine($"  {(prediccion.EsPositivo ? "Positivo" : "Negativo"),-8} ({prediccion.Probability,4:P0} positivo)  {texto}");
}

static string BuscarRaizDelRepositorio()
{
    var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
    while (carpeta != null && !File.Exists(Path.Combine(carpeta.FullName, "proyectoGrupal.csproj")))
    {
        carpeta = carpeta.Parent;
    }
    return carpeta?.FullName
        ?? throw new InvalidOperationException("No se encontró proyectoGrupal.csproj en las carpetas superiores.");
}
