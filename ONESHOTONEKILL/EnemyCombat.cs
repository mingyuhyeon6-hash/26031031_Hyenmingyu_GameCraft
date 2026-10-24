using System.Numerics;

// 화면 출력과 분리한 적의 체력, 피격 시간, 총알 충돌 처리입니다.
internal sealed class EnemyCombat
{
    public const int MaxHealth = 100;
    public const int BulletDamage = 40;
    public const double HurtDuration = 0.4;
    public const double DeathDuration = 0.5;
    public Vector2 Position { get; } = new(900, 222);
    public int Health { get; private set; } = MaxHealth;
    public bool IsDead => Health == 0;
    public bool FacesLeft { get; private set; } = true;
    public double HitTime { get; private set; } = 1;
    public double DeathTime { get; private set; }
    public int LastDamage { get; private set; }

    public void Update(float elapsed, float playerX)
    {
        HitTime += elapsed;
        if (IsDead)
            DeathTime = Math.Min(DeathDuration, DeathTime + elapsed);
        else
            FacesLeft = playerX < Position.X;
    }

    public bool TryHit(Vector2 previous, Vector2 current)
    {
        if (IsDead) return false;

        // 투명한 여백을 제외한 몸통 영역과 총알이 지나간 구간을 비교합니다.
        // 프레임 사이에 빠른 총알이 몸통을 건너뛰어도 명중합니다.
        float left = Position.X + 46;
        float right = Position.X + 82;
        if (current.Y < Position.Y + 58 || current.Y > Position.Y + 124
            || Math.Max(previous.X, current.X) < left
            || Math.Min(previous.X, current.X) > right)
            return false;

        LastDamage = Math.Min(Health, BulletDamage);
        Health -= LastDamage;
        HitTime = 0;
        return true;
    }
}
