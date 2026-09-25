using Microsoft.EntityFrameworkCore;
using proyectoGrupal.Data;
using proyectoGrupal.Services;

var builder = WebApplication.CreateBuilder(args);

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
