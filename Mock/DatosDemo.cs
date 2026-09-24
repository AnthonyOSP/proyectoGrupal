using proyectoGrupal.ViewModels;

namespace proyectoGrupal.Mock;

// Datos ficticios SOLO para mostrar la interfaz (Etapa 1).
// En la Etapa 2 esta clase se reemplaza por consultas a SQLite con Entity Framework.
public static class DatosDemo
{
    public static readonly List<CategoriaViewModel> Categorias = new()
    {
        new() { Slug = "pistas", Nombre = "Pistas y veredas", NombreCorto = "Pistas y veredas", Icono = "road",
                Descripcion = "Baches, veredas rotas, rampas dañadas o pistas en mal estado." },
        new() { Slug = "alumbrado", Nombre = "Alumbrado público", NombreCorto = "Alumbrado", Icono = "lightbulb",
                Descripcion = "Postes apagados, luminarias rotas o calles sin iluminación." },
        new() { Slug = "parques", Nombre = "Parques y áreas verdes", NombreCorto = "Parques", Icono = "tree",
                Descripcion = "Áreas verdes descuidadas, juegos rotos o árboles en riesgo." },
        new() { Slug = "limpieza", Nombre = "Limpieza pública", NombreCorto = "Limpieza", Icono = "trash",
                Descripcion = "Acumulación de basura, desmonte o puntos críticos de residuos." },
        new() { Slug = "agua", Nombre = "Agua y desagüe", NombreCorto = "Agua y desagüe", Icono = "droplet",
                Descripcion = "Fugas de agua, buzones abiertos o desagües colapsados." },
        new() { Slug = "transito", Nombre = "Tránsito y señalización", NombreCorto = "Tránsito", Icono = "traffic",
                Descripcion = "Semáforos dañados, señales caídas o cruces peligrosos." },
        new() { Slug = "seguridad", Nombre = "Seguridad", NombreCorto = "Seguridad", Icono = "shield",
                Descripcion = "Zonas inseguras, espacios abandonados o riesgos para vecinos." },
        new() { Slug = "otros", Nombre = "Otros", NombreCorto = "Otros", Icono = "map-pin",
                Descripcion = "Cualquier otro problema que afecte a la comunidad." },
    };

    public static readonly List<IncidenciaViewModel> Incidencias = new()
    {
        new() { Id = 1, Titulo = "Vereda dañada", Categoria = Cat("pistas"),
                Descripcion = "Vereda deteriorada cerca del parque principal. Hay losetas levantadas y es peligroso para adultos mayores y personas con coche de bebé.",
                Ubicacion = "Jr. Lima cuadra 3, frente al parque principal", Fecha = new DateTime(2026, 9, 23), Estado = EstadoIncidencia.Pendiente },
        new() { Id = 2, Titulo = "Poste de alumbrado sin funcionar", Categoria = Cat("alumbrado"),
                Descripcion = "El poste ubicado cerca del parque no enciende durante la noche. La esquina queda totalmente oscura desde las 6:30 p. m.",
                Ubicacion = "Av. Principal con Calle Los Olivos", Fecha = new DateTime(2026, 9, 23), Estado = EstadoIncidencia.EnRevision },
        new() { Id = 3, Titulo = "Parque con acumulación de basura", Categoria = Cat("limpieza"),
                Descripcion = "Se observa acumulación de residuos en la entrada del parque desde hace varios días. Atrae perros callejeros y malos olores.",
                Ubicacion = "Parque Los Jazmines, entrada por Calle 8", Fecha = new DateTime(2026, 9, 20), Estado = EstadoIncidencia.Atendida },
        new() { Id = 4, Titulo = "Fuga de agua en la vía pública", Categoria = Cat("agua"),
                Descripcion = "Sale agua de forma constante desde una tubería rota junto a la vereda. El agua ya llega a la pista.",
                Ubicacion = "Av. Próceres con Jr. Lima", Fecha = new DateTime(2026, 9, 22), Estado = EstadoIncidencia.EnRevision },
        new() { Id = 5, Titulo = "Semáforo peatonal apagado", Categoria = Cat("transito"),
                Descripcion = "El semáforo peatonal no funciona y los vecinos cruzan sin saber cuándo es seguro. Hay mucho tránsito en horas punta.",
                Ubicacion = "Cruce de Av. Los Próceres y Av. Central", Fecha = new DateTime(2026, 9, 21), Estado = EstadoIncidencia.Pendiente },
        new() { Id = 6, Titulo = "Juegos infantiles rotos", Categoria = Cat("parques"),
                Descripcion = "El columpio y el tobogán del parque tienen piezas sueltas. Los niños siguen usándolos y pueden lastimarse.",
                Ubicacion = "Parque Santa Rosa, zona de juegos", Fecha = new DateTime(2026, 9, 18), Estado = EstadoIncidencia.Atendida },
        new() { Id = 7, Titulo = "Bache profundo en la pista", Categoria = Cat("pistas"),
                Descripcion = "Hay un bache grande en el carril derecho. Varios mototaxis han tenido que frenar de golpe para esquivarlo.",
                Ubicacion = "Av. Central cuadra 12", Fecha = new DateTime(2026, 9, 19), Estado = EstadoIncidencia.EnRevision },
        new() { Id = 8, Titulo = "Terreno abandonado sin cerco", Categoria = Cat("seguridad"),
                Descripcion = "Un terreno sin cerco y sin iluminación se ha convertido en un punto inseguro por las noches.",
                Ubicacion = "Calle Las Gardenias, altura del mercado", Fecha = new DateTime(2026, 9, 17), Estado = EstadoIncidencia.Pendiente },
        new() { Id = 9, Titulo = "Señal de PARE caída", Categoria = Cat("transito"),
                Descripcion = "La señal de PARE de la esquina está en el suelo. Los conductores no respetan la preferencia.",
                Ubicacion = "Jr. Huáscar con Jr. Tupac Amaru", Fecha = new DateTime(2026, 9, 15), Estado = EstadoIncidencia.Atendida },
    };

    public static CategoriaViewModel? BuscarCategoria(string? slug) =>
        Categorias.FirstOrDefault(c => c.Slug == slug);

    private static CategoriaViewModel Cat(string slug) => Categorias.First(c => c.Slug == slug);
}
