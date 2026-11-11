using System.ComponentModel.DataAnnotations;

namespace proyectoGrupal.Models;

// Valoración (👍/👎) de una respuesta del asistente virtual (tabla "ValoracionesAsistente").
// La pregunta y la respuesta las copia el servidor desde la sesión: el navegador solo envía el Id
// de la respuesta y si fue útil. No se guarda quién valoró (no hace falta para mejorar la ayuda).
public class ValoracionAsistente
{
    public const int PreguntaMaximo = 1000;
    public const int RespuestaMaximo = 4000;
    public const int FuentesMaximo = 500;

    public int Id { get; set; }

    // Id de la respuesta (SesionAsistente.RespuestaId). Único: votar otra vez cambia el voto.
    [Required]
    [StringLength(32)]
    public string RespuestaId { get; set; } = string.Empty;

    [Required]
    [StringLength(PreguntaMaximo)]
    public string Pregunta { get; set; } = string.Empty;

    [Required]
    [StringLength(RespuestaMaximo)]
    public string Respuesta { get; set; } = string.Empty;

    // Documentos de ayuda que usó la respuesta, separados por " · " (vacío si no usó ninguno).
    [StringLength(FuentesMaximo)]
    public string Fuentes { get; set; } = string.Empty;

    public bool Util { get; set; }

    public DateTime Fecha { get; set; }
}
