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

# 5. Configurar tu administrador local (ver sección 9: usa tu propio correo y contraseña)
dotnet user-secrets set "ADMIN_EMAIL" "admin@alertavecinal.local"
dotnet user-secrets set "ADMIN_PASSWORD" "<tu-contraseña>"

# 6. Ejecutar la aplicación
dotnet run
```

Luego abre en el navegador la dirección que aparece en la terminal, normalmente:

```text
http://localhost:5131
```

Para detener la aplicación: `Ctrl + C` en la terminal.

> **Nota:** al iniciar, la aplicación también aplica las migraciones pendientes, así que el paso 4 es opcional. Hacerlo a mano te permite ver errores de la base de datos antes de ejecutar la app.
>
> Si te saltas el paso 5, la aplicación funciona igual, pero nadie podrá entrar al panel `/Admin`.

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
| Quiero empezar con la base de datos vacía | Detén la app, borra `alerta_vecinal.db` y ejecuta `dotnet tool run dotnet-ef database update`. Se borran también las cuentas; el administrador se vuelve a crear al iniciar |
| `/Admin` me lleva a "No tienes permiso" | Iniciaste sesión con una cuenta de ciudadano. Cierra sesión y entra con la cuenta de `ADMIN_EMAIL` |
| En el log aparece "Administrador inicial no configurado" | Falta configurar `ADMIN_EMAIL` y/o `ADMIN_PASSWORD` (sección 9) |
| En el log aparece "No se creó el administrador inicial" | `ADMIN_PASSWORD` no cumple las reglas: mínimo 6 caracteres, con al menos una letra y un número |
| Olvidé la contraseña del administrador | Cambiar `ADMIN_PASSWORD` **no** modifica una cuenta existente. En local, borra la base de datos (fila anterior) y vuelve a iniciar |
| Los cambios de CSS no se ven | Recarga la página sin caché: `Ctrl + Shift + R` (Windows) o `Cmd + Shift + R` (Mac) |
| "No fue posible procesar la fotografía..." | Google Cloud Vision no está configurado o falló. Revisa la sección 10. Puedes enviar el reporte sin foto |

---

## 8. Estructura del proyecto

```text
Controllers/     HomeController (sitio público), AccountController (registro, login, logout)
                 y AdminController (panel /Admin)
Constants/       RoleNames: nombres de los roles (Administrador, Ciudadano)
Data/            ApplicationDbContext (SQLite + Identity) e IdentitySeeder (roles y administrador inicial)
Models/          Incidencia, ApplicationUser (cuenta de usuario), estados y categorías
ViewModels/      Datos que usan las vistas y el formulario
Views/           Páginas Razor (.cshtml)
Helpers/         Iconos SVG, catálogo de categorías y filtros
Services/        Protección de fotos: Google Cloud Vision + pixelado de rostros
Migrations/      Historial de cambios de la base de datos (NO borrar)
Dockerfile       Imagen Docker para publicar en Render (ver sección 11)
wwwroot/         CSS, JavaScript y librerías
```

Páginas disponibles y quién puede verlas:

| Página | Ruta | Visitante | Ciudadano | Administrador |
|---|---|:-:|:-:|:-:|
| Inicio | `/` | ✓ | ✓ | ✓ |
| Listado de incidencias | `/Home/Incidencias` | ✓ | ✓ | ✓ |
| Detalle de una incidencia | `/Home/Detalle/{id}` | ✓ | ✓ | ✓ |
| Reportar incidencia | `/Home/Reportar` | ✗ (va al login) | ✓ | ✓ |
| Panel administrativo | `/Admin` | ✗ (va al login) | ✗ (acceso denegado) | ✓ |
| Crear cuenta | `/Account/Register` | ✓ | — | — |
| Iniciar sesión | `/Account/Login` | ✓ | — | — |
| Cerrar sesión | botón del menú (POST a `/Account/Logout`) | — | ✓ | ✓ |

---

## 9. Cuentas de usuario, roles y administrador inicial

Las cuentas usan **ASP.NET Core Identity**. Sus tablas (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`...) están en el mismo archivo `alerta_vecinal.db` que las incidencias. Identity guarda solo el **hash** de cada contraseña, nunca la contraseña en texto plano.

Hay dos roles, que se crean automáticamente al iniciar la aplicación:

| Rol | Quién lo tiene | Qué puede hacer |
|---|---|---|
| `Ciudadano` | Toda persona que se registra en `/Account/Register` | Reportar incidencias |
| `Administrador` | La cuenta configurada con `ADMIN_EMAIL` | Reportar y usar el panel `/Admin` (cambiar estados) |

El formulario de registro no tiene campo de rol: el servidor asigna siempre `Ciudadano`.

Reglas de contraseña: mínimo 6 caracteres, con al menos una letra y un número. Después de 5 intentos fallidos de inicio de sesión, la cuenta se bloquea 5 minutos.

### 9.1 Configuración del administrador inicial

Al iniciar, la aplicación lee dos valores de configuración:

| Variable | Ejemplo |
|---|---|
| `ADMIN_EMAIL` | `admin@alertavecinal.local` |
| `ADMIN_PASSWORD` | una contraseña que elijas tú (mínimo 6 caracteres, una letra y un número) |

- Si la cuenta **no existe**, la crea y le asigna el rol `Administrador`.
- Si la cuenta **ya existe**, solo se asegura de que tenga el rol `Administrador`. **No** cambia su contraseña.
- Si falta alguno de los dos valores, **no crea ningún administrador** y escribe en el log: `Administrador inicial no configurado...`.

> **Nunca** escribas la contraseña real en el código, en `appsettings.json`, en este README ni en ningún archivo que se suba a Git.

**Opción A: User Secrets (recomendado en tu computadora).** Se guardan fuera de la carpeta del proyecto, en tu perfil de usuario, así que nunca llegan a Git. Solo se leen en modo `Development`, que es el que usa `dotnet run`.

```bash
dotnet user-secrets set "ADMIN_EMAIL" "admin@alertavecinal.local"
dotnet user-secrets set "ADMIN_PASSWORD" "<tu-contraseña>"

# Revisar lo configurado / borrar
dotnet user-secrets list
dotnet user-secrets remove "ADMIN_PASSWORD"
```

**Opción B: variables de entorno.**

```powershell
# Windows (PowerShell), solo para la terminal actual:
$env:ADMIN_EMAIL = "admin@alertavecinal.local"
$env:ADMIN_PASSWORD = "<tu-contraseña>"
dotnet run
```

```bash
# macOS / Linux, solo para la terminal actual:
export ADMIN_EMAIL="admin@alertavecinal.local"
export ADMIN_PASSWORD="<tu-contraseña>"
dotnet run
```

Luego entra a `/Account/Login` con ese correo y contraseña, y abre `/Admin`.

Cada integrante del grupo configura **su propio** administrador local: la base de datos no se comparte.

### 9.2 Migraciones de esta etapa

La migración `AddIdentity` crea las tablas de Identity sin tocar la tabla `Incidencias` ni sus datos. Para aplicarla a tu base de datos local:

```bash
dotnet tool restore
dotnet tool run dotnet-ef database update
```

---

## 10. Protección de fotografías con Google Cloud Vision

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

### 10.1 Crear el proyecto y habilitar la API (una sola vez, lo hace una persona del grupo)

1. Entra a https://console.cloud.google.com y crea un proyecto (por ejemplo `alerta-vecinal`).
2. Asocia una **cuenta de facturación** al proyecto. Vision la exige, aunque las primeras 1000 imágenes al mes son gratuitas.
3. Ve a **APIs y servicios → Biblioteca**, busca **Cloud Vision API** y pulsa **Habilitar**.

### 10.2 Crear las credenciales (cuenta de servicio)

1. Ve a **IAM y administración → Cuentas de servicio → Crear cuenta de servicio**.
   Ponle un nombre como `alerta-vecinal-vision`. No necesitas asignarle roles para usar Vision
   (si luego aparece un error de permisos, asígnale el rol **Service Usage Consumer**).
2. Entra a la cuenta creada → pestaña **Claves** → **Agregar clave → Crear clave nueva → JSON**.
   Se descargará un archivo `.json`.
3. **Guarda ese archivo FUERA de la carpeta del proyecto**, por ejemplo:
   - macOS / Linux: `~/credenciales/alerta-vecinal-vision.json`
   - Windows: `C:\credenciales\alerta-vecinal-vision.json`

> Si tu organización (por ejemplo, una cuenta institucional) no permite crear claves JSON, usa una cuenta personal de Google para este proyecto.

### 10.3 Configurar `GOOGLE_APPLICATION_CREDENTIALS`

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

### 10.4 Ejecutar y probar

```bash
echo $GOOGLE_APPLICATION_CREDENTIALS     # debe mostrar la ruta (en PowerShell: echo $env:GOOGLE_APPLICATION_CREDENTIALS)
dotnet run
```

1. Inicia sesión, entra a `/Home/Reportar`, completa el formulario y adjunta una foto donde aparezcan personas.
2. Envía el reporte y abre **Ver mi reporte**: los rostros deben verse pixelados.
3. La foto guardada está en `wwwroot/uploads/incidencias/` con un nombre aleatorio.

Usa fotos propias o de bancos de imágenes libres para las pruebas, nunca fotos de personas sin su permiso.

### 10.5 Seguridad: qué NUNCA se sube a Git

Ya está configurado en `.gitignore`, pero revisa siempre con `git status` antes de hacer commit:

- **El archivo JSON de credenciales.** Quien lo tenga puede usar el proyecto de Google Cloud y generar cobros. Si se sube por error, **elimina la clave** en Google Cloud Console (Cuentas de servicio → Claves) y crea una nueva.
- **La carpeta `wwwroot/uploads/`**, con fotos de vecinos.
- **La base de datos `alerta_vecinal.db`.**

---

## 11. Publicar en Render (Docker)

El proyecto incluye un `Dockerfile` listo para Render. Al iniciar, la aplicación crea la base de datos SQLite y aplica las migraciones automáticamente.

### 11.1 Antes de publicar: lee esto

- **Configura el administrador** con `ADMIN_EMAIL` y `ADMIN_PASSWORD` (sección 11.3). Sin ellas, nadie puede entrar a `/Admin`. Usa una contraseña distinta de la de tu computadora.
- **Los datos no son permanentes en el plan gratuito.** El disco del contenedor se borra en cada despliegue y cada vez que Render reinicia el servicio. Se pierden las incidencias, las fotos y las **cuentas de ciudadanos**. El administrador se vuelve a crear solo al iniciar. Es suficiente para una demostración, pero no para uso real.
- **Las sesiones se cierran en cada reinicio.** Las claves que protegen las cookies se guardan dentro del contenedor, así que después de un despliegue hay que volver a iniciar sesión.
- **El plan gratuito se "duerme"** tras unos 15 minutos sin visitas. La primera visita después tarda cerca de un minuto en responder, y al despertar la base de datos empieza vacía.
- **Limita el uso de Google Vision** (cuota diaria baja en Google Cloud Console), porque ahora cualquier persona podría subir fotos.

### 11.2 Crear el servicio

1. Sube los cambios a GitHub (el `Dockerfile` debe estar en la rama que vas a publicar, por ejemplo `main`).
2. Entra a https://dashboard.render.com → **New → Web Service** y conecta el repositorio `proyectoGrupal`.
3. Configura:
   - **Language / Runtime:** `Docker` (Render lo detecta por el `Dockerfile`).
   - **Branch:** `main`.
   - **Instance Type:** `Free`.
4. No hace falta configurar el puerto: Render asigna la variable `PORT` y la aplicación la usa automáticamente.

### 11.3 Administrador inicial en Render

En el servicio, ve a **Environment → Environment Variables** y agrega:

| Key | Value |
|---|---|
| `ADMIN_EMAIL` | el correo del administrador |
| `ADMIN_PASSWORD` | una contraseña segura, solo en Render |

Render guarda estos valores fuera del repositorio. Al desplegar, el log debe mostrar `Administrador inicial ... creado.`

### 11.4 Credenciales de Google Vision en Render (Secret File)

El archivo JSON **no** se sube a GitHub. En Render se carga como archivo secreto:

1. En el servicio, ve a **Environment → Secret Files → Add Secret File**.
2. **Filename:** `google-credentials.json`. **Contents:** pega el contenido completo del JSON de tu cuenta de servicio.
3. En **Environment Variables**, agrega:

   | Key | Value |
   |---|---|
   | `GOOGLE_APPLICATION_CREDENTIALS` | `/etc/secrets/google-credentials.json` |

4. Guarda. Render vuelve a desplegar el servicio.

Sin este paso, la aplicación funciona, pero no acepta fotos (muestra el mensaje "No fue posible procesar la fotografía...").

### 11.5 Desplegar y comprobar

1. Pulsa **Deploy** (o espera el despliegue automático). La primera compilación tarda unos minutos.
2. Abre la URL que te da Render (`https://<nombre>.onrender.com`) y prueba reportar una incidencia.
3. Si algo falla, revisa la pestaña **Logs** del servicio.

Cada vez que hagas `git push` a la rama configurada, Render vuelve a desplegar automáticamente.

### 11.6 Probar la imagen en tu computadora (opcional)

Requiere Docker Desktop instalado:

```bash
docker build -t alerta-vecinal .
docker run -p 8080:8080 \
  -e GOOGLE_APPLICATION_CREDENTIALS=/secrets/google.json \
  -v "$GOOGLE_APPLICATION_CREDENTIALS":/secrets/google.json:ro \
  alerta-vecinal
```

Luego abre http://localhost:8080.
