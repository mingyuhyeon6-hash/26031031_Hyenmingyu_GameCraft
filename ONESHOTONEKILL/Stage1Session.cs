using System.Numerics;

// 스테이지의 목표와 종료 상태를 렌더링·입력과 분리합니다.
internal sealed class Stage1Session
{
    public enum Outcome { Playing, Cleared, Defeated }

    public static readonly Vector2 PlayerStart = new(120, 222);
    public PlayerCombat Player { get; } = new();
    public IReadOnlyList<EnemyCombat> Enemies { get; } = new EnemyCombat[]
    {
        new(new Vector2(450, 210)),
        new(new Vector2(630, 250)),
        new(new Vector2(810, 222)),
        new(new Vector2(990, 265))
    };
    public Outcome Result { get; private set; }
    public bool Finished => Result != Outcome.Playing;
    public int DefeatedCount => Enemies.Count(enemy => enemy.IsDead);
    public double PlaySeconds { get; private set; }

    public void AdvanceTime(float elapsed)
    {
        if (!Finished) PlaySeconds += elapsed;
    }

    public void CheckOutcome()
    {
        if (Finished) return;
        if (Player.IsDead) Result = Outcome.Defeated;
        else if (DefeatedCount == Enemies.Count) Result = Outcome.Cleared;
    }

    public void Reset()
    {
        Player.Reset();
        foreach (EnemyCombat enemy in Enemies) enemy.Reset(PlayerStart.X);
        Result = Outcome.Playing;
        PlaySeconds = 0;
    }
}
