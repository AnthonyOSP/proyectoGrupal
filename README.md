# Alerta Vecinal

Plataforma web para que los vecinos de un distrito reporten incidencias de su comunidad (pistas dañadas, alumbrado, basura, fugas de agua, etc.).

Proyecto académico del curso de Programación, hecho con **ASP.NET Core MVC (.NET 10)**, **Entity Framework Core** y **SQLite**.

---

## 1. Requisitos (instalar una sola vez)

- **.NET SDK 10**: https://dotnet.microsoft.com/download/dotnet/10.0
- **Git**
- Un editor, por ejemplo **VS Code** con la extensión **C# Dev Kit**

Comprueba que .NET está instalado:

```bash
dotnet --version
```

Debe mostrar una versión que empiece con `10.`

---

## 2. Primera vez: descargar y preparar el proyecto

```bash
# 1. Clonar el repositorio
git clone https://github.com/AnthonyOSP/proyectoGrupal.git
cd proyectoGrupal

# 2. Instalar la herramienta dotnet-ef (la versión está fijada en dotnet-tools.json)
dotnet tool restore

# 3. Descargar los paquetes NuGet (Entity Framework Core, SQLite)
dotnet restore

# 4. Crear la base de datos SQLite (alerta_vecinal.db) aplicando las migraciones
dotnet tool run dotnet-ef database update

# 5. Ejecutar la aplicación
dotnet run
```

Luego abre en el navegador la dirección que aparece en la terminal, normalmente:

```text
http://localhost:5131
```

Para detener la aplicación: `Ctrl + C` en la terminal.

> **Importante:** si te saltas el paso 4, la aplicación mostrará un error `no such table: Incidencias`, porque la base de datos no existe todavía.

---

## 3. Día a día: antes de empezar a trabajar

Cada vez que un compañero suba cambios, actualiza tu copia:

```bash
git pull
dotnet tool run dotnet-ef database update
dotnet run
```

`database update` solo aplica las migraciones nuevas. Si no hay ninguna, no hace nada, así que es seguro ejecutarlo siempre.

---

## 4. Si modificas el modelo (`Models/Incidencia.cs`)

Si agregas, quitas o cambias una propiedad de un modelo, debes crear una migración:

```bash
# 1. Crear la migración (usa un nombre que describa el cambio, sin espacios)
dotnet tool run dotnet-ef migrations add NombreDelCambio

# 2. Aplicarla a tu base de datos local
dotnet tool run dotnet-ef database update

# 3. Comprobar que compila
dotnet build
```

Sube a Git los archivos nuevos de la carpeta `Migrations/`: tus compañeros los necesitan para actualizar su base de datos.

Si te equivocaste y **todavía no la subiste a Git**, puedes deshacer la última migración:

```bash
dotnet tool run dotnet-ef database update NombreDeLaMigracionAnterior
dotnet tool run dotnet-ef migrations remove
```

> No borres ni edites a mano migraciones que ya están en GitHub: rompería la base de datos de tus compañeros.

---

## 5. Subir tus cambios a GitHub

```bash
git status                  # revisa qué archivos cambiaste
git add .
git commit -m "Describe brevemente tu cambio"
git pull                    # trae los cambios de los demás antes de subir
git push
```

**Qué NO se sube** (ya está configurado en `.gitignore`):

- `alerta_vecinal.db`: cada uno tiene su propia base de datos local con sus datos de prueba.
- `bin/` y `obj/`: se generan al compilar.

**Qué SÍ se sube:** el código, las vistas y la carpeta `Migrations/`.

---

## 6. Comandos útiles para revisar que todo funciona

```bash
# Compilar sin ejecutar
dotnet build

# Ver las migraciones y cuáles están aplicadas
dotnet tool run dotnet-ef migrations list

# Ver a qué base de datos se conecta el proyecto
dotnet tool run dotnet-ef dbcontext info

# Ver las incidencias guardadas (requiere tener instalado sqlite3)
sqlite3 alerta_vecinal.db "SELECT Id, Titulo, Categoria, Estado, FechaRegistro FROM Incidencias"
```

---

## 7. Problemas frecuentes

| Problema | Solución |
|---|---|
| `no such table: Incidencias` | Ejecuta `dotnet tool run dotnet-ef database update` |
| `dotnet-ef` no se encuentra | Ejecuta `dotnet tool restore` dentro de la carpeta del proyecto |
| `address already in use` (puerto ocupado) | Ya hay otra copia de la app abierta. Ciérrala con `Ctrl + C` o cierra la otra terminal |
| Quiero empezar con la base de datos vacía | Detén la app, borra `alerta_vecinal.db` y ejecuta `dotnet tool run dotnet-ef database update` |
| Los cambios de CSS no se ven | Recarga la página sin caché: `Ctrl + Shift + R` (Windows) o `Cmd + Shift + R` (Mac) |

---

## 8. Estructura del proyecto

```text
Controllers/     HomeController: páginas y lógica (crear, listar, detalle)
Data/            ApplicationDbContext: conexión con SQLite
Models/          Incidencia (tabla de la base de datos), estados y categorías
ViewModels/      Datos que usan las vistas y el formulario
Views/           Páginas Razor (.cshtml)
Helpers/         Iconos SVG y catálogo de categorías
Migrations/      Historial de cambios de la base de datos (NO borrar)
wwwroot/         CSS, JavaScript y librerías
```

Páginas disponibles:

| Página | Ruta |
|---|---|
| Inicio | `/` |
| Reportar incidencia | `/Home/Reportar` |
| Listado de incidencias | `/Home/Incidencias` |
| Detalle de una incidencia | `/Home/Detalle/{id}` |
