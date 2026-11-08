using System.Numerics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

internal sealed class DrivingScene : IDisposable
{
    private readonly G2Texture _bridge = new("resource/backgrounds/MAP/city-bridge-driving-v4-night.png");
    private readonly G2Texture _car = new("resource/backgrounds/car/vintage_coupe_left_v1.png");
    private readonly ID2D1SolidColorBrush _brush;
    private readonly ID2D1Bitmap _titleOutline;
    private readonly DrivingAudio _drivingAudio = new();
    private float _speed;
    private float _roadOffset;
    private float _laneY = 351;
    private float _elapsed;
    public float Speed => _speed;

    public DrivingScene(ID2D1RenderTarget target)
    {
        _brush = target.CreateSolidColorBrush(new Color4(0, 0, 0, 1));
        _titleOutline = CreateTitleOutline(target);
    }

    private static ID2D1Bitmap CreateTitleOutline(ID2D1RenderTarget target)
    {
        const string title = "LIFE OF MAFIA";
        using var bitmap = new Bitmap(1200, 400, PixelFormat.Format32bppPArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (var family = new FontFamily("Georgia"))
        using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
        using (var path = new GraphicsPath())
        using (var shadow = new Pen(System.Drawing.Color.FromArgb(220, 0, 0, 0), 8f))
        using (var pen = new Pen(System.Drawing.Color.FromArgb(255, 245, 220), 4f))
        {
            graphics.Clear(System.Drawing.Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            path.AddString(title, family, (int)(System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic), 94f,
                new RectangleF(35, 115, 1130, 170), format);
            pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
            shadow.LineJoin = pen.LineJoin;
            graphics.DrawPath(shadow, path);
            graphics.DrawPath(pen, path);
        }

        var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            return target.CreateBitmap(new Vortice.Mathematics.SizeI(bitmap.Width, bitmap.Height),
                bits.Scan0, (uint)bits.Stride,
                new BitmapProperties(new Vortice.DCommon.PixelFormat(
                    Vortice.DXGI.Format.B8G8R8A8_UNorm,
                    Vortice.DCommon.AlphaMode.Premultiplied)));
        }
        finally { bitmap.UnlockBits(bits); }
    }

    public void Start()
    {
        _speed = -650;
        _roadOffset = 0;
        _laneY = 351;
        _elapsed = 0;

        _drivingAudio.Start();
    }
    public void Stop()
    {
        _drivingAudio.Stop();
    }

    public void Update(double delta)
    {
        float dt = (float)Math.Clamp(delta, 0, 0.05);
        // Cinematic tracking shot: the car is already moving as the scene opens.
        _speed = Approach(_speed, -840, 95 * dt);
        _roadOffset = ((_roadOffset - _speed * dt) % 1200 + 1200) % 1200;
        _drivingAudio.Update(_speed, _elapsed < 2);
        _elapsed += dt;
    }
    private static float Approach(float value, float target, float delta) => value < target
        ? Math.Min(value + delta, target) : Math.Max(value - delta, target);

    public void Draw(ID2D1RenderTarget target)
    {
        // Repeat the supplied panorama in the direction opposite to the car.
        _bridge.Draw(new Rect(_roadOffset - 1200, 0, 1201, 400), new Rect(0, 0, 2170, 725));
        _bridge.Draw(new Rect(_roadOffset, 0, 1201, 400), new Rect(0, 0, 2170, 725));
        float intensity = Math.Clamp(Math.Abs(_speed) / 840, 0, 1);
        float x = 515 + _speed * 0.055f;
        float bounce = MathF.Sin(_elapsed * 42) * intensity * 0.7f;
        // Thin streaks on the asphalt reinforce acceleration without hiding the scenery.
        if (intensity > 0.35f)
        {
            _brush.Color = new Color4(0.55f, 0.67f, 0.85f, (intensity - 0.35f) * 0.2f);
            for (int i = 0; i < 16; i++)
            {
                float streakX = (i * 193 + _roadOffset * 1.6f) % 1200;
                float y = 301 + i * 29 % 94;
                target.DrawLine(new Vector2(streakX, y), new Vector2(streakX + intensity * 65, y), _brush, 1);
            }
        }
        float carWidth = 265;
        float carHeight = carWidth * 560 / 1940;
        _brush.Color = new Color4(0, 0, 0, 0.35f);
        target.FillEllipse(new Ellipse(new Vector2(x + carWidth / 2, _laneY - 2), 118, 7), _brush);
        _car.Draw(new Rect(x, _laneY - carHeight + bounce, carWidth, carHeight), new Rect(25, 161, 1940, 560),
            interpolationMode: BitmapInterpolationMode.NearestNeighbor);
        _brush.Color = new Color4(0, 0, 0, 1);
        target.FillRectangle(new Rect(0, 0, 1200, 16), _brush);
        target.FillRectangle(new Rect(0, 384, 1200, 16), _brush);
        // Reveal the car shot from black.
        if (_elapsed < 0.45f)
        {
            _brush.Color = new Color4(0, 0, 0, 1 - _elapsed / 0.45f);
            target.FillRectangle(new Rect(0, 0, 1200, 400), _brush);
        }
        const float titleDelay = 4f;
        if (_elapsed >= titleDelay)
        {
            float titleOpacity = Math.Clamp((_elapsed - titleDelay) / 0.4f, 0, 1);
            target.DrawBitmap(_titleOutline, titleOpacity, BitmapInterpolationMode.Linear);
        }
    }

    public void Dispose()
    {
        _drivingAudio.Dispose(); _titleOutline.Dispose(); _brush.Dispose(); _bridge.Dispose(); _car.Dispose();
    }
}
