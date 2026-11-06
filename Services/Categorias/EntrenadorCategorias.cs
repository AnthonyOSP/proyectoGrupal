using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace proyectoGrupal.Services.Categorias;

// ===== Clasificación MULTICLASE: sugerir la categoría de una incidencia a partir de su texto =====
// Este archivo lo comparten el programa de consola ML/categorizacionIncidencias/Entrenamiento
// (lo enlaza) y la aplicación web (CategoriaSugeridaService), así los dos entrenan igual.

// Una fila del CSV de entrenamiento (incidencias_entrenamiento.csv): Texto,Categoria
// En la aplicación solo se llena Texto (título + descripción del reporte).
public class EntradaCategoria
{
    [LoadColumn(0)]
    public string Texto { get; set; } = string.Empty;

    // Nombre exacto de la categoría (uno de CategoriasIncidencia.Todas). Solo se usa al entrenar.
    [LoadColumn(1)]
    public string Categoria { get; set; } = string.Empty;
}

// Lo que devuelve el modelo para un texto.
public class PrediccionCategoria
{
    // Categoría más probable.
    [ColumnName("PredictedLabel")]
    public string Categoria { get; set; } = string.Empty;

    // Probabilidad de cada categoría (en el orden de las "slot names" de la columna Score).
    public float[] Score { get; set; } = Array.Empty<float>();
}

// Datos del último entrenamiento. Se guardan junto al modelo (CategoriaModel.json).
public record InfoEntrenamientoCategorias(
    DateTime Fecha,
    int EjemplosCsv,
    int EjemplosIncidencias,
    double ExactitudMicro,
    double ExactitudMacro);

public static class EntrenadorCategorias
{
    // Pipeline:
    //   MapValueToKey       → convierte el nombre de la categoría en un número (la "clase").
    //   FeaturizeText       → convierte el texto en números (n-gramas de palabras y caracteres).
    //   SdcaMaximumEntropy  → aprende a elegir una de las 8 categorías (clasificación multiclase).
    //   MapKeyToValue       → devuelve el número de clase como nombre de categoría.
    public static IEstimator<ITransformer> CrearPipeline(MLContext ctx) =>
        ctx.Transforms.Conversion.MapValueToKey(outputColumnName: "Label", inputColumnName: nameof(EntradaCategoria.Categoria))
            .Append(ctx.Transforms.Text.FeaturizeText(outputColumnName: "Features", inputColumnName: nameof(EntradaCategoria.Texto)))
            .Append(ctx.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features"))
            .Append(ctx.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

    // Entrena con el CSV + ejemplos extra (las incidencias reales ya registradas).
    // Evalúa con validación cruzada de 5 particiones (con pocos datos es más estable que un solo
    // 80/20) y luego entrena el modelo final con TODOS los datos.
    public static (ITransformer Modelo, DataViewSchema Esquema, InfoEntrenamientoCategorias Info) Entrenar(
        MLContext ctx, string rutaCsv, IReadOnlyCollection<EntradaCategoria> extras)
    {
        IDataView csv = ctx.Data.LoadFromTextFile<EntradaCategoria>(rutaCsv, hasHeader: true, separatorChar: ',', allowQuoting: true);
        var filasCsv = ctx.Data.CreateEnumerable<EntradaCategoria>(csv, reuseRowObject: false).ToList();
        IDataView trainingData = ctx.Data.LoadFromEnumerable(filasCsv.Concat(extras));

        var pipeline = CrearPipeline(ctx);
        var validacion = ctx.MulticlassClassification.CrossValidate(trainingData, pipeline, numberOfFolds: 5, seed: 1);

        var modelo = pipeline.Fit(trainingData);
        var info = new InfoEntrenamientoCategorias(
            DateTime.Now, filasCsv.Count, extras.Count,
            validacion.Average(v => v.Metrics.MicroAccuracy),
            validacion.Average(v => v.Metrics.MacroAccuracy));
        return (modelo, trainingData.Schema, info);
    }

    // Guarda el modelo (.zip) y sus datos (.json). Primero en un temporal y luego lo reemplaza.
    public static void Guardar(MLContext ctx, ITransformer modelo, DataViewSchema esquema, InfoEntrenamientoCategorias info, string rutaModelo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(rutaModelo)!);
        var temporal = rutaModelo + ".tmp";
        ctx.Model.Save(modelo, esquema, temporal);
        File.Move(temporal, rutaModelo, overwrite: true);
        File.WriteAllText(Path.ChangeExtension(rutaModelo, ".json"), JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static InfoEntrenamientoCategorias? LeerInfo(string rutaModelo)
    {
        try
        {
            var ruta = Path.ChangeExtension(rutaModelo, ".json");
            return File.Exists(ruta) ? JsonSerializer.Deserialize<InfoEntrenamientoCategorias>(File.ReadAllText(ruta)) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Nombres de las categorías en el mismo orden que PrediccionCategoria.Score.
    public static string[] NombresDeClases(PredictionEngine<EntradaCategoria, PrediccionCategoria> motor)
    {
        VBuffer<ReadOnlyMemory<char>> nombres = default;
        motor.OutputSchema[nameof(PrediccionCategoria.Score)].GetSlotNames(ref nombres);
        return nombres.DenseValues().Select(n => n.ToString()).ToArray();
    }
}
