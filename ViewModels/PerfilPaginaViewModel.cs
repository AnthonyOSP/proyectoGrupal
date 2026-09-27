namespace proyectoGrupal.ViewModels;

// Página "Mi perfil": datos de solo lectura + los dos formularios (perfil y contraseña).
// Cada formulario se envía con su prefijo ("Perfil." y "Contrasena.") a su propia acción.
public class PerfilPaginaViewModel
{
    // Solo se muestra; nunca se recibe desde el navegador.
    public string Email { get; set; } = "";

    public bool EsAdministrador { get; set; }

    public PerfilViewModel Perfil { get; set; } = new();

    public CambiarContrasenaViewModel Contrasena { get; set; } = new();
}
