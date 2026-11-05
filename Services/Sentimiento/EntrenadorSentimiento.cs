using System.Text.Json;
using Microsoft.ML;

namespace proyectoGrupal.Services.Sentimiento;

// Datos del último entrenamiento. Se guardan junto al modelo (SentimientoModel.json) y los muestra el panel.
public record InfoEntrenamiento(
    DateTime Fecha,
    int EjemplosCsv,
    int Correcciones,
    double Exactitud,
    double F1);

// Entrenamiento del modelo de sentimientos con ML.NET. Lo usan los dos lados, así entrenan igual:
//   - el programa de consola ML/sentimentAnalysisNLP/Entrenamiento (enlaza este archivo), y
//   - el botón "Reentrenar modelo" del panel administrativo (SentimientoService).
public static class EntrenadorSentimiento
{
    // Pipeline: FeaturizeText convierte el texto en números (minúsculas, sin tildes, n-gramas de
    // palabras y de caracteres) y SdcaLogisticRegression aprende a separar positivos de negativos.
    public static IEstimator<ITransformer> CrearPipeline(MLContext ctx) =>
        ctx.Transforms.Text
            .FeaturizeText(outputColumnName: "Features", inputColumnName: nameof(EntradaSentimiento.Comentario))
            .Append(ctx.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: "Label", featureColumnName: "Features"));

    // Entrena con el CSV más los ejemplos extra (las correcciones del administrador).
    // Evalúa con un 20% de los datos reservado y luego entrena el modelo final con TODOS los datos.
    public static (ITransformer Modelo, DataViewSchema Esquema, InfoEntrenamiento Info) Entrenar(
        MLContext ctx, string rutaCsv, IReadOnlyCollection<EntradaSentimiento> extras)
    {
        // Step 2. Read in the input data from a text file for model training
        IDataView csv = ctx.Data.LoadFromTextFile<EntradaSentimiento>(rutaCsv, hasHeader: true, separatorChar: ',', allowQuoting: true);
        var filasCsv = ctx.Data.CreateEnumerable<EntradaSentimiento>(csv, reuseRowObject: false).ToList();

        IDataView trainingData = ctx.Data.LoadFromEnumerable(filasCsv.Concat(extras));

        // Evaluación con datos que el modelo no vio al entrenar.
        var particion = ctx.Data.TrainTestSplit(trainingData, testFraction: 0.2, seed: 1);
        var pipeline = CrearPipeline(ctx);
        var metricas = ctx.BinaryClassification.Evaluate(
            pipeline.Fit(particion.TrainSet).Transform(particion.TestSet), labelColumnName: "Label");

        // Modelo final con todos los datos.
        var modelo = pipeline.Fit(trainingData);
        var info = new InfoEntrenamiento(DateTime.Now, filasCsv.Count, extras.Count, metricas.Accuracy, metricas.F1Score);
        return (modelo, trainingData.Schema, info);
    }

    // Guarda el modelo (.zip) y sus datos (.json). Escribe primero en un archivo temporal y luego lo
    // reemplaza: así nunca queda un modelo a medio escribir si algo falla.
    public static void Guardar(MLContext ctx, ITransformer modelo, DataViewSchema esquema, InfoEntrenamiento info, string rutaModelo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(rutaModelo)!);
        var temporal = rutaModelo + ".tmp";
        ctx.Model.Save(modelo, esquema, temporal);
        File.Move(temporal, rutaModelo, overwrite: true);

        File.WriteAllText(RutaInfo(rutaModelo), JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
    }

    // Datos del último entrenamiento, o null si no hay (o el archivo no se puede leer).
    public static InfoEntrenamiento? LeerInfo(string rutaModelo)
    {
        try
        {
            var ruta = RutaInfo(rutaModelo);
            return File.Exists(ruta) ? JsonSerializer.Deserialize<InfoEntrenamiento>(File.ReadAllText(ruta)) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string RutaInfo(string rutaModelo) => Path.ChangeExtension(rutaModelo, ".json");
}
