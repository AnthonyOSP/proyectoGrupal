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

    // Tabla "HistorialEstadosIncidencia"
    public DbSet<HistorialEstadoIncidencia> HistorialEstadosIncidencia { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Obligatorio: configura las tablas de Identity.
        base.OnModelCreating(builder);

        // ApplicationUser 1 ── N Incidencia.
        // UsuarioId es opcional: las incidencias anteriores a esta relación no tienen usuario.
        // Si se elimina un usuario, sus incidencias se conservan (son reportes públicos) y quedan sin usuario.
        builder.Entity<Incidencia>()
            .HasOne(i => i.Usuario)
            .WithMany(u => u.Incidencias)
            .HasForeignKey(i => i.UsuarioId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Incidencia 1 ── N HistorialEstadoIncidencia.
        // El historial pertenece a la incidencia: si algún día se borra la incidencia, se borra su historial.
        builder.Entity<HistorialEstadoIncidencia>()
            .HasOne(h => h.Incidencia)
            .WithMany(i => i.Historial)
            .HasForeignKey(h => h.IncidenciaId)
            .OnDelete(DeleteBehavior.Cascade);

        // ApplicationUser 1 ── N HistorialEstadoIncidencia (quién hizo cada cambio).
        // Si se elimina la cuenta, el historial se conserva y queda sin usuario.
        builder.Entity<HistorialEstadoIncidencia>()
            .HasOne(h => h.Usuario)
            .WithMany(u => u.CambiosDeEstado)
            .HasForeignKey(h => h.UsuarioId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
