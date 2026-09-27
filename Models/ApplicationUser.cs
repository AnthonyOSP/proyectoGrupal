using Microsoft.AspNetCore.Identity;

namespace proyectoGrupal.Models;

// Usuario de Alerta Vecinal (tabla "AspNetUsers").
// IdentityUser ya incluye Id, UserName, Email y PasswordHash, entre otros.
// La contraseña nunca se guarda en texto plano: Identity guarda solo su hash (PasswordHasher).
// Por ahora no necesita propiedades propias; se agregarán aquí en etapas posteriores.
public class ApplicationUser : IdentityUser
{
}
