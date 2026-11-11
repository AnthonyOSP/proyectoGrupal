namespace proyectoGrupal.Services.Asistente;

// Lo que las herramientas del agente encontraron durante UNA respuesta (scoped: una por petición).
// Al terminar el texto, el controlador lo envía al navegador como mensaje de control.
public sealed class SesionAsistente
{
    private const int MaximoTarjetas = 5;

    private readonly List<string> _fuentes = [];
    private readonly List<TarjetaIncidencia> _incidencias = [];

    public IReadOnlyList<string> Fuentes => _fuentes;

    public IReadOnlyList<TarjetaIncidencia> Incidencias => _incidencias;

    public BorradorReporte? Borrador { get; private set; }

    public bool TieneControl => _fuentes.Count > 0 || _incidencias.Count > 0 || Borrador != null;

    public void AgregarFuente(string documento)
    {
        if (!string.IsNullOrWhiteSpace(documento) && !_fuentes.Contains(documento, StringComparer.OrdinalIgnoreCase))
        {
            _fuentes.Add(documento);
        }
    }

    public void AgregarIncidencia(TarjetaIncidencia tarjeta)
    {
        if (_incidencias.Count < MaximoTarjetas && _incidencias.All(t => t.Id != tarjeta.Id))
        {
            _incidencias.Add(tarjeta);
        }
    }

    public void PonerBorrador(BorradorReporte borrador) => Borrador = borrador;

    public MensajeControlAsistente Control() => new()
    {
        Fuentes = _fuentes,
        Incidencias = _incidencias,
        Borrador = Borrador
    };
}
