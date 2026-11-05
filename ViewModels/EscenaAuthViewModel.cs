namespace proyectoGrupal.ViewModels;

// Textos del lado izquierdo (mapa animado) de las páginas de cuenta: ver Views/Shared/_EscenaDistrito.cshtml.
// Titulo y Destacado forman el titular en dos líneas; Texto va debajo.
public record EscenaAuthViewModel(string Titulo, string Destacado, string Texto);
