using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;

// 게임이 잠깐 멈춘 동안에도 섬광과 흔들림은 계속 갱신합니다.
internal sealed class HitEffect : IDisposable
{
    private readonly ID2D1SolidColorBrush _brush;
    private readonly G2Texture _impactTexture;
    private Vector2 _position;
    private float _age = 1;
    private float _stopRemaining;
    private float _strength;
    private float _direction;
    private bool _lethal;
    private const float Duration = 0.24f;

    public HitEffect(ID2D1RenderTarget target)
    {
        _brush = target.CreateSolidColorBrush(new Color4(1, 1, 1, 1));
        _impactTexture = new G2Texture("resource/weapons/effect/All_Fire_Bullet_Pixel_16x16_00.png");
    }

    public void Trigger(Vector2 position, float direction, bool lethal)
    {
        _position = position;
        _direction = direction;
        _lethal = lethal;
        _age = 0;
        _strength = lethal ? 3.75f : 2.25f;
        _stopRemaining = lethal ? 0.075f : 0.04f;
    }

    public float Update(float elapsed)
    {
        _age += elapsed;
        float stopped = Math.Min(elapsed, _stopRemaining);
        _stopRemaining -= stopped;
        return elapsed - stopped;
    }

    public Vector2 Shake => _age >= Duration ? Vector2.Zero :
        new Vector2(MathF.Cos(_age * 145), MathF.Sin(_age * 190) * 0.6f)
        * (_strength * (1 - _age / Duration));

    public void Draw(ID2D1RenderTarget target, bool grayscale)
    {
        if (_age >= Duration) return;
        float fade = 1 - _age / Duration;
        // 둥근 폭발 대신 별 모양 타격 섬광이 짧게 튀었다가 작아집니다.
        const float flashDuration = 0.14f;
        if (_age < flashDuration)
        {
            float progress = _age / flashDuration;
            int frame = Math.Min(3, (int)(progress * 4));
            float size = (_lethal ? 48 : 36) * (1 - progress * 0.55f);
            _impactTexture.Draw(
                new Rect(_position.X - size / 2, _position.Y - size / 2, size, size),
                new Rect(96 + frame * 16, 80, 16, 16), opacity: 1 - progress,
                interpolationMode: BitmapInterpolationMode.NearestNeighbor);
        }
        _brush.Color = grayscale ? new Color4(1, 1, 1, fade)
            : new Color4(1, 0.8f, 0.35f, fade);
        // 탄환 진행 방향으로 가는 불꽃 조각을 흩뿌립니다.
        for (int i = 0; i < 7; i++)
        {
            float angle = (i - 3) * 0.38f;
            Vector2 ray = new(MathF.Cos(angle) * _direction, MathF.Sin(angle));
            Vector2 drift = new(0, _age * _age * 120);
            float radius = 4 + _age * (i % 2 == 0 ? 125 : 85) * (_lethal ? 1.2f : 1);
            target.DrawLine(_position + drift + ray * radius,
                _position + drift + ray * (radius + 7 * fade), _brush, fade + 0.6f);
        }
        if (_age < 0.08f)
        {
            _brush.Color = new Color4(1, 1, 1, 1 - _age / 0.08f);
            target.DrawLine(_position - new Vector2(10, 0), _position + new Vector2(10, 0), _brush, 5);
            target.DrawLine(_position - new Vector2(0, 10), _position + new Vector2(0, 10), _brush, 4);
        }
    }

    public void Dispose()
    {
        _impactTexture.Dispose();
        _brush.Dispose();
    }
}
