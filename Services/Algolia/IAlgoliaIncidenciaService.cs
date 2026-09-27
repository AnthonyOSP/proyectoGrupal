using proyectoGrupal.Models;

namespace proyectoGrupal.Services.Algolia;

// Resultado de una búsqueda en Algolia: solo los Id encontrados, en orden de relevancia.
// Las incidencias reales se leen después de SQLite (fuente de verdad).
public record ResultadoBusquedaAlgolia(bool Exito, IReadOnlyList<int> Ids)
{
    public static ResultadoBusquedaAlgolia Fallo() => new(false, Array.Empty<int>());
}

public record ResultadoSincronizacion(bool Exito, int Indexadas, string Mensaje);

// Índice de búsqueda de incidencias en Algolia.
// Ningún método lanza excepciones por fallos de Algolia: devuelven false y registran el problema,
// para que un error de Algolia nunca afecte a los datos guardados en SQLite.
public interface IAlgoliaIncidenciaService
{
    bool EstaConfigurado { get; }

    string NombreIndice { get; }

    // Crea o reemplaza el registro de la incidencia (nueva incidencia o cambio de estado).
    Task<bool> IndexarAsync(Incidencia incidencia, CancellationToken cancellationToken = default);

    // Quita la incidencia del índice (preparado para cuando exista la eliminación de incidencias).
    Task<bool> EliminarAsync(int incidenciaId, CancellationToken cancellationToken = default);

    // Busca texto libre respetando los filtros exactos de categoría y estado.
    Task<ResultadoBusquedaAlgolia> BuscarIdsAsync(string texto, string? categoria, string? estado, CancellationToken cancellationToken = default);

    // Reemplaza todo el índice con las incidencias recibidas (sin duplicados: objectID = Id).
    Task<ResultadoSincronizacion> SincronizarTodoAsync(IReadOnlyCollection<Incidencia> incidencias, CancellationToken cancellationToken = default);
}
