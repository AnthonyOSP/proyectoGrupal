using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using proyectoGrupal.Models;

namespace proyectoGrupal.Services.Zonas;

// ===== CLUSTERING (K-Means): agrupar incidencias cercanas en "zonas críticas" =====
// Aprendizaje NO supervisado: no hay datos de entrenamiento ni archivo de modelo. Cada vez que el
// administrador abre la página se agrupan las incidencias actuales por su ubicación (milisegundos).

// Una incidencia con coordenadas, lista para agrupar.
public record PuntoZona(int Id, string Titulo, string Categoria, string Estado, double Lat, double Lng);

// Un grupo de incidencias cercanas.
public record ZonaCritica(
    int Numero,
    double Lat,                     // centro de la zona
    double Lng,
    int RadioMetros,                // distancia del centro a la incidencia más lejana del grupo
    IReadOnlyList<PuntoZona> Incidencias,
    string CategoriaPrincipal,
    int CantidadCategoriaPrincipal,
    int Abiertas)                   // pendientes o en revisión
{
    public int Total => Incidencias.Count;
}

public record ResultadoZonas(
    IReadOnlyList<ZonaCritica> Zonas,
    int NumeroDeZonas,
    bool Automatico,                // true si el número de zonas lo eligió el índice Davies-Bouldin
    double? DaviesBouldin);         // menor = zonas más compactas y separadas

public interface IZonasCriticasService
{
    // Mínimo de incidencias con ubicación para que tenga sentido agrupar.
    const int MinimoPuntos = 4;
    const int MaximoZonas = 8;

    // numeroDeZonas null = automático. Devuelve zonas vacías si hay menos de MinimoPuntos.
    ResultadoZonas Calcular(IReadOnlyList<PuntoZona> puntos, int? numeroDeZonas);
}

public class ZonasCriticasService : IZonasCriticasService
{
    // En modo automático, si todas las incidencias están a esta distancia del centro, se muestran como una sola zona.
    private const int RadioZonaUnicaMetros = 400;

    // Entrada de K-Means: la posición en kilómetros (x = este-oeste, y = norte-sur).
    private class PuntoKm
    {
        [VectorType(2)]
        public float[] Features { get; set; } = new float[2];
    }

    private class ZonaPredicha
    {
        // Número de zona (1, 2, 3…) que eligió K-Means.
        [ColumnName("PredictedLabel")]
        public uint Zona { get; set; }
    }

    public ResultadoZonas Calcular(IReadOnlyList<PuntoZona> puntos, int? numeroDeZonas)
    {
        if (puntos.Count < IZonasCriticasService.MinimoPuntos)
        {
            return new ResultadoZonas(Array.Empty<ZonaCritica>(), 0, numeroDeZonas == null, null);
        }

        // Automático y todas las incidencias juntas (en un radio pequeño): es UNA sola zona.
        // Davies-Bouldin no puede evaluar "1 grupo", así que sin esta regla siempre partiría en 2 o más.
        var todas = CrearZona(puntos.ToList()) with { Numero = 1 };
        if (numeroDeZonas == null && todas.RadioMetros <= RadioZonaUnicaMetros)
        {
            return new ResultadoZonas(new[] { todas }, 1, true, null);
        }

        // Latitud/longitud → kilómetros alrededor del centro de los puntos. Sin esto K-Means mediría
        // "grados", y un grado de longitud no mide lo mismo que uno de latitud.
        var lat0 = puntos.Average(p => p.Lat);
        var lng0 = puntos.Average(p => p.Lng);
        var kmPorGradoLng = 111.320 * Math.Cos(lat0 * Math.PI / 180);
        const double KmPorGradoLat = 110.574;
        var enKm = puntos
            .Select(p => new PuntoKm { Features = new[] { (float)((p.Lng - lng0) * kmPorGradoLng), (float)((p.Lat - lat0) * KmPorGradoLat) } })
            .ToList();

        var ctx = new MLContext(seed: 1);
        var datos = ctx.Data.LoadFromEnumerable(enKm);

        // Como máximo la mitad de los puntos: zonas de 1 incidencia no dicen nada.
        var maximo = Math.Min(IZonasCriticasService.MaximoZonas, puntos.Count / 2);
        int k;
        double? daviesBouldin;
        ITransformer modelo;

        if (numeroDeZonas is int pedido)
        {
            k = Math.Clamp(pedido, 2, Math.Max(2, maximo));
            (modelo, daviesBouldin) = Entrenar(ctx, datos, k);
        }
        else
        {
            // Automático: se prueba de 2 a "maximo" zonas y se queda la de menor índice Davies-Bouldin.
            (k, modelo, daviesBouldin) = Enumerable.Range(2, Math.Max(1, maximo - 1))
                .Select(n => { var (m, db) = Entrenar(ctx, datos, n); return (n, m, db); })
                .OrderBy(r => r.db ?? double.MaxValue)
                .First();
        }

        // A qué zona pertenece cada incidencia.
        var zonasPorPunto = ctx.Data
            .CreateEnumerable<ZonaPredicha>(modelo.Transform(datos), reuseRowObject: false)
            .Select(z => z.Zona)
            .ToList();

        var zonas = puntos
            .Select((p, i) => (Punto: p, Zona: zonasPorPunto[i]))
            .GroupBy(x => x.Zona, x => x.Punto)
            .Select(g => CrearZona(g.ToList()))
            // Primero las zonas con más incidencias abiertas: son las que requieren atención.
            .OrderByDescending(z => z.Abiertas).ThenByDescending(z => z.Total)
            .Select((z, i) => z with { Numero = i + 1 })
            .ToList();

        return new ResultadoZonas(zonas, zonas.Count, numeroDeZonas == null, daviesBouldin);
    }

    private static (ITransformer Modelo, double? DaviesBouldin) Entrenar(MLContext ctx, IDataView datos, int k)
    {
        var kmeans = ctx.Clustering.Trainers.KMeans(new KMeansTrainer.Options
        {
            FeatureColumnName = nameof(PuntoKm.Features),
            NumberOfClusters = k,
            InitializationAlgorithm = KMeansTrainer.InitializationAlgorithm.KMeansPlusPlus,
            MaximumNumberOfIterations = 100
        });
        var modelo = kmeans.Fit(datos);

        try
        {
            var metricas = ctx.Clustering.Evaluate(modelo.Transform(datos), scoreColumnName: "Score", featureColumnName: nameof(PuntoKm.Features));
            return (modelo, double.IsFinite(metricas.DaviesBouldinIndex) ? metricas.DaviesBouldinIndex : null);
        }
        catch (Exception)
        {
            // Por ejemplo, si todos los puntos están en el mismo lugar: se usa el modelo igual.
            return (modelo, null);
        }
    }

    private static ZonaCritica CrearZona(List<PuntoZona> incidencias)
    {
        var lat = incidencias.Average(p => p.Lat);
        var lng = incidencias.Average(p => p.Lng);
        var radio = incidencias.Max(p => DistanciaMetros(lat, lng, p.Lat, p.Lng));

        var principal = incidencias
            .GroupBy(p => p.Categoria)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .First();

        return new ZonaCritica(
            Numero: 0,
            Lat: lat,
            Lng: lng,
            RadioMetros: Math.Max(60, (int)Math.Ceiling(radio / 10) * 10),   // mínimo 60 m para que se vea en el mapa
            Incidencias: incidencias,
            CategoriaPrincipal: principal.Key,
            CantidadCategoriaPrincipal: principal.Count(),
            Abiertas: incidencias.Count(p => p.Estado != EstadosIncidencia.Atendida));
    }

    // Distancia aproximada en metros (suficiente para distancias de un distrito).
    private static double DistanciaMetros(double lat1, double lng1, double lat2, double lng2)
    {
        var x = (lng2 - lng1) * 111_320 * Math.Cos((lat1 + lat2) / 2 * Math.PI / 180);
        var y = (lat2 - lat1) * 110_574;
        return Math.Sqrt(x * x + y * y);
    }
}
