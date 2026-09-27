using Microsoft.AspNetCore.Identity;

namespace proyectoGrupal.Models;

// Usuario de Alerta Vecinal (tabla "AspNetUsers").
// IdentityUser ya incluye Id, UserName, Email y PasswordHash, entre otros.
// La contraseña nunca se guarda en texto plano: Identity guarda solo su hash (PasswordHasher).
public class ApplicationUser : IdentityUser
{
    // Incidencias que reportó este usuario (relación 1 usuario → N incidencias).
    public ICollection<Incidencia> Incidencias { get; set; } = new List<Incidencia>();
}
