namespace proyectoGrupal.Services.Asistente;

// Configuración del asistente virtual (ver Program.cs y README, sección 17).
//   GROQ_API_KEY: clave de Groq (obligatoria para que el chat aparezca).
//   GROQ_MODEL:   modelo de Groq (opcional). Debe soportar llamadas a funciones (tool calling).
// Groq usa la misma API que OpenAI, así que Semantic Kernel se conecta con su conector de OpenAI.
public class AsistenteOptions
{
    public const string BaseUrlGroq = "https://api.groq.com/openai/v1";

    public string? ApiKey { get; set; }

    public string Modelo { get; set; } = "openai/gpt-oss-120b";

    // Límite de mensajes por persona (usuario con sesión, o IP si no inició sesión), contra el abuso
    // de la cuota de Groq. Opcionales: ASISTENTE_LIMITE_MENSAJES y ASISTENTE_LIMITE_MINUTOS.
    public int LimiteMensajes { get; set; } = 20;

    public int LimiteMinutos { get; set; } = 10;

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(ApiKey);
}
