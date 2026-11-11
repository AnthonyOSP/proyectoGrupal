using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Models;

namespace proyectoGrupal.Data;

// Mensajes de contacto de demostración (5 positivos y 5 negativos).
// Igual que IncidenciasDemoSeeder: solo si la tabla MensajesContacto está VACÍA al iniciar (en Render, después
// de cada reinicio), y se desactiva con INCIDENCIAS_DEMO=false.
// 6 de los 10 quedan con la fecha del día en que arranca la aplicación (la bandeja del panel muestra los de hoy)
// y los otros 4 en días anteriores. Se guardan SIN sentimiento: justo después, Program.cs analiza los mensajes pendientes con el modelo de ML.NET,
// así la bandeja del panel muestra la clasificación real del modelo (que el administrador puede corregir).
public static class MensajesDemoSeeder
{
    // MinutosAtras: cuánto antes del arranque se escribió. Para los de HOY se reparten dentro del día
    // (nunca antes de las 00:00), así siempre aparecen en el reporte del día.
    private record Demo(string Nombre, string Email, string Mensaje, int DiasAtras, int MinutosAtras);

    private static readonly Demo[] Mensajes =
    [
        // Positivos
        new("María Torres", "maria.torres@example.com",
            "Muchas gracias por arreglar el poste de mi calle tan rápido. Ahora la cuadra está iluminada y los vecinos nos sentimos más seguros. ¡Excelente trabajo!", 0, 25),
        new("Carlos Huamán", "carlos.huaman@example.com",
            "Felicitaciones por la plataforma, es muy fácil de usar. Reporté un bache desde el celular en dos minutos y pude ver cuando pasó a revisión.", 0, 140),
        new("Rosa Quispe", "rosa.quispe@example.com",
            "Quería agradecer al equipo de limpieza: retiraron el desmonte de la berma al día siguiente de mi reporte. Me encantó la atención.", 2, 0),
        new("Jorge Mendoza", "jorge.mendoza@example.com",
            "Excelente iniciativa. El asistente del chat me ayudó a preparar mi reporte y me avisó que ya existía uno parecido. Muy útil, gracias.", 0, 260),
        new("Lucía Fernández", "lucia.fernandez@example.com",
            "Gracias por reparar los juegos del parque Los Fresnos. Mis hijos ya pueden jugar tranquilos. Sigan así, buen trabajo.", 5, 0),

        // Negativos
        new("Pedro Salazar", "pedro.salazar@example.com",
            "Reporté hace tres semanas una fuga de agua y sigue igual. Nadie viene a revisar, se pierde muchísima agua. Es una vergüenza que no hagan nada.", 0, 10),
        new("Ana Rojas", "ana.rojas@example.com",
            "Pésimo servicio. La basura de mi esquina lleva días acumulándose, hay mal olor y ratas. Ya reporté dos veces y el estado sigue en pendiente.", 0, 85),
        new("Luis Paredes", "luis.paredes@example.com",
            "Estoy muy molesto: el semáforo del cruce estuvo malogrado una semana entera y casi hay un accidente. No entiendo cómo tardan tanto en atender algo tan peligroso.", 1, 0),
        new("Carmen Vega", "carmen.vega@example.com",
            "No puedo subir la foto de mi reporte, la página me da error y no me deja enviarlo. Es muy frustrante, perdí todo lo que había escrito.", 0, 200),
        new("Ricardo Chávez", "ricardo.chavez@example.com",
            "El parque Las Palmas sigue completamente oscuro y anoche asaltaron a otro vecino. Reportamos esto hace tiempo y nadie hace nada. Muy decepcionante.", 8, 0),
    ];

    // Devuelve cuántos mensajes creó (0 si la tabla no estaba vacía o la demo está desactivada).
    public static async Task<int> SeedAsync(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var context = services.GetRequiredService<ApplicationDbContext>();

        if (string.Equals(configuration["INCIDENCIAS_DEMO"]?.Trim(), "false", StringComparison.OrdinalIgnoreCase) ||
            await context.MensajesContacto.AnyAsync())
        {
            return 0;
        }

        var ahora = DateTime.Now;
        var minutosDeHoy = (int)(ahora - ahora.Date).TotalMinutes;
        context.MensajesContacto.AddRange(Mensajes.Select(m => new MensajeContacto
        {
            Nombre = m.Nombre,
            Email = m.Email,
            Mensaje = m.Mensaje,
            FechaRegistro = m.DiasAtras == 0
                // Hoy: como mucho tantos minutos como lleva el día (si la app arranca a las 00:30, quedan entre 00:00 y 00:30).
                ? ahora.AddMinutes(-Math.Min(m.MinutosAtras, Math.Max(0, minutosDeHoy - 1)))
                // Días anteriores: a media mañana o media tarde de ese día.
                : ahora.Date.AddDays(-m.DiasAtras).AddHours(10 + m.DiasAtras % 7)
            // Sentimiento: lo pone el modelo de ML.NET al iniciar (SentimientoService.AnalizarMensajesAsync).
        }));
        await context.SaveChangesAsync();

        services.GetRequiredService<ILoggerFactory>().CreateLogger("MensajesDemo")
            .LogInformation("Mensajes de contacto de demostración creados: {Cantidad}.", Mensajes.Length);
        return Mensajes.Length;
    }
}
