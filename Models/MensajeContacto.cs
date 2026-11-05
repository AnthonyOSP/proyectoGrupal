using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.Models;

// Un mensaje enviado desde la página "Contáctanos" (tabla "MensajesContacto").
// Cualquier visitante puede escribir, tenga o no una cuenta.
public class MensajeContacto
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Mensaje { get; set; } = string.Empty;

    // Hora del servidor (DateTime.Now), nunca del formulario. No se muestra al visitante.
    public DateTime FechaRegistro { get; set; }

    // Análisis de sentimientos (ML.NET) hecho por el servidor al guardar. Solo lo ve el administrador.
    // Valores de SentimientosMensaje.Todos, o null si el modelo no estaba disponible.
    [StringLength(10)]
    public string? Sentimiento { get; set; }

    // Probabilidad (0 a 1) de que el mensaje sea positivo, según el modelo. null si no se analizó.
    public float? ProbabilidadPositiva { get; set; }

    // true si el administrador fijó el sentimiento a mano (corrigió o confirmó al modelo).
    // Estos mensajes se usan como ejemplos al reentrenar y el modelo ya no los vuelve a clasificar.
    public bool SentimientoManual { get; set; }
}
