using proyectoGrupal.Models;
using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Helpers;

// Datos visuales de cada categoría (icono, descripción y slug para la URL).
// En la base de datos solo se guarda el nombre (ver Models/CategoriasIncidencia.cs).
public static class CatalogoCategorias
{
    public static readonly List<CategoriaViewModel> Todas = new()
    {
        new() { Slug = "pistas", Nombre = CategoriasIncidencia.PistasVeredas, NombreCorto = "Pistas y veredas", Icono = "road",
                Descripcion = "Baches, veredas rotas, rampas dañadas o pistas en mal estado." },
        new() { Slug = "alumbrado", Nombre = CategoriasIncidencia.Alumbrado, NombreCorto = "Alumbrado", Icono = "lightbulb",
                Descripcion = "Postes apagados, luminarias rotas o calles sin iluminación." },
        new() { Slug = "parques", Nombre = CategoriasIncidencia.Parques, NombreCorto = "Parques", Icono = "tree",
                Descripcion = "Áreas verdes descuidadas, juegos rotos o árboles en riesgo." },
        new() { Slug = "limpieza", Nombre = CategoriasIncidencia.Limpieza, NombreCorto = "Limpieza", Icono = "trash",
                Descripcion = "Acumulación de basura, desmonte o puntos críticos de residuos." },
        new() { Slug = "agua", Nombre = CategoriasIncidencia.Agua, NombreCorto = "Agua y desagüe", Icono = "droplet",
                Descripcion = "Fugas de agua, buzones abiertos o desagües colapsados." },
        new() { Slug = "transito", Nombre = CategoriasIncidencia.Transito, NombreCorto = "Tránsito", Icono = "traffic",
                Descripcion = "Semáforos dañados, señales caídas o cruces peligrosos." },
        new() { Slug = "seguridad", Nombre = CategoriasIncidencia.Seguridad, NombreCorto = "Seguridad", Icono = "shield",
                Descripcion = "Zonas inseguras, espacios abandonados o riesgos para vecinos." },
        new() { Slug = "otros", Nombre = CategoriasIncidencia.Otros, NombreCorto = "Otros", Icono = "map-pin",
                Descripcion = "Cualquier otro problema que afecte a la comunidad." },
    };

    // Busca por slug de la URL. Ej: "alumbrado"
    public static CategoriaViewModel? PorSlug(string? slug) =>
        Todas.FirstOrDefault(c => c.Slug == slug);

    // Busca por el nombre guardado en la base de datos. Ej: "Alumbrado público"
    // Si el nombre no existe en el catálogo, se muestra como "Otros" para no romper la vista.
    public static CategoriaViewModel PorNombre(string nombre) =>
        Todas.FirstOrDefault(c => c.Nombre == nombre) ?? Todas.Last();
}
