using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Models;

namespace proyectoGrupal.Data;

// Mensajes de contacto de demostración (5 positivos y 5 negativos).
// Igual que IncidenciasDemoSeeder: solo si la tabla MensajesContacto está VACÍA al iniciar (en Render, después
// de cada reinicio), y se desactiva con INCIDENCIAS_DEMO=false.
// Se guardan SIN sentimiento: justo después, Program.cs analiza los mensajes pendientes con el modelo de ML.NET,
// así la bandeja del panel muestra la clasificación real del modelo (que el administrador puede corregir).
public static class MensajesDemoSeeder
{
    private record Demo(string Nombre, string Email, string Mensaje, double DiasAtras);

    private static readonly Demo[] Mensajes =
    [
        // Positivos
        new("María Torres", "maria.torres@example.com",
            "Muchas gracias por arreglar el poste de mi calle tan rápido. Ahora la cuadra está iluminada y los vecinos nos sentimos más seguros. ¡Excelente trabajo!", 1.3),
        new("Carlos Huamán", "carlos.huaman@example.com",
            "Felicitaciones por la plataforma, es muy fácil de usar. Reporté un bache desde el celular en dos minutos y pude ver cuando pasó a revisión.", 3.6),
        new("Rosa Quispe", "rosa.quispe@example.com",
            "Quería agradecer al equipo de limpieza: retiraron el desmonte de la berma al día siguiente de mi reporte. Me encantó la atención.", 5.2),
        new("Jorge Mendoza", "jorge.mendoza@example.com",
            "Excelente iniciativa. El asistente del chat me ayudó a preparar mi reporte y me avisó que ya existía uno parecido. Muy útil, gracias.", 8.4),
        new("Lucía Fernández", "lucia.fernandez@example.com",
            "Gracias por reparar los juegos del parque Los Fresnos. Mis hijos ya pueden jugar tranquilos. Sigan así, buen trabajo.", 11.7),

        // Negativos
        new("Pedro Salazar", "pedro.salazar@example.com",
            "Reporté hace tres semanas una fuga de agua y sigue igual. Nadie viene a revisar, se pierde muchísima agua. Es una vergüenza que no hagan nada.", 0.6),
        new("Ana Rojas", "ana.rojas@example.com",
            "Pésimo servicio. La basura de mi esquina lleva días acumulándose, hay mal olor y ratas. Ya reporté dos veces y el estado sigue en pendiente.", 2.4),
        new("Luis Paredes", "luis.paredes@example.com",
            "Estoy muy molesto: el semáforo del cruce estuvo malogrado una semana entera y casi hay un accidente. No entiendo cómo tardan tanto en atender algo tan peligroso.", 4.9),
        new("Carmen Vega", "carmen.vega@example.com",
            "No puedo subir la foto de mi reporte, la página me da error y no me deja enviarlo. Es muy frustrante, perdí todo lo que había escrito.", 7.1),
        new("Ricardo Chávez", "ricardo.chavez@example.com",
            "El parque Las Palmas sigue completamente oscuro y anoche asaltaron a otro vecino. Reportamos esto hace tiempo y nadie hace nada. Muy decepcionante.", 13.2),
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
        context.MensajesContacto.AddRange(Mensajes.Select(m => new MensajeContacto
        {
            Nombre = m.Nombre,
            Email = m.Email,
            Mensaje = m.Mensaje,
            FechaRegistro = ahora.AddDays(-m.DiasAtras)
            // Sentimiento: lo pone el modelo de ML.NET al iniciar (SentimientoService.AnalizarMensajesAsync).
        }));
        await context.SaveChangesAsync();

        services.GetRequiredService<ILoggerFactory>().CreateLogger("MensajesDemo")
            .LogInformation("Mensajes de contacto de demostración creados: {Cantidad}.", Mensajes.Length);
        return Mensajes.Length;
    }
}
