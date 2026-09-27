using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Data;
using proyectoGrupal.Helpers;
using proyectoGrupal.Models;
using proyectoGrupal.Services;

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

// Protección de fotografías: detección de rostros con Google Cloud Vision + pixelado local.
// Credenciales: variable de entorno GOOGLE_APPLICATION_CREDENTIALS (ver README.md).
builder.Services.AddSingleton<IFaceDetectionService, GoogleVisionFaceDetectionService>();
builder.Services.AddScoped<FotoIncidenciaService>();

var app = builder.Build();

// Crea la base de datos y aplica las migraciones pendientes al iniciar.
// Necesario en Docker/Render, donde no existe "dotnet ef database update".
// Después crea los roles y el administrador inicial (ADMIN_EMAIL / ADMIN_PASSWORD) si faltan.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

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

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
