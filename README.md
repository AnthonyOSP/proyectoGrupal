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
| "No fue posible procesar la fotografía..." | Google Cloud Vision no está configurado o falló. Revisa la sección 9. Puedes enviar el reporte sin foto |

---

## 8. Estructura del proyecto

```text
Controllers/     HomeController (sitio público) y AdminController (panel /Admin)
Data/            ApplicationDbContext: conexión con SQLite
Models/          Incidencia (tabla de la base de datos), estados y categorías
ViewModels/      Datos que usan las vistas y el formulario
Views/           Páginas Razor (.cshtml)
Helpers/         Iconos SVG, catálogo de categorías y filtros
Services/        Protección de fotos: Google Cloud Vision + pixelado de rostros
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
| Panel administrativo | `/Admin` |

---

## 9. Protección de fotografías con Google Cloud Vision

Cuando un vecino adjunta una foto, la aplicación oculta automáticamente los rostros antes de guardarla:

```text
Foto recibida (en memoria)
   ↓  se valida: JPG/PNG real, máximo 5 MB
   ↓  se corrige la orientación y se eliminan los metadatos (GPS, cámara...)
Google Cloud Vision → devuelve DÓNDE hay rostros (no identifica personas)
   ↓
Pixelado local de cada rostro (con ImageSharp, en el servidor)
   ↓
Se guarda SOLO la foto protegida en wwwroot/uploads/incidencias/{nombre-aleatorio}.jpg
   ↓
Su ruta (/uploads/incidencias/...) se guarda en SQLite (FotoUrl)
```

- **La foto original nunca se guarda.** Solo existe en memoria mientras se procesa.
- **Si no hay rostros**, se guarda la foto (sin metadatos).
- **Si Google Vision falla** (sin credenciales, sin internet, etc.), **no se guarda ninguna foto** y el vecino ve un mensaje para intentarlo de nuevo o enviar el reporte sin foto.
- Sin credenciales, la aplicación funciona igual: solo no se pueden adjuntar fotos.

### 9.1 Crear el proyecto y habilitar la API (una sola vez, lo hace una persona del grupo)

1. Entra a https://console.cloud.google.com y crea un proyecto (por ejemplo `alerta-vecinal`).
2. Asocia una **cuenta de facturación** al proyecto. Vision la exige, aunque las primeras 1000 imágenes al mes son gratuitas.
3. Ve a **APIs y servicios → Biblioteca**, busca **Cloud Vision API** y pulsa **Habilitar**.

### 9.2 Crear las credenciales (cuenta de servicio)

1. Ve a **IAM y administración → Cuentas de servicio → Crear cuenta de servicio**.
   Ponle un nombre como `alerta-vecinal-vision`. No necesitas asignarle roles para usar Vision
   (si luego aparece un error de permisos, asígnale el rol **Service Usage Consumer**).
2. Entra a la cuenta creada → pestaña **Claves** → **Agregar clave → Crear clave nueva → JSON**.
   Se descargará un archivo `.json`.
3. **Guarda ese archivo FUERA de la carpeta del proyecto**, por ejemplo:
   - macOS / Linux: `~/credenciales/alerta-vecinal-vision.json`
   - Windows: `C:\credenciales\alerta-vecinal-vision.json`

> Si tu organización (por ejemplo, una cuenta institucional) no permite crear claves JSON, usa una cuenta personal de Google para este proyecto.

### 9.3 Configurar `GOOGLE_APPLICATION_CREDENTIALS`

La aplicación lee la ruta del archivo desde esta variable de entorno. **Nunca** se escribe en el código ni en `appsettings.json`.

**macOS / Linux** (zsh o bash):

```bash
# Solo para la terminal actual:
export GOOGLE_APPLICATION_CREDENTIALS="$HOME/credenciales/alerta-vecinal-vision.json"

# Permanente (zsh, el shell por defecto en macOS):
echo 'export GOOGLE_APPLICATION_CREDENTIALS="$HOME/credenciales/alerta-vecinal-vision.json"' >> ~/.zshrc
```

**Windows** (PowerShell):

```powershell
# Solo para la terminal actual:
$env:GOOGLE_APPLICATION_CREDENTIALS = "C:\credenciales\alerta-vecinal-vision.json"

# Permanente (abre una terminal nueva después):
setx GOOGLE_APPLICATION_CREDENTIALS "C:\credenciales\alerta-vecinal-vision.json"
```

Después de configurarla permanentemente, **cierra y vuelve a abrir la terminal** (y VS Code) para que la tome.

### 9.4 Ejecutar y probar

```bash
echo $GOOGLE_APPLICATION_CREDENTIALS     # debe mostrar la ruta (en PowerShell: echo $env:GOOGLE_APPLICATION_CREDENTIALS)
dotnet run
```

1. Entra a `/Home/Reportar`, completa el formulario y adjunta una foto donde aparezcan personas.
2. Envía el reporte y abre **Ver mi reporte**: los rostros deben verse pixelados.
3. La foto guardada está en `wwwroot/uploads/incidencias/` con un nombre aleatorio.

Usa fotos propias o de bancos de imágenes libres para las pruebas, nunca fotos de personas sin su permiso.

### 9.5 Seguridad: qué NUNCA se sube a Git

Ya está configurado en `.gitignore`, pero revisa siempre con `git status` antes de hacer commit:

- **El archivo JSON de credenciales.** Quien lo tenga puede usar el proyecto de Google Cloud y generar cobros. Si se sube por error, **elimina la clave** en Google Cloud Console (Cuentas de servicio → Claves) y crea una nueva.
- **La carpeta `wwwroot/uploads/`**, con fotos de vecinos.
- **La base de datos `alerta_vecinal.db`.**
