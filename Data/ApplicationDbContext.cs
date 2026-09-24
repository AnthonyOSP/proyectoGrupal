using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Models;

namespace proyectoGrupal.Data;

// Punto de acceso a la base de datos SQLite.
// Se registra en Program.cs y se recibe en los controladores por inyección de dependencias.
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Tabla "Incidencias"
    public DbSet<Incidencia> Incidencias { get; set; }
}
