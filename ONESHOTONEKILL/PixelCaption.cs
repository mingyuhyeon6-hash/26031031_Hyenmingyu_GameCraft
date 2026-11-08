using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Vortice.Direct2D1;
using Vortice.Mathematics;

// Rasterize Korean glyphs once at 16 px, then enlarge with nearest-neighbor.
// No machine-wide font installation or external font download is needed.
internal sealed class PixelCaption : IDisposable
{
    private const int Cell = 24;
    private readonly string _glyphs;
    private readonly ID2D1Bitmap _atlas;

    public PixelCaption(ID2D1RenderTarget target, IEnumerable<string> lines)
    {
        _glyphs = new string(string.Concat(lines).Distinct().ToArray());
        using var bitmap = new Bitmap(Cell * _glyphs.Length, Cell, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font("GulimChe", 16, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(System.Drawing.Color.FromArgb(230, 222, 198)))
        {
            graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            for (int i = 0; i < _glyphs.Length; i++)
                graphics.DrawString(_glyphs[i].ToString(), font, brush,
                    new PointF(i * Cell + 2, 1), StringFormat.GenericTypographic);
        }
        var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            _atlas = target.CreateBitmap(new SizeI(bitmap.Width, bitmap.Height), bits.Scan0, (uint)bits.Stride,
                new BitmapProperties(new Vortice.DCommon.PixelFormat(
                    Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied)));
        }
        finally { bitmap.UnlockBits(bits); }
    }

    public void Draw(ID2D1RenderTarget target, string line, int count, float opacity, float y = 180)
    {
        // Center the completed sentence; its first letter stays still while typing.
        float width = line.Sum(c => c < 128 ? 16 : 32);
        float x = (1200 - width) / 2;
        for (int i = 0; i < Math.Min(count, line.Length); i++)
        {
            int index = _glyphs.IndexOf(line[i]);
            target.DrawBitmap(_atlas, new Rect(x - 4, y, Cell * 2, Cell * 2), opacity,
                BitmapInterpolationMode.NearestNeighbor, new Rect(index * Cell, 0, Cell, Cell));
            x += line[i] < 128 ? 16 : 32;
        }
    }

    public void Dispose() => _atlas.Dispose();
}
