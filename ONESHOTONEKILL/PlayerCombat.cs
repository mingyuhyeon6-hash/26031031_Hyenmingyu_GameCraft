using System.Numerics;

internal sealed class PlayerCombat
{
    public int Health { get; private set; } = 100;
    public bool IsDead => Health == 0;
    public float Invulnerability { get; private set; }
    public float FlashRemaining { get; private set; }

    public void Reset()
    {
        Health = 100;
        Invulnerability = FlashRemaining = 0;
    }

    public void Update(float elapsed)
    {
        Invulnerability = Math.Max(0, Invulnerability - elapsed);
        FlashRemaining = Math.Max(0, FlashRemaining - elapsed);
    }

    // 대각선 탄환의 이동 선분을 몸통 사각형과 비교합니다.
    public static bool Intersects(Vector2 from, Vector2 to, Vector2 position)
    {
        Vector2 min = position + new Vector2(46, 58);
        Vector2 max = position + new Vector2(82, 124);
        Vector2 delta = to - from;
        float enter = 0, exit = 1;
        return Clip(from.X, delta.X, min.X, max.X, ref enter, ref exit)
            && Clip(from.Y, delta.Y, min.Y, max.Y, ref enter, ref exit);
    }

    private static bool Clip(float start, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (Math.Abs(delta) < 0.00001f) return start >= min && start <= max;
        float a = (min - start) / delta, b = (max - start) / delta;
        enter = Math.Max(enter, Math.Min(a, b));
        exit = Math.Min(exit, Math.Max(a, b));
        return enter <= exit;
    }

    public bool TakeHit()
    {
        if (IsDead || Invulnerability > 0) return false;
        Health = Math.Max(0, Health - 10);
        Invulnerability = 0.6f;
        FlashRemaining = 0.06f;
        return true;
    }
}
