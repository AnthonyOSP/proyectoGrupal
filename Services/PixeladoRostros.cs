using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace proyectoGrupal.Services;

// Pixela localmente las zonas de los rostros (no usa servicios externos).
public static class PixeladoRostros
{
    // El recuadro se amplía un 20 % (10 % por lado) para cubrir bien cabello, frente y mentón.
    public const double MargenSeguridad = 0.20;

    // Cantidad aproximada de bloques en el lado más largo del rostro.
    // Con tan pocos bloques el rostro queda irreconocible.
    public const int BloquesPorRostro = 8;

    // Aplica el pixelado sobre la imagen y devuelve cuántas zonas se pixelaron.
    public static int Pixelar(Image imagen, IEnumerable<RegionRostro> rostros)
    {
        var zonas = rostros
            .Select(r => ZonaSegura(r, imagen.Width, imagen.Height))
            .Where(z => z.Width > 0 && z.Height > 0)
            .ToList();

        if (zonas.Count == 0)
        {
            return 0;
        }

        imagen.Mutate(ctx =>
        {
            foreach (var zona in zonas)
            {
                // Tamaño de cada bloque: mientras más grande el rostro, más grandes los bloques.
                var tamanoBloque = Math.Max(6, Math.Max(zona.Width, zona.Height) / BloquesPorRostro);
                ctx.Pixelate(tamanoBloque, zona);
            }
        });

        return zonas.Count;
    }

    // Amplía el recuadro con el margen de seguridad y lo recorta a los límites de la imagen,
    // para no acceder nunca fuera de ella.
    public static Rectangle ZonaSegura(RegionRostro rostro, int anchoImagen, int altoImagen)
    {
        var extraX = (int)Math.Ceiling(rostro.Ancho * MargenSeguridad / 2);
        var extraY = (int)Math.Ceiling(rostro.Alto * MargenSeguridad / 2);

        var ampliada = new Rectangle(
            rostro.X - extraX,
            rostro.Y - extraY,
            rostro.Ancho + extraX * 2,
            rostro.Alto + extraY * 2);

        return Rectangle.Intersect(ampliada, new Rectangle(0, 0, anchoImagen, altoImagen));
    }
}
