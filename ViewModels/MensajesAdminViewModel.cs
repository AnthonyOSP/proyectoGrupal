using proyectoGrupal.Models;

namespace proyectoGrupal.ViewModels;

// Bandeja de mensajes de contacto del panel administrativo (/Admin/Mensajes).
public class MensajesAdminViewModel
{
    // Texto buscado (ya normalizado), o null si no hay búsqueda.
    public string? Buscar { get; set; }

    // Total de mensajes guardados, sin filtrar.
    public int Total { get; set; }

    // Mensajes a mostrar (filtrados por la búsqueda), del más reciente al más antiguo.
    public List<MensajeContacto> Mensajes { get; set; } = new();

    public bool HayFiltros => Buscar != null;
}
