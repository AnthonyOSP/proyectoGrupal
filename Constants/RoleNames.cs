namespace proyectoGrupal.Constants;

// Nombres de los roles de la aplicación.
// Usar siempre estas constantes (en [Authorize], el seeder y las vistas) para no escribir
// el nombre de un rol de forma distinta en cada lugar.
public static class RoleNames
{
    // Accede al panel /Admin y cambia el estado de las incidencias.
    public const string Administrador = "Administrador";

    // Vecino registrado: puede reportar incidencias. Se asigna automáticamente al registrarse.
    public const string Ciudadano = "Ciudadano";

    public static readonly IReadOnlyList<string> Todos = [Administrador, Ciudadano];
}
