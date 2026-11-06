# Sugerencia de categoría al reportar (ML.NET · clasificación multiclase)

Mientras el vecino escribe el título y la descripción en **Reportar incidencia**, un modelo de
ML.NET le sugiere una de las 8 categorías: *"Según lo que escribiste, parece **Alumbrado público**
(91% de confianza) · [Elegir esta categoría]"*. Nunca se elige sola: el vecino decide.

## Cómo funciona

| Parte | Dónde |
| --- | --- |
| Datos de entrenamiento (`Texto,Categoria`, ~40 por categoría) | `trainingData/incidencias_entrenamiento.csv` |
| Pipeline, clases de entrada/salida y entrenamiento (compartido) | `Services/Categorias/EntrenadorCategorias.cs` |
| Programa de consola que entrena solo con el CSV | `Entrenamiento/Program.cs` |
| Modelo entrenado y sus datos | `MLModels/CategoriaModel.zip` y `.json` |
| Consumo y reentrenamiento en la web | `Services/Categorias/CategoriaSugeridaService.cs` |
| Endpoint que consulta el formulario | `HomeController.SugerirCategoria` (POST, con sesión y token) |
| Caja de sugerencia | `Views/Home/Reportar.cshtml` + `wwwroot/js/reportar.js` (sección 6) |

Pipeline: `MapValueToKey` (categoría → clase) + `FeaturizeText` (texto → números) +
`SdcaMaximumEntropy` (elige 1 de 8 clases) + `MapKeyToValue` (clase → nombre).
Se evalúa con **validación cruzada de 5 particiones** (con pocos datos es más estable que un 80/20).

La sugerencia solo aparece si el texto tiene al menos 8 caracteres y el modelo tiene **40% o más de
confianza**: con textos ambiguos ("Algo raro, no sé qué es") no sugiere nada.

## Entrenar

Desde la raíz del repositorio (solo el CSV):

```
dotnet run --project ML/categorizacionIncidencias/Entrenamiento
```

O en **Panel → Dashboard → Sugerencia de categorías → Reentrenar con las incidencias**: suma el
título y la descripción de las incidencias registradas con la categoría que eligió el vecino, y el
modelo nuevo se usa al instante. En tu computadora esto reemplaza `MLModels/CategoriaModel.zip`.

## Precisión actual

Con 324 ejemplos: **67.5%** en validación cruzada (al azar sería 12.5% con 8 categorías). Los
reportes claros salen con más de 85% de confianza; los mixtos (por ejemplo, "Otros" o "Seguridad")
son los más difíciles. Más ejemplos reales es lo que más lo mejora.
