using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Data;
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

// Protección de fotografías: detección de rostros con Google Cloud Vision + pixelado local.
// Credenciales: variable de entorno GOOGLE_APPLICATION_CREDENTIALS (ver README.md).
builder.Services.AddSingleton<IFaceDetectionService, GoogleVisionFaceDetectionService>();
builder.Services.AddScoped<FotoIncidenciaService>();

var app = builder.Build();

// Crea la base de datos y aplica las migraciones pendientes al iniciar.
// Necesario en Docker/Render, donde no existe "dotnet ef database update".
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
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

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
