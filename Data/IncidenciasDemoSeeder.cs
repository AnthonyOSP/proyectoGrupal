using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Models;

namespace proyectoGrupal.Data;

// Incidencias de demostración.
// En el plan gratuito de Render el disco se borra en cada reinicio y la página arrancaría sin incidencias.
// Si la tabla Incidencias está VACÍA al iniciar, se crean estas 24 incidencias de ejemplo (con su historial).
// Si ya hay incidencias (por ejemplo, la base local de desarrollo), no se toca nada.
// Se desactiva con la variable INCIDENCIAS_DEMO=false. Es temporal: con una base de datos persistente
// (por ejemplo Supabase) deja de hacer falta.
public static class IncidenciasDemoSeeder
{
    // Datos fijos: así, después de cada reinicio, las incidencias tienen los mismos números (Id 1..24)
    // y los índices de búsqueda (Algolia y Qdrant) coinciden con SQLite.
    private record Demo(
        string Titulo, string Descripcion, string Categoria, string Ubicacion,
        decimal? Latitud, decimal? Longitud, string Estado, double DiasAtras, bool DelUsuarioPrueba = false);

    private static readonly Demo[] Incidencias =
    [
        new("Bache grande en la Av. La Molina",
            "Hay un bache profundo en el carril derecho. Los autos frenan de golpe para esquivarlo y ya se cayó un motociclista.",
            CategoriasIncidencia.PistasVeredas, "Av. La Molina 1150, frente al grifo", -12.0868m, -76.9432m, EstadosIncidencia.Pendiente, 2.3),
        new("Poste de alumbrado apagado",
            "El poste frente al parque no enciende desde hace una semana y la cuadra queda totalmente oscura por la noche.",
            CategoriasIncidencia.Alumbrado, "Calle Las Lilas 245, cerca del parque", -12.0995m, -76.9408m, EstadosIncidencia.EnRevision, 9.1, true),
        new("Acumulación de basura en la esquina",
            "Los vecinos dejan bolsas de basura en la esquina y el camión no pasa desde el lunes. Ya hay mal olor y perros rompiendo las bolsas.",
            CategoriasIncidencia.Limpieza, "Av. Los Frutales con Calle Los Pinos", -12.0712m, -76.9550m, EstadosIncidencia.Pendiente, 1.2),
        new("Fuga de agua en la vereda",
            "Sale agua limpia de una tubería bajo la vereda. Se está desperdiciando mucha agua y la vereda se está hundiendo.",
            CategoriasIncidencia.Agua, "Av. Raúl Ferrero 1120", -12.0921m, -76.9290m, EstadosIncidencia.Atendida, 20.4, true),
        new("Juegos infantiles rotos",
            "El columpio y el tobogán del parque tienen fierros sueltos. Los niños siguen usándolos y es peligroso.",
            CategoriasIncidencia.Parques, "Parque Los Fresnos, Calle Los Fresnos", -12.0806m, -76.9365m, EstadosIncidencia.EnRevision, 12.6),
        new("Semáforo apagado en el cruce",
            "El semáforo del cruce no funciona en ningún sentido. En las horas punta el tránsito es un caos y los peatones no pueden cruzar.",
            CategoriasIncidencia.Transito, "Av. Javier Prado Este con Av. La Molina", -12.0838m, -76.9462m, EstadosIncidencia.Atendida, 25.2),
        new("Casa abandonada usada como guarida",
            "La casa está abandonada hace meses y por las noches entran personas desconocidas. Los vecinos tenemos miedo de pasar por ahí.",
            CategoriasIncidencia.Seguridad, "Calle Los Álamos 380", -12.0973m, -76.9330m, EstadosIncidencia.Pendiente, 6.5),
        new("Poste caído",
            "Un poste de alumbrado se cayó sobre la vereda hace tres días. Los cables quedaron sueltos y la gente tiene que bajar a la pista.",
            CategoriasIncidencia.Alumbrado, "Av. La Fontana 1271, La Molina", -12.07217m, -76.94171m, EstadosIncidencia.Pendiente, 3.1, true),
        new("Buzón sin tapa en la pista",
            "Se robaron la tapa del buzón del desagüe. Está en medio de la pista y de noche no se ve: un auto ya metió la llanta.",
            CategoriasIncidencia.Agua, "Av. La Fontana 234", -12.0790m, -76.9452m, EstadosIncidencia.Pendiente, 4.2),
        new("Vereda levantada por raíces",
            "Las raíces de un árbol levantaron la vereda. Las personas mayores se tropiezan y no se puede pasar con coche de bebé.",
            CategoriasIncidencia.PistasVeredas, "Calle Las Orquídeas 150", -12.1012m, -76.9365m, EstadosIncidencia.Pendiente, 8.3),
        new("Desmonte abandonado en la berma",
            "Alguien dejó varios sacos de desmonte y restos de construcción en la berma central. Ya ocupan media vereda.",
            CategoriasIncidencia.Limpieza, "Av. Separadora Industrial, cuadra 5", -12.1050m, -76.9480m, EstadosIncidencia.EnRevision, 15.4),
        new("Señal de PARE caída",
            "La señal de PARE de la esquina está tirada en el suelo desde el fin de semana. Los autos pasan sin detenerse.",
            CategoriasIncidencia.Transito, "Calle Los Cedros con Calle Los Robles", -12.0950m, -76.9270m, EstadosIncidencia.Pendiente, 5.2),
        new("Árbol inclinado a punto de caer",
            "Después de los vientos, un árbol grande quedó muy inclinado sobre la vereda, justo frente a la puerta del colegio.",
            CategoriasIncidencia.Parques, "Parque El Remanso, frente al colegio", -12.0905m, -76.9220m, EstadosIncidencia.Pendiente, 0.4, true),
        new("Calle sin luz por las noches",
            "Toda la cuadra está sin alumbrado público. Es una zona de paso hacia el paradero y de noche es peligroso.",
            CategoriasIncidencia.Alumbrado, "Pasaje Santa Rosa, cuadra 2", -12.1080m, -76.9310m, EstadosIncidencia.Atendida, 30.2),
        new("Desagüe colapsado inunda la calle",
            "El desagüe se atoró y el agua sucia sale a la calle. El olor es muy fuerte y los niños juegan cerca.",
            CategoriasIncidencia.Agua, "Calle Los Geranios 410", -12.1110m, -76.9405m, EstadosIncidencia.EnRevision, 7.3, true),
        new("Hundimiento en la pista",
            "La pista se está hundiendo cerca de la esquina y cada día el hueco es más grande. Los carros pasan por la vereda para evitarlo.",
            CategoriasIncidencia.PistasVeredas, "Av. Melgarejo 560", -12.0870m, -76.9180m, EstadosIncidencia.Pendiente, 10.1),
        new("Contenedor de basura desbordado",
            "El contenedor de la puerta del mercado está lleno y la basura se cae a la vereda. Hay moscas y mal olor todo el día.",
            CategoriasIncidencia.Limpieza, "Mercado Santa Patricia, puerta principal", -12.0748m, -76.9560m, EstadosIncidencia.Atendida, 18.5),
        new("Parque oscuro donde roban de noche",
            "El parque no tiene iluminación y en las últimas semanas han robado celulares a varios vecinos que pasan de noche.",
            CategoriasIncidencia.Seguridad, "Parque Las Palmas", -12.1020m, -76.9440m, EstadosIncidencia.EnRevision, 11.2),
        new("Grafitis en la pared del colegio",
            "Pintaron grafitis en toda la pared exterior del colegio durante el fin de semana. Algunos tienen palabras ofensivas.",
            CategoriasIncidencia.Otros, "Calle Las Gardenias 120", -12.0985m, -76.9480m, EstadosIncidencia.Pendiente, 13.4),
        new("Paso de cebra borrado frente al mercado",
            "Las líneas del paso de cebra ya no se ven. Muchas personas cruzan con bolsas y los autos no se detienen.",
            CategoriasIncidencia.Transito, "Av. La Molina 590, frente al mercado", -12.0920m, -76.9400m, EstadosIncidencia.Pendiente, 3.6),
        new("Áreas verdes sin riego",
            "El pasto del parque está seco y amarillo desde hace semanas. Parece que el riego dejó de pasar.",
            CategoriasIncidencia.Parques, "Parque Los Sauces", null, null, EstadosIncidencia.Pendiente, 16.3),
        new("Ruidos molestos de un local nocturno",
            "Un local pone música a todo volumen hasta las 3 de la mañana de jueves a domingo. Los vecinos no podemos dormir.",
            CategoriasIncidencia.Otros, "Av. Javier Prado Este 5800", null, null, EstadosIncidencia.EnRevision, 6.1),
        new("Rampa para silla de ruedas destruida",
            "La rampa de la esquina está rota y tiene un desnivel grande. Una persona en silla de ruedas no puede subir a la vereda.",
            CategoriasIncidencia.PistasVeredas, "Av. Los Fresnos con Calle Las Magnolias", -12.0815m, -76.9395m, EstadosIncidencia.Atendida, 22.2),
        new("Luminaria que parpadea toda la noche",
            "El foco del poste se prende y se apaga toda la noche. Molesta a los vecinos y la calle queda casi a oscuras.",
            CategoriasIncidencia.Alumbrado, "Calle Los Nogales 275", -12.0880m, -76.9340m, EstadosIncidencia.Pendiente, 2.1),
    ];

    // Devuelve cuántas incidencias creó (0 si la tabla no estaba vacía o la demo está desactivada).
    public static async Task<int> SeedAsync(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IncidenciasDemo");
        var context = services.GetRequiredService<ApplicationDbContext>();

        if (string.Equals(configuration["INCIDENCIAS_DEMO"]?.Trim(), "false", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Incidencias de demostración desactivadas (INCIDENCIAS_DEMO=false).");
            return 0;
        }

        if (await context.Incidencias.AnyAsync())
        {
            return 0;
        }

        // Dueños: el ciudadano de prueba (USER_EMAIL) para algunas, así "Mis incidencias" tiene datos;
        // los cambios de estado los hace el administrador inicial (ADMIN_EMAIL). Si no existen, quedan sin usuario.
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var ciudadano = await BuscarUsuarioAsync(userManager, configuration["USER_EMAIL"]);
        var administrador = await BuscarUsuarioAsync(userManager, configuration["ADMIN_EMAIL"]);

        var ahora = DateTime.Now;
        foreach (var demo in Incidencias)
        {
            var registro = ahora.AddDays(-demo.DiasAtras);
            var dueno = demo.DelUsuarioPrueba ? ciudadano?.Id : null;

            var incidencia = new Incidencia
            {
                Titulo = demo.Titulo,
                Descripcion = demo.Descripcion,
                Categoria = demo.Categoria,
                Ubicacion = demo.Ubicacion,
                Latitud = demo.Latitud,
                Longitud = demo.Longitud,
                Estado = demo.Estado,
                FechaRegistro = registro,
                UsuarioId = dueno
            };

            // Historial coherente con el estado: creación y, si corresponde, los cambios del equipo municipal.
            incidencia.Historial.Add(new HistorialEstadoIncidencia
            {
                EstadoAnterior = null,
                EstadoNuevo = EstadosIncidencia.Pendiente,
                FechaCambio = registro,
                UsuarioId = dueno
            });

            if (demo.Estado != EstadosIncidencia.Pendiente)
            {
                incidencia.Historial.Add(new HistorialEstadoIncidencia
                {
                    EstadoAnterior = EstadosIncidencia.Pendiente,
                    EstadoNuevo = EstadosIncidencia.EnRevision,
                    FechaCambio = registro.AddDays(Math.Min(1.5, demo.DiasAtras / 3)),
                    UsuarioId = administrador?.Id
                });
            }

            if (demo.Estado == EstadosIncidencia.Atendida)
            {
                incidencia.Historial.Add(new HistorialEstadoIncidencia
                {
                    EstadoAnterior = EstadosIncidencia.EnRevision,
                    EstadoNuevo = EstadosIncidencia.Atendida,
                    FechaCambio = registro.AddDays(Math.Min(5, demo.DiasAtras * 2 / 3)),
                    UsuarioId = administrador?.Id
                });
            }

            // Se agregan en orden: así reciben los Id 1, 2, 3... (siempre los mismos en cada reinicio).
            context.Incidencias.Add(incidencia);
            await context.SaveChangesAsync();
        }

        logger.LogInformation(
            "Incidencias de demostración creadas: {Cantidad} ({ConDueno} del usuario de prueba). Desactívalas con INCIDENCIAS_DEMO=false.",
            Incidencias.Length, ciudadano == null ? 0 : Incidencias.Count(d => d.DelUsuarioPrueba));
        return Incidencias.Length;
    }

    private static async Task<ApplicationUser?> BuscarUsuarioAsync(UserManager<ApplicationUser> userManager, string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : await userManager.FindByEmailAsync(email.Trim());
}
