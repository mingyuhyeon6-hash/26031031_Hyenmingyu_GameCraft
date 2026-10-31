using System.Numerics;

// 화면 출력과 분리한 적의 체력, 피격 시간, 총알 충돌 처리입니다.
internal sealed class EnemyCombat
{
    public const int MaxHealth = 100;
    public const int BulletDamage = 40;
    public const double HurtDuration = 0.4;
    public const double DeathDuration = 0.5;
    public const double RespawnDelay = 3;
    public const double FlashDuration = 0.06;
    private readonly Vector2 _spawnPosition;
    private double _deadElapsed;
    public double FlashRemaining { get; private set; }
    public double RespawnRemaining => IsDead ? Math.Max(0, RespawnDelay - _deadElapsed) : 0;
    public Vector2 Position { get; private set; }
    private double _fireCooldown;
    public double ShotTime { get; private set; } = 1;
    public bool IsAiming => !IsDead && HitTime >= HurtDuration && _fireCooldown <= 0.45;
    public Vector2 Muzzle => Position + new Vector2(FacesLeft ? 24 : 104, 75);

    public EnemyCombat(Vector2 position)
    {
        _spawnPosition = Position = position;
        _fireCooldown = 1.5 + position.X / 600;
    }

    // 히트스톱 중에도 섬광은 실제 시간 기준으로 짧게 끝납니다.
    public void UpdateFlash(float elapsed) => FlashRemaining = Math.Max(0, FlashRemaining - elapsed);

    public void Respawn(float playerX)
    {
        Position = _spawnPosition;
        Health = MaxHealth;
        HitTime = 1;
        DeathTime = _deadElapsed = FlashRemaining = 0;
        LastDamage = 0;
        _knockbackVelocity = 0;
        FacesLeft = playerX < Position.X;
        _fireCooldown = 1.5 + Position.X / 600;
        ShotTime = 1;
    }
    private float _knockbackVelocity;
    public int Health { get; private set; } = MaxHealth;
    public bool IsDead => Health == 0;
    public bool FacesLeft { get; private set; } = true;
    public double HitTime { get; private set; } = 1;
    public double DeathTime { get; private set; }
    public int LastDamage { get; private set; }

    public bool TryShoot(float elapsed, Vector2 playerPosition, out Vector2 velocity)
    {
        velocity = Vector2.Zero;
        ShotTime += elapsed;
        if (IsDead || HitTime < HurtDuration) return false;
        _fireCooldown -= elapsed;
        if (_fireCooldown > 0) return false;
        Vector2 aim = playerPosition + new Vector2(64, 90) - Muzzle;
        if (aim.LengthSquared() < 1) return false;
        velocity = Vector2.Normalize(aim) * 260;
        _fireCooldown = 1.8 + _spawnPosition.X / 2000;
        ShotTime = 0;
        return true;
    }

    public void Update(float elapsed, float playerX, float maxX = 1072)
    {
        // 감속하며 밀려나고, 화면 가장자리에서는 더 밀리지 않습니다.
        float decay = MathF.Exp(-10 * elapsed);
        Position = new Vector2(Math.Clamp(Position.X + _knockbackVelocity * (1 - decay) / 10, 0, maxX), Position.Y);
        _knockbackVelocity *= decay;
        HitTime += elapsed;
        if (IsDead)
        {
            DeathTime = Math.Min(DeathDuration, DeathTime + elapsed);
            _deadElapsed += elapsed;
            if (_deadElapsed >= RespawnDelay) Respawn(playerX);
        }
        else
            FacesLeft = playerX < Position.X;
    }

    public float? HitFraction(Vector2 previous, Vector2 current)
    {
        if (IsDead) return null;

        // 투명한 여백을 제외한 몸통 영역과 총알이 지나간 구간을 비교합니다.
        // 프레임 사이에 빠른 총알이 몸통을 건너뛰어도 명중합니다.
        float left = Position.X + 46;
        float right = Position.X + 82;
        if (current.Y < Position.Y + 58 || current.Y > Position.Y + 124
            || Math.Max(previous.X, current.X) < left
            || Math.Min(previous.X, current.X) > right)
            return null;

        float distance = current.X - previous.X;
        if (distance == 0) return 0;
        float entry = distance > 0 ? left : right;
        return Math.Clamp((entry - previous.X) / distance, 0, 1);
    }

    public bool TryHit(Vector2 previous, Vector2 current)
    {
        if (HitFraction(previous, current) == null) return false;

        LastDamage = Math.Min(Health, BulletDamage);
        Health -= LastDamage;
        HitTime = 0;
        FlashRemaining = FlashDuration;
        _fireCooldown = Math.Max(_fireCooldown, 0.6);
        ShotTime = 1;
        _knockbackVelocity = Math.Sign(current.X - previous.X) * (IsDead ? 165 : 105);
        return true;
    }
}
