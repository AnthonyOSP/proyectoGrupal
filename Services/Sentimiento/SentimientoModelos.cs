using Microsoft.ML.Data;

namespace proyectoGrupal.Services.Sentimiento;

// Clases de entrada y salida del modelo de análisis de sentimientos (ML.NET).
// Las usan los dos lados, por eso viven en un solo archivo:
//   - el entrenamiento (ML/sentimentAnalysisNLP/Entrenamiento, que enlaza este archivo), y
//   - la aplicación web, que consume el modelo entrenado (SentimientoService).
// Si cambias un nombre o una columna aquí, vuelve a entrenar el modelo.

// Una fila del CSV de entrenamiento (comentarios_entrenamiento.csv): Comentario,Label
// En la aplicación solo se llena Comentario (el texto del mensaje de contacto).
public class EntradaSentimiento
{
    [LoadColumn(0)]
    public string Comentario { get; set; } = string.Empty;

    // 1 = positivo, 0 = negativo. Solo se usa al entrenar.
    [LoadColumn(1), ColumnName("Label")]
    public bool Label { get; set; }
}

// Lo que devuelve el modelo para un texto.
public class PrediccionSentimiento
{
    // true = positivo, false = negativo (umbral 0.5 sobre Probability).
    [ColumnName("PredictedLabel")]
    public bool EsPositivo { get; set; }

    // Probabilidad (0 a 1) de que el texto sea positivo.
    public float Probability { get; set; }

    // Puntaje crudo del modelo (antes de convertirlo en probabilidad).
    public float Score { get; set; }
}
