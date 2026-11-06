# Zonas críticas (ML.NET · clustering K-Means)

En **Panel → Zonas críticas** las incidencias se agrupan por ubicación para ver dónde se concentran
los problemas: cada zona muestra su centro y radio en el mapa, cuántas incidencias tiene, cuántas
siguen abiertas y su problema principal.

## Cómo funciona

Es aprendizaje **no supervisado**: no hay datos de entrenamiento ni archivo de modelo. Cada vez que
se abre la página, `Services/Zonas/ZonasCriticasService.cs` agrupa las incidencias actuales
(milisegundos, incluso con cientos de puntos):

1. Convierte latitud/longitud a **kilómetros** (si no, K-Means mediría "grados", que no miden lo
   mismo de norte a sur que de este a oeste).
2. Entrena **K-Means** (`ctx.Clustering.Trainers.KMeans`, inicio K-Means++).
3. **Número de zonas automático:** prueba de 2 a 8 y se queda con el menor índice
   **Davies-Bouldin** (zonas más compactas y separadas). Si todas las incidencias están a menos de
   400 m, forman una sola zona. El administrador también puede elegir el número a mano.
4. Ordena las zonas por incidencias abiertas: primero las que requieren atención.

Filtros: abiertas o todas, y por categoría. Se necesitan al menos 4 incidencias con punto en el mapa.

## Datos de demostración

`datos_demo.sql` crea 38 incidencias con ubicación en La Molina, en 4 focos (mercado, Av. La Molina,
un parque y una calle con fugas de agua). Con la app detenida, desde la raíz del repositorio:

```
sqlite3 alerta_vecinal.db < ML/zonasCriticas/datos_demo.sql
```

Para borrarlas (su historial se borra en cascada):

```
sqlite3 alerta_vecinal.db "DELETE FROM Incidencias WHERE Descripcion LIKE '%[datos de demostración]';"
```

Se insertan directo en SQLite: si usas Algolia, presiona **Sincronizar todas las incidencias** en el
Dashboard. Con esos datos, K-Means encuentra solo los 4 focos (índice Davies-Bouldin 0.12).
