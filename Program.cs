using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using proyectoGrupal.Controllers;
using proyectoGrupal.Data;
using proyectoGrupal.Helpers;
using proyectoGrupal.Models;
using proyectoGrupal.Services;
using proyectoGrupal.Services.Algolia;
using proyectoGrupal.Services.Asistente;
using proyectoGrupal.Services.Asistente.Conocimiento;
using proyectoGrupal.Services.Asistente.Plugins;
using proyectoGrupal.Services.GoogleMaps;
using proyectoGrupal.Services.LimiteReportes;
using proyectoGrupal.Services.PieSocket;
using proyectoGrupal.Services.Sentimiento;
using proyectoGrupal.Services.Categorias;
using proyectoGrupal.Services.Zonas;

var builder = WebApplication.CreateBuilder(args);

// En Render (y otros hosts) el puerto llega en la variable de entorno PORT.
var puerto = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(puerto))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{puerto}");
}

// Render atiende el HTTPS y reenvía la petición a la app por HTTP.
// Estos encabezados le indican a la app el esquema (https) y la IP original del visitante.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// Base de datos SQLite (cadena de conexión en appsettings.json)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cuentas de usuario y roles con ASP.NET Core Identity, guardados en la misma base SQLite.
// Identity guarda solo el hash de cada contraseña (PasswordHasher), nunca el texto plano.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Contraseña: mínimo 6 caracteres, con al menos una letra (ContrasenaConLetraValidator) y un número.
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        // El correo es también el nombre de usuario, así que no puede repetirse.
        options.User.RequireUniqueEmail = true;

        // Esta etapa todavía no envía correos de confirmación.
        options.SignIn.RequireConfirmedAccount = false;

        // Bloqueo temporal tras varios intentos fallidos, contra ataques de fuerza bruta.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddPasswordValidator<ContrasenaConLetraValidator<ApplicationUser>>()
    .AddErrorDescriber<MensajesIdentity>();

// Cookie de sesión de Identity: rutas de login, logout y acceso denegado.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "AlertaVecinal.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // HTTPS en Render; en local también funciona con el perfil http.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    // "Recordarme" mantiene la sesión 14 días; se renueva mientras el usuario la use.
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// "Continuar con Google" (OAuth). Claves en variables de entorno o User Secrets (ver README).
// Sin ellas no se registra Google: el botón no aparece y el inicio de sesión con correo funciona igual.
var googleClientId = builder.Configuration["GOOGLE_CLIENT_ID"]?.Trim();
var googleClientSecret = builder.Configuration["GOOGLE_CLIENT_SECRET"]?.Trim();
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        // Google vuelve a /signin-google con un GET normal: Lax basta, y así la cookie
        // también funciona en local con http (SameSite=None exige https).
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        // Si Google verificó el correo (AccountController solo vincula o crea cuentas con correos verificados).
        options.ClaimActions.MapJsonKey("email_verified", "email_verified");
        options.ClaimActions.MapJsonKey("verified_email", "verified_email");
    });
}

// Protección de fotografías: detección de rostros con Google Cloud Vision + pixelado local.
// Credenciales: variable de entorno GOOGLE_APPLICATION_CREDENTIALS (ver README.md).
builder.Services.AddSingleton<IFaceDetectionService, GoogleVisionFaceDetectionService>();
builder.Services.AddScoped<FotoIncidenciaService>();

// Mapas con Google Maps (Maps JavaScript API, Places API (New) y Geocoding API).
// Clave en variables de entorno o User Secrets (ver README). Sin ella, los mapas muestran un aviso
// y el reporte se puede enviar solo con la ubicación escrita.
builder.Services.Configure<GoogleMapsOptions>(opciones =>
{
    opciones.ApiKey = builder.Configuration["GOOGLE_MAPS_API_KEY"]?.Trim();
    var mapId = builder.Configuration["GOOGLE_MAPS_MAP_ID"]?.Trim();
    if (!string.IsNullOrEmpty(mapId))
    {
        opciones.MapId = mapId;
    }
});

// Búsqueda de incidencias con Algolia (índice de búsqueda; SQLite sigue siendo la fuente de verdad).
// Claves en variables de entorno o User Secrets (ver README). Sin ellas, la búsqueda usa SQLite.
builder.Services.Configure<AlgoliaOptions>(opciones =>
{
    opciones.ApplicationId = builder.Configuration["ALGOLIA_APPLICATION_ID"];
    opciones.AdminApiKey = builder.Configuration["ALGOLIA_ADMIN_API_KEY"];
    opciones.HostPruebas = builder.Configuration["ALGOLIA_HOST_PRUEBAS"];
    var indice = builder.Configuration["ALGOLIA_INDEX_NAME"];
    if (!string.IsNullOrWhiteSpace(indice))
    {
        opciones.IndexName = indice.Trim();
    }
});
builder.Services.AddSingleton<IAlgoliaIncidenciaService, AlgoliaIncidenciaService>();
builder.Services.AddScoped<BusquedaIncidenciasService>();

// Tiempo real con PieSocket (solo transporte de avisos; SQLite sigue siendo la fuente de verdad).
// Claves en variables de entorno o User Secrets (ver README). Sin ellas, no hay tiempo real y todo lo demás funciona.
builder.Services.Configure<PieSocketOptions>(opciones =>
{
    opciones.ApiKey = builder.Configuration["PIESOCKET_API_KEY"];
    opciones.ApiSecret = builder.Configuration["PIESOCKET_API_SECRET"];
    opciones.ClusterId = builder.Configuration["PIESOCKET_CLUSTER_ID"]?.Trim();
    opciones.HostPruebas = builder.Configuration["PIESOCKET_HOST_PRUEBAS"];
});
builder.Services.AddHttpClient(PieSocketRealtimeService.NombreClienteHttp, cliente =>
{
    // Si PieSocket no responde, el reporte o el cambio de estado no esperan más que esto.
    cliente.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddSingleton<IPieSocketRealtimeService, PieSocketRealtimeService>();

// Límite de reportes por usuario (contra el spam). Conteo en Redis si REDIS_CONNECTION está configurada;
// si no, en la memoria del servidor. LIMITE_REPORTES y LIMITE_REPORTES_MINUTOS son opcionales (3 cada 10 min).
builder.Services.Configure<LimiteReportesOptions>(opciones =>
{
    opciones.RedisConnection = builder.Configuration["REDIS_CONNECTION"];
    if (int.TryParse(builder.Configuration["LIMITE_REPORTES"], out var maximo) && maximo > 0)
    {
        opciones.MaximoReportes = maximo;
    }
    if (int.TryParse(builder.Configuration["LIMITE_REPORTES_MINUTOS"], out var minutos) && minutos > 0)
    {
        opciones.VentanaMinutos = minutos;
    }
});
builder.Services.AddSingleton<ILimiteReportesService, LimiteReportesService>();

// Análisis de sentimientos de los mensajes de contacto (modelo de ML.NET en MLModels/).
// Se entrena con ML/sentimentAnalysisNLP/Entrenamiento o con el botón "Reentrenar modelo" del panel.
// Sin el modelo, los mensajes se guardan igual pero sin sentimiento.
builder.Services.AddSingleton<ISentimientoService, SentimientoService>();

// Machine learning sobre incidencias (ML.NET):
//   - Clasificación multiclase: sugiere la categoría al reportar (modelo en MLModels/CategoriaModel.zip).
//   - Clustering K-Means: agrupa las incidencias por ubicación en "zonas críticas" (se calcula al momento).
builder.Services.AddSingleton<ICategoriaSugeridaService, CategoriaSugeridaService>();
builder.Services.AddSingleton<IZonasCriticasService, ZonasCriticasService>();

// Asistente virtual (chat): agente de Semantic Kernel sobre Groq, con la ayuda de la página en Qdrant.
// Claves en variables de entorno o User Secrets (ver README, sección 17). Sin GROQ_API_KEY el chat no aparece;
// sin QDRANT_* el asistente funciona igual y busca la ayuda en los archivos de Conocimiento/.
builder.Services.Configure<AsistenteOptions>(opciones =>
{
    opciones.ApiKey = builder.Configuration["GROQ_API_KEY"];
    var modelo = builder.Configuration["GROQ_MODEL"];
    if (!string.IsNullOrWhiteSpace(modelo))
    {
        opciones.Modelo = modelo.Trim();
    }
    if (int.TryParse(builder.Configuration["ASISTENTE_LIMITE_MENSAJES"], out var mensajes) && mensajes > 0)
    {
        opciones.LimiteMensajes = mensajes;
    }
    if (int.TryParse(builder.Configuration["ASISTENTE_LIMITE_MINUTOS"], out var minutosChat) && minutosChat > 0)
    {
        opciones.LimiteMinutos = minutosChat;
    }
});
builder.Services.Configure<QdrantOptions>(opciones =>
{
    opciones.Endpoint = builder.Configuration["QDRANT_ENDPOINT"];
    opciones.ApiKey = builder.Configuration["QDRANT_API_KEY"];
    var coleccion = builder.Configuration["QDRANT_COLLECTION"];
    if (!string.IsNullOrWhiteSpace(coleccion))
    {
        opciones.Coleccion = coleccion.Trim();
    }
    var coleccionIncidencias = builder.Configuration["QDRANT_COLLECTION_INCIDENCIAS"];
    if (!string.IsNullOrWhiteSpace(coleccionIncidencias))
    {
        opciones.ColeccionIncidencias = coleccionIncidencias.Trim();
    }
});
builder.Services.AddHttpClient(ClienteQdrant.NombreClienteHttp, cliente =>
{
    // Cargar todos los documentos puede tardar (Qdrant genera los vectores); cada operación tiene además su propio límite.
    cliente.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddSingleton<ClienteQdrant>();
builder.Services.AddSingleton<IBaseConocimientoService, BaseConocimientoService>();
builder.Services.AddSingleton<IIncidenciasSemanticasService, IncidenciasSemanticasService>();
builder.Services.AddHostedService<SincronizacionInicialConocimiento>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SesionAsistente>();
builder.Services.AddScoped<AyudaPlugin>();
builder.Services.AddScoped<IncidenciasPlugin>();
builder.Services.AddScoped<ReportePlugin>();
builder.Services.AddScoped<ContactoPlugin>();
builder.Services.AddScoped<MisReportesPlugin>();
builder.Services.AddScoped<IAsistenteService, AsistenteService>();

// Sesión del servidor: guarda el historial de la conversación con el asistente (en memoria).
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "AlertaVecinal.Asistente";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

// Límite de mensajes al asistente por persona (usuario con sesión o, si no, su IP): cuida la cuota de Groq.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AsistenteController.PoliticaLimite, contexto =>
    {
        var opciones = contexto.RequestServices.GetRequiredService<IOptions<AsistenteOptions>>().Value;
        var clave = contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? contexto.Connection.RemoteIpAddress?.ToString()
                    ?? "anonimo";
        return RateLimitPartition.GetFixedWindowLimiter(clave, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = opciones.LimiteMensajes,
            Window = TimeSpan.FromMinutes(opciones.LimiteMinutos),
            QueueLimit = 0
        });
    });
    options.OnRejected = async (contexto, cancellationToken) =>
    {
        var opciones = contexto.HttpContext.RequestServices.GetRequiredService<IOptions<AsistenteOptions>>().Value;
        contexto.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await contexto.HttpContext.Response.WriteAsync(
            $"Llegaste al límite de {opciones.LimiteMensajes} preguntas cada {opciones.LimiteMinutos} minutos. Espera unos minutos y vuelve a intentarlo.",
            cancellationToken);
    };
});

var app = builder.Build();

// Crea la base de datos y aplica las migraciones pendientes al iniciar.
// Necesario en Docker/Render, donde no existe "dotnet ef database update".
// Después crea los roles, el administrador inicial (ADMIN_EMAIL / ADMIN_PASSWORD) y el usuario
// de prueba (USER_EMAIL / USER_PASSWORD) si faltan.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);

    // Incidencias de demostración: solo si la tabla está vacía (en Render, después de cada reinicio).
    // Algolia se resincroniza para que no queden resultados del arranque anterior (Qdrant se revisa
    // en SincronizacionInicialConocimiento, en segundo plano).
    if (await IncidenciasDemoSeeder.SeedAsync(scope.ServiceProvider) > 0)
    {
        var algolia = scope.ServiceProvider.GetRequiredService<IAlgoliaIncidenciaService>();
        if (algolia.EstaConfigurado)
        {
            var demo = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Incidencias.AsNoTracking().ToListAsync();
            var resultado = await algolia.SincronizarTodoAsync(demo);
            app.Logger.LogInformation("Algolia tras crear las incidencias de demostración: {Mensaje}", resultado.Mensaje);
        }
    }

    // Mensajes de contacto guardados sin sentimiento (anteriores al modelo o sin modelo disponible).
    var analizados = await SentimientoService.AnalizarMensajesAsync(
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
        scope.ServiceProvider.GetRequiredService<ISentimientoService>());
    if (analizados > 0)
    {
        app.Logger.LogInformation("Sentimiento analizado en {Cantidad} mensajes de contacto pendientes.", analizados);
    }
}

// Crea los clientes de Algolia, PieSocket y Redis al iniciar: el log indica enseguida si están configurados.
app.Services.GetRequiredService<IAlgoliaIncidenciaService>();
app.Services.GetRequiredService<IPieSocketRealtimeService>();
app.Services.GetRequiredService<ILimiteReportesService>();
if (string.IsNullOrEmpty(googleClientId) || string.IsNullOrEmpty(googleClientSecret))
{
    app.Logger.LogInformation("Google no está configurado (GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET): no se muestra \"Continuar con Google\".");
}
if (string.IsNullOrWhiteSpace(builder.Configuration["GOOGLE_MAPS_API_KEY"]))
{
    app.Logger.LogInformation("Google Maps no está configurado (GOOGLE_MAPS_API_KEY): los mapas muestran un aviso.");
}
if (string.IsNullOrWhiteSpace(builder.Configuration["GROQ_API_KEY"]))
{
    app.Logger.LogInformation("Groq no está configurado (GROQ_API_KEY): el asistente virtual no aparece.");
}
app.Services.GetRequiredService<IBaseConocimientoService>();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Sirve archivos creados en tiempo de ejecución, como las fotos de /uploads/incidencias.
// (MapStaticAssets solo conoce los archivos que existían al compilar).
app.UseStaticFiles();

app.UseRouting();

// Primero se identifica al usuario (cookie) y después se comprueban sus permisos.
app.UseAuthentication();
app.UseAuthorization();

// Límite de mensajes del asistente (después de la autenticación: se cuenta por usuario) y sesión del chat.
app.UseRateLimiter();
app.UseSession();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
