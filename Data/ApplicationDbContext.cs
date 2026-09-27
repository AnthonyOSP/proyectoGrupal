using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Models;

namespace proyectoGrupal.Data;

// Punto de acceso a la base de datos SQLite.
// Se registra en Program.cs y se recibe en los controladores por inyección de dependencias.
// Hereda de IdentityDbContext para que las tablas de usuarios y roles (AspNetUsers, AspNetRoles, ...)
// vivan en el mismo archivo SQLite que las incidencias.
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Tabla "Incidencias"
    public DbSet<Incidencia> Incidencias { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Obligatorio: configura las tablas de Identity.
        base.OnModelCreating(builder);
    }
}
