namespace proyectoGrupal.Models;

// Valores que se guardan en MensajeContacto.Sentimiento (resultado del modelo de ML.NET).
public static class SentimientosMensaje
{
    public const string Positivo = "Positivo";
    public const string Negativo = "Negativo";

    public static readonly string[] Todos = { Positivo, Negativo };
}
