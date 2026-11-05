# Análisis de sentimientos de los mensajes de contacto (ML.NET)

Modelo que clasifica cada mensaje de la página **Contáctanos** como **positivo** o **negativo**.
El administrador ve el resultado en **Panel administrativo → Mensajes** (reporte del día), puede
corregirlo a mano y reentrenar el modelo con esas correcciones.

## Cómo funciona

| Parte | Dónde |
| --- | --- |
| Datos de entrenamiento (`Comentario,Label` · 1 = positivo, 0 = negativo) | `trainingData/comentarios_entrenamiento.csv` |
| Pipeline y entrenamiento (compartido por la consola y el panel) | `Services/Sentimiento/EntrenadorSentimiento.cs` |
| Clases de entrada y salida del modelo | `Services/Sentimiento/SentimientoModelos.cs` |
| Programa de consola que entrena solo con el CSV | `Entrenamiento/Program.cs` |
| Modelo entrenado y sus datos (fecha, ejemplos, exactitud) | `MLModels/SentimientoModel.zip` y `.json` |
| Consumo y reentrenamiento en la web | `Services/Sentimiento/SentimientoService.cs` |
| Se analiza cada mensaje al guardarlo | `HomeController.Contacto` (POST) |
| Corrección manual y botón "Reentrenar modelo" | `AdminController.CambiarSentimiento` y `ReentrenarModelo` |

Pipeline de ML.NET: `FeaturizeText` (texto → números: minúsculas, sin tildes, n-gramas de palabras y
de caracteres) + `SdcaLogisticRegression` (clasificación binaria). Se evalúa con un 20% de los datos
reservado y luego se entrena el modelo final con todos los datos.

## Mejorar el modelo desde el panel

1. En **Panel → Mensajes**, si un mensaje está mal clasificado, presiona **Positivo** o **Negativo**
   en ese mensaje. Queda marcado como *corregido* y el modelo ya no lo cambia.
   También puedes presionar el sentimiento que ya tiene para *confirmarlo*.
2. Presiona **Reentrenar modelo**. Se entrena con el CSV **más** todos los mensajes marcados a mano,
   se empieza a usar al instante (sin reiniciar) y se vuelven a analizar los mensajes que clasificó
   el modelo.

Ejemplo probado: "No tengo ninguna queja, el servicio es bueno" salía negativo (2% positivo).
Después de marcarlo como positivo y reentrenar, una frase nueva parecida —"No tengo ninguna queja,
la atención es buena"— salió positiva (74%).

## Volver a entrenar desde la consola

Cuando cambien los datos de `comentarios_entrenamiento.csv`, desde la raíz del repositorio:

```
dotnet run --project ML/sentimentAnalysisNLP/Entrenamiento
```

Este programa usa **solo el CSV** (las correcciones del panel están en la base de datos). Después de
usarlo, presiona **Reentrenar modelo** en el panel si quieres volver a sumar las correcciones.

## Tener en cuenta

- El botón **Reentrenar modelo** reemplaza `MLModels/SentimientoModel.zip`. En tu computadora eso
  cambia el archivo del repositorio: súbelo a git solo si quieres compartir ese modelo con el grupo.
- En Render el disco se borra en cada despliegue: el modelo vuelve al que está en el repositorio y
  las correcciones se pierden junto con la base de datos.
- Es un modelo binario: un mensaje neutral (por ejemplo, una consulta) igual sale positivo o negativo.
  Agregar al CSV más mensajes reales etiquetados es lo que más mejora la precisión.
