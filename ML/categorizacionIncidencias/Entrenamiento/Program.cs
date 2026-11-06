// Entrena el clasificador de categorías de incidencias (clasificación multiclase) con ML.NET.
//
// Uso (desde la raíz del repositorio):
//   dotnet run --project ML/categorizacionIncidencias/Entrenamiento
//
// Lee  ML/categorizacionIncidencias/trainingData/incidencias_entrenamiento.csv  (Texto,Categoria)
// y guarda el modelo en  MLModels/CategoriaModel.zip, que la aplicación web carga al iniciar.
//
// Usa SOLO el CSV. Para sumar las incidencias reales de la base de datos usa el botón
// "Reentrenar" del Panel administrativo (Dashboard → Sugerencia de categorías).
using Microsoft.ML;
using proyectoGrupal.Services.Categorias;

var raiz = BuscarRaizDelRepositorio();
var dataPath = Path.Combine(raiz, "ML", "categorizacionIncidencias", "trainingData", "incidencias_entrenamiento.csv");
var modelPath = Path.Combine(raiz, "MLModels", "CategoriaModel.zip");

var ctx = new MLContext(seed: 1);

Console.WriteLine($"Datos: {dataPath}");
var (modelo, esquema, info) = EntrenadorCategorias.Entrenar(ctx, dataPath, Array.Empty<EntradaCategoria>());

Console.WriteLine();
Console.WriteLine($"Ejemplos de entrenamiento: {info.EjemplosCsv}");
Console.WriteLine("Evaluación con validación cruzada (5 particiones):");
Console.WriteLine($"  Exactitud micro (aciertos sobre el total):       {info.ExactitudMicro:P1}");
Console.WriteLine($"  Exactitud macro (promedio de cada categoría):    {info.ExactitudMacro:P1}");

EntrenadorCategorias.Guardar(ctx, modelo, esquema, info, modelPath);
Console.WriteLine();
Console.WriteLine($"Modelo guardado en: {modelPath}");

// Probar el modelo guardado con reportes que NO están en el CSV.
var motor = ctx.Model.CreatePredictionEngine<EntradaCategoria, PrediccionCategoria>(ctx.Model.Load(modelPath, out _));
var clases = EntrenadorCategorias.NombresDeClases(motor);
string[] ejemplos =
{
    "Hay un hueco gigante en la calle y mi carro se malogró",
    "El poste de mi cuadra no prende hace tres días",
    "Están botando basura y desmonte en la esquina",
    "Sale agua por la pista desde la mañana",
    "El semáforo de la esquina está malogrado",
    "Nos robaron dos veces esta semana en el pasaje",
    "El árbol del parque tiene ramas a punto de caer",
    "Música a todo volumen hasta las 4 de la mañana"
};
Console.WriteLine();
Console.WriteLine("Pruebas:");
foreach (var texto in ejemplos)
{
    var p = motor.Predict(new EntradaCategoria { Texto = texto });
    var confianza = p.Score.Length == clases.Length ? p.Score[Array.IndexOf(clases, p.Categoria)] : 0f;
    Console.WriteLine($"  {p.Categoria,-24} ({confianza,4:P0})  {texto}");
}

static string BuscarRaizDelRepositorio()
{
    var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
    while (carpeta != null && !File.Exists(Path.Combine(carpeta.FullName, "proyectoGrupal.csproj")))
    {
        carpeta = carpeta.Parent;
    }
    return carpeta?.FullName
        ?? throw new InvalidOperationException("No se encontró proyectoGrupal.csproj en las carpetas superiores.");
}
