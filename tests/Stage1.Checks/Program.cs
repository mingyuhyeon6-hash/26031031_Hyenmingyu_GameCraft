using System.Numerics;

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
    Console.WriteLine($"PASS: {description}");
}

void Defeat(EnemyCombat enemy)
{
    Vector2 from = enemy.Position + new Vector2(-100, 90);
    Vector2 to = enemy.Position + new Vector2(200, 90);
    for (int i = 0; i < 3; i++) enemy.TryHit(from, to);
}

var stage = new Stage1Session();
stage.CheckOutcome();
Check(!stage.Finished && stage.DefeatedCount == 0 && stage.Enemies.Count == 4,
    "A new stage starts in combat with four living enemies.");
stage.AdvanceTime(12.5f);
foreach (EnemyCombat enemy in stage.Enemies.Take(3)) Defeat(enemy);
stage.CheckOutcome();
Check(!stage.Finished && stage.DefeatedCount == 3, "Three kills do not clear a four-enemy stage.");
Defeat(stage.Enemies[3]);
stage.CheckOutcome();
Check(stage.Result == Stage1Session.Outcome.Cleared, "The fourth kill clears the stage.");
stage.AdvanceTime(20);
Check(stage.PlaySeconds == 12.5, "The record stops when the result is shown.");
foreach (EnemyCombat enemy in stage.Enemies) enemy.Update(30, Stage1Session.PlayerStart.X);
Check(stage.Enemies.All(enemy => enemy.IsDead && enemy.DeathTime == EnemyCombat.DeathDuration),
    "Dead enemies finish their animation and never respawn automatically.");
stage.Reset();
Check(!stage.Finished && stage.PlaySeconds == 0 && stage.Player.Health == 100
    && stage.Enemies.All(enemy => enemy.Health == EnemyCombat.MaxHealth && enemy.LastDamage == 0)
    && stage.Enemies[0].Position == new Vector2(450, 210),
    "Restart resets health, enemy positions, damage, progress and time.");
Check(stage.Player.TakeHit() && !stage.Player.TakeHit(), "Player invulnerability prevents repeated hits in one instant.");
for (int i = 0; i < 9; i++) { stage.Player.Update(1); stage.Player.TakeHit(); }
stage.CheckOutcome();
Check(stage.Result == Stage1Session.Outcome.Defeated, "Zero player health produces a failure result.");
foreach (EnemyCombat enemy in stage.Enemies) Defeat(enemy);
stage.CheckOutcome();
Check(stage.Result == Stage1Session.Outcome.Defeated, "A failure result cannot turn into a clear afterwards.");
stage.Reset();
Check(stage.Player.Invulnerability == 0 && stage.Player.FlashRemaining == 0,
    "Restart clears player hit effects and invulnerability.");
foreach (EnemyCombat enemy in stage.Enemies)
{
    float bulletY = Math.Clamp(enemy.Position.Y + 90, 330 - 128 + 90, 400 - 128 + 90);
    Check(enemy.HitFraction(new Vector2(0, bulletY), new Vector2(1200, bulletY)).HasValue,
        $"Enemy at X={enemy.Position.X} is hittable from the playable road.");
}
Check(PlayerCombat.Intersects(new Vector2(-100, -100), new Vector2(500, 500), Vector2.Zero),
    "Swept diagonal enemy bullets hit the player between frames.");
Check(!PlayerCombat.Intersects(new Vector2(-100, 10), new Vector2(500, 10), Vector2.Zero),
    "A bullet above the player's body misses.");
Console.WriteLine($"All {checks} checks passed.");
