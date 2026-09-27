using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace proyectoGrupal.Models;

// Usuario de Alerta Vecinal (tabla "AspNetUsers").
// IdentityUser ya incluye Id, UserName, Email, PasswordHash y PhoneNumber (el teléfono del perfil), entre otros.
// La contraseña nunca se guarda en texto plano: Identity guarda solo su hash (PasswordHasher).
public class ApplicationUser : IdentityUser
{
    // Datos del perfil. Son opcionales: las cuentas se crean solo con correo y contraseña.
    [StringLength(60)]
    public string? Nombres { get; set; }

    [StringLength(60)]
    public string? Apellidos { get; set; }

    // Incidencias que reportó este usuario (relación 1 usuario → N incidencias).
    public ICollection<Incidencia> Incidencias { get; set; } = new List<Incidencia>();

    // Registros de historial hechos por este usuario (1 usuario → N cambios de estado).
    public ICollection<HistorialEstadoIncidencia> CambiosDeEstado { get; set; } = new List<HistorialEstadoIncidencia>();
}
