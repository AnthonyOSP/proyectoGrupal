namespace proyectoGrupal.Models;

// Valores permitidos para Incidencia.Categoria.
public static class CategoriasIncidencia
{
    public const string PistasVeredas = "Pistas y veredas";
    public const string Alumbrado = "Alumbrado público";
    public const string Parques = "Parques y áreas verdes";
    public const string Limpieza = "Limpieza pública";
    public const string Agua = "Agua y desagüe";
    public const string Transito = "Tránsito y señalización";
    public const string Seguridad = "Seguridad";
    public const string Otros = "Otros";

    public static readonly string[] Todas =
    {
        PistasVeredas, Alumbrado, Parques, Limpieza, Agua, Transito, Seguridad, Otros
    };
}
