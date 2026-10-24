using Vortice.Direct2D1;
using Vortice.Mathematics;

// 완성된 게임 화면 위에 옛날 TV 느낌을 덧그립니다.
internal sealed class CrtEffect : IDisposable
{
    private readonly ID2D1SolidColorBrush _brush;
    public bool Enabled { get; set; } = true;

    public CrtEffect(ID2D1RenderTarget target)
    {
        _brush = target.CreateSolidColorBrush(new Color4(0, 0, 0, 1));
    }

    public void Draw(ID2D1RenderTarget target, float width, float height, double time, bool grayscale = false)
    {
        if (!Enabled) return;

        // 청록색을 살짝 덮고 밝기를 아주 조금씩 바꿉니다.
        Fill(0, 0, width, height, grayscale
            ? new Color4(0.57f, 0.57f, 0.57f, 0.045f)
            : new Color4(0.05f, 0.8f, 0.75f, 0.045f));
        float dim = 0.025f + 0.015f * (float)Math.Sin(time * 9);
        Fill(0, 0, width, height, new Color4(0, 0, 0, dim));

        // 작은 격자를 곡선으로 그려 브라운관의 촘촘한 표면을 표현합니다.
        _brush.Color = new Color4(0, 0, 0, 0.38f);
        for (float y = 0; y <= height; y += 4)
        {
            for (float x = 0; x < width; x += 32)
                target.DrawLine(Curve(x, y), Curve(Math.Min(x + 32, width), y), _brush, 0.9f);
        }
        _brush.Color = new Color4(0, 0, 0, 0.30f);
        for (float x = 0; x <= width; x += 4)
        {
            for (float y = 0; y < height; y += 24)
                target.DrawLine(Curve(x, y), Curve(x, Math.Min(y + 24, height)), _brush, 0.8f);
        }
        float bandY = (float)(time * 35 % (height + 30)) - 30;
        Fill(0, bandY, width, 24, new Color4(1, 1, 1, 0.035f));

        // 잡음은 초당 12번만 바뀌므로 실행 속도에 따라 심하게 떨리지 않습니다.
        var noise = new Random((int)(time * 12 % int.MaxValue));
        for (int i = 0; i < 70; i++)
            Fill(noise.NextSingle() * width, noise.NextSingle() * height,
                2 + noise.NextSingle() * 8, 1, new Color4(1, 1, 1, 0.10f));

        // 모서리를 크게 둥글리고 경계 안쪽에 부드러운 검은 음영을 넣습니다.
        for (float y = 0; y < height; y += 1)
        {
            float distance = Math.Abs(y + 0.5f - height / 2) / (height / 2);
            float edge = width * (0.009f + 0.065f * MathF.Pow(distance, 8));
            var black = new Color4(0, 0, 0, 1);
            Fill(0, y, edge, 1, black);
            Fill(width - edge, y, edge, 1, black);
            for (int i = 0; i < 16; i++)
            {
                float opacity = 0.70f * MathF.Pow(1 - i / 16f, 2);
                var shadow = new Color4(0, 0, 0, opacity);
                Fill(edge + i * 2, y, 2, 1, shadow);
                Fill(width - edge - (i + 1) * 2, y, 2, 1, shadow);
            }
        }
        for (float x = 0; x < width; x += 2)
        {
            float distance = Math.Abs(x + 1 - width / 2) / (width / 2);
            float edge = height * (0.012f + 0.055f * MathF.Pow(distance, 4));
            Fill(x, 0, 2, edge, new Color4(0, 0, 0, 1));
            Fill(x, height - edge, 2, edge, new Color4(0, 0, 0, 1));
            for (int i = 0; i < 12; i++)
            {
                var shadow = new Color4(0, 0, 0, 0.65f * MathF.Pow(1 - i / 12f, 2));
                Fill(x, edge + i * 2, 2, 2, shadow);
                Fill(x, height - edge - (i + 1) * 2, 2, 2, shadow);
            }
        }

        // 가장자리로 갈수록 격자를 안쪽으로 휘게 만듭니다.
        System.Numerics.Vector2 Curve(float x, float y)
        {
            float nx = x / width * 2 - 1;
            float ny = y / height * 2 - 1;
            return new System.Numerics.Vector2(
                width / 2 + nx * width / 2 * (1 - 0.065f * ny * ny),
                height / 2 + ny * height / 2 * (1 - 0.065f * nx * nx));
        }

        void Fill(float x, float y, float w, float h, Color4 color)
        {
            _brush.Color = color;
            target.FillRectangle(new Rect(x, y, w, h), _brush);
        }
    }

    public void Dispose() => _brush.Dispose();
}
