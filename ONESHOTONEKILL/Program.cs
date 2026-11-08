
using System.Windows.Forms;
using Vortice.Direct2D1;
using System.Numerics;
using Vortice.Mathematics;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var game = new Stage1Game();
        game.Run();
    }
}

internal sealed class Stage1Game : G2AppBase
{
    private G2Texture? _background;
    private G2Texture? _crow;
    private const int CrowFrameSize = 630;
    // 멀리 있는 배경 새처럼 작고 옅게, 천천히 표시합니다.
    private const float CrowDrawSize = 32f;
    private const float CrowSpeed = 30f;
    private const float CrowOpacity = 0.6f;
    private float _crowX = 200f;
    private double _crowAnimationTime;
    private G2Texture? _gangster;
    private G2Texture? _gangsterWalk;
    private G2Texture? _gangsterShot;
    private G2Texture? _enemyIdle;
    private G2Texture? _enemyHurt;
    private G2Texture? _enemyDead;
    private G2Texture? _enemyAttack;
    private G2Texture? _enemyGun;
    private readonly Stage1Session _stage = new();
    private PlayerCombat _player => _stage.Player;
    private bool _paused;
    private bool _focusPaused;
    private G2Font? _titleFont;
    private G2Font? _messageFont;
    private ID2D1SolidColorBrush? _hudBrush;
    private readonly List<(Vector2 Position, Vector2 Velocity)> _enemyBullets = new();
    private const int EnemyFrameSize = 128;
    private const int EnemyIdleFrameCount = 7;
    private IReadOnlyList<EnemyCombat> _enemies => _stage.Enemies;
    private double _enemyAnimationTime;
    private G2Texture? _bulletEffect;
    private CrtEffect? _crtEffect;
    private HitEffect? _hitEffect;
    private G2Audio? _backgroundMusic;
    private int _musicVolumePercent = 100;
    private G2Font? _volumeFont;
    private readonly List<G2Audio> _footstepSounds = new();
    private int _nextFootstepSound;
    private double _footstepCooldown;
    // 걷기 동작 10장이 1초에 재생되므로 반 주기마다 발을 디딥니다.
    private const double FootstepInterval = 0.5;
    private readonly List<G2Audio> _shotSounds = new();
    private int _nextShotSound;
    private readonly List<(Vector2 Position, float Direction, float Age, int FlameOffset)> _bullets = new();
    private Vector2 _muzzlePosition;
    private float _muzzleDirection = 1;
    private const double MuzzleFlashDuration = 0.09;
    private const float BulletSpeed = 650f;
    private const double ShotInterval = 0.2;
    private const double ShotDuration = 0.2;
    private double _shotCooldown;
    private double _shotTime = ShotDuration;
    private const int IdleFrameSize = 128;
    private const int IdleFrameCount = 6;
    private const int WalkFrameCount = 10;
    private const float MoveSpeed = 130f;
    // 1200 x 400 화면에서 도로 위쪽 경계입니다. 캐릭터의 발을 기준으로 막습니다.
    private const float RoadTopY = 330f;
    private Vector2 _position = Stage1Session.PlayerStart;
    private bool _isMoving;
    private bool _facesLeft;
    private double _animationTime;

    public override System.Drawing.Size ScreenSize => new(1200, 400);
    public override string GameName => "LIFE OF MAFIA - Stage 1 Demo";

    protected override void Initialize()
    {
        _volumeFont = new G2Font("Malgun Gothic", 16);
        _titleFont = new G2Font("Malgun Gothic", 36,
            textAlignment: Vortice.DirectWrite.TextAlignment.Center);
        _messageFont = new G2Font("Malgun Gothic", 18,
            textAlignment: Vortice.DirectWrite.TextAlignment.Center);
        _hudBrush = RenderTarget.CreateSolidColorBrush(new Color4(0, 0, 0, 0.72f));
        _crtEffect = new CrtEffect(RenderTarget);
        _hitEffect = new HitEffect(RenderTarget);
        _background = new G2Texture("resource/backgrounds/stage1.png");
        _crow = new G2Texture("resource/backgrounds/crow/crow_fly.png");
        _gangster = new G2Texture("resource/character/Gangsters_1/Idle.png");
        _gangsterWalk = new G2Texture("resource/character/Gangsters_1/Walk.png");
        _gangsterShot = new G2Texture("resource/character/Gangsters_1/Shot.png");
        _enemyIdle = new G2Texture("resource/character/Gangsters_2/Idle.png");
        _enemyHurt = new G2Texture("resource/character/Gangsters_2/Hurt.png");
        _enemyDead = new G2Texture("resource/character/Gangsters_2/Dead.png");
        _enemyAttack = new G2Texture("resource/character/Gangsters_2/Attack_2.png");
        _enemyGun = new G2Texture("resource/weapons/guns/Pistol-1.png");
        _bulletEffect = new G2Texture("resource/weapons/effect/All_Fire_Bullet_Pixel_16x16_00.png");
        if (G2AudioContext.Instance?.Audio != null)
        {
            // 게임이 시작되면 배경음악을 반복해서 재생합니다.
            _backgroundMusic = new G2Audio("resource/music/Mansion Waltz.mp3");
            _backgroundMusic.SetVolume(_musicVolumePercent / 100f);
            _backgroundMusic.Play(isLooping: true);

            _footstepSounds.Add(new G2Audio("resource/music/footsteps/stone_walk_left.wav"));
            _footstepSounds.Add(new G2Audio("resource/music/footsteps/stone_walk_right.wav"));

            // 2초짜리 소리를 0.2초마다 재생해도 이전 총성의 잔향이 끊기지 않게 합니다.
            for (int i = 0; i < 12; i++)
            {
                _shotSounds.Add(new G2Audio(
                    "resource/music/gun sound/Snake's Authentic Gun Sounds/Full Sound/7.62x54R/WAV/762x54r Single WAV.wav"));
            }
        }
    }

    protected override void Update()
    {
        _focusPaused = Form.ActiveForm == null;
        if (_focusPaused) return;

        // 배경음악만 10%씩 조절하며, 총성과 발소리는 그대로 유지합니다.
        if (Form.ActiveForm != null)
        {
            int change = (Input.IsKeyDown(Keys.F6) ? 10 : 0)
                - (Input.IsKeyDown(Keys.F5) ? 10 : 0);
            if (change != 0)
            {
                _musicVolumePercent = Math.Clamp(_musicVolumePercent + change, 0, 100);
                _backgroundMusic?.SetVolume(_musicVolumePercent / 100f);
            }
        }
        // = 키를 누를 때마다 컬러와 흑백을 전환합니다.
        if (Form.ActiveForm != null && Input.IsKeyDown(Keys.Oemplus))
            GrayscaleEnabled = !GrayscaleEnabled;
        // F2를 한 번 누를 때마다 TV 효과를 켜거나 끕니다.
        if (Form.ActiveForm != null && Input.IsKeyDown(Keys.F2) && _crtEffect != null)
            _crtEffect.Enabled = !_crtEffect.Enabled;
        if (Input.IsKeyDown(Keys.R))
        {
            RestartStage();
            return;
        }
        if (Input.IsKeyDown(Keys.Escape) && !_stage.Finished) _paused = !_paused;
        if (_paused) return;

        float elapsed = (float)Math.Min(DeltaTime, 0.05);
        if (_stage.Finished)
        {
            _hitEffect?.Update(elapsed);
            foreach (EnemyCombat enemy in _enemies)
            {
                enemy.UpdateFlash(elapsed);
                if (enemy.IsDead) enemy.Update(elapsed, _position.X);
            }
            return;
        }
        bool shootHeld = !_player.IsDead && (IsHeld(Keys.Z) || IsHeld(Keys.Space));
        // 연사 사이에도 멈춰 있고, 키를 놓아도 마지막 사격 동작이 끝날 때까지 이동을 막습니다.
        bool movementLocked = _player.IsDead || shootHeld || _shotTime < ShotDuration;
        Vector2 direction = Vector2.Zero;
        if (Form.ActiveForm != null && !movementLocked)
        {
            if (IsHeld(Keys.Left) || IsHeld(Keys.A)) direction.X--;
            if (IsHeld(Keys.Right) || IsHeld(Keys.D)) direction.X++;
            if (IsHeld(Keys.Up) || IsHeld(Keys.W)) direction.Y--;
            if (IsHeld(Keys.Down) || IsHeld(Keys.S)) direction.Y++;
        }

        // 대각선 이동도 같은 속도로 유지하고, 긴 프레임 뒤의 순간 이동을 방지합니다.
        _stage.AdvanceTime(elapsed);
        _player.Update(elapsed);
        foreach (EnemyCombat enemy in _enemies) enemy.UpdateFlash(elapsed);
        elapsed = _hitEffect?.Update(elapsed) ?? elapsed;
        if (elapsed <= 0) return;
        _enemyAnimationTime += elapsed;
        _crowAnimationTime += elapsed;
        _crowX += CrowSpeed * elapsed;
        if (_crowX > ScreenSize.Width)
            _crowX = -CrowDrawSize;
        Vector2 previousPosition = _position;
        if (direction != Vector2.Zero)
        {
            _position += Vector2.Normalize(direction) * MoveSpeed * elapsed;
            _position.X = Math.Clamp(_position.X, 0, ScreenSize.Width - IdleFrameSize);
            _position.Y = Math.Clamp(
                _position.Y, RoadTopY - IdleFrameSize, ScreenSize.Height - IdleFrameSize);
            if (direction.X != 0) _facesLeft = direction.X < 0;
        }

        bool moving = _position != previousPosition;
        if (moving != _isMoving) _animationTime = 0;
        _isMoving = moving;
        _animationTime += elapsed;

        // 벽에 막히거나 사격 중일 때는 실제 이동이 없으므로 새 발소리를 내지 않습니다.
        if (_isMoving)
        {
            _footstepCooldown -= elapsed;
            if (_footstepCooldown <= 0)
            {
                if (_footstepSounds.Count > 0)
                {
                    _footstepSounds[_nextFootstepSound].Play();
                    _nextFootstepSound = (_nextFootstepSound + 1) % _footstepSounds.Count;
                }
                _footstepCooldown += FootstepInterval;
            }
        }
        else
        {
            _footstepCooldown = 0;
        }

        foreach (EnemyCombat enemy in _enemies)
            enemy.Update(elapsed, _position.X, ScreenSize.Width - EnemyFrameSize);

        // 명중한 총알은 바로 제거하여 한 발이 여러 번 피해를 주지 않게 합니다.
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            bullet.Age += elapsed;
            Vector2 previousBulletPosition = bullet.Position;
            bullet.Position.X += bullet.Direction * BulletSpeed * elapsed;
            // 목록 순서 대신 총알 진행 방향에서 가장 먼저 만난 적을 고릅니다.
            EnemyCombat? target = null;
            float nearest = float.PositiveInfinity;
            foreach (EnemyCombat enemy in _enemies)
            {
                float? fraction = enemy.HitFraction(previousBulletPosition, bullet.Position);
                if (fraction.HasValue && fraction.Value < nearest)
                {
                    target = enemy;
                    nearest = fraction.Value;
                }
            }
            if (target != null && target.TryHit(previousBulletPosition, bullet.Position))
            {
                Vector2 impact = Vector2.Lerp(previousBulletPosition, bullet.Position, nearest);
                _hitEffect?.Trigger(impact, bullet.Direction, target.IsDead);
                _bullets.RemoveAt(i);
            }
            else if (bullet.Position.X < -32 || bullet.Position.X > ScreenSize.Width + 32)
                _bullets.RemoveAt(i);
            else
                _bullets[i] = bullet;
        }

        _stage.CheckOutcome();
        if (_stage.Finished)
        {
            FinishStage();
            return;
        }
        _shotTime += elapsed;
        UpdateEnemyFire(elapsed);
        _stage.CheckOutcome();
        if (_stage.Finished)
        {
            FinishStage();
            return;
        }
        _shotCooldown = Math.Max(0, _shotCooldown - elapsed);
        if (!_player.IsDead && shootHeld && _shotCooldown <= 0)
        {
            float bulletDirection = _facesLeft ? -1f : 1f;
            Vector2 muzzle = _position + new Vector2(_facesLeft ? 48 : 80, 90);
            _bullets.Add((muzzle, bulletDirection, 0f, Random.Shared.Next(4)));
            _muzzlePosition = muzzle;
            _muzzleDirection = bulletDirection;
            if (_shotSounds.Count > 0)
            {
                _shotSounds[_nextShotSound].Play();
                _nextShotSound = (_nextShotSound + 1) % _shotSounds.Count;
            }
            _shotTime = 0;
            _shotCooldown = ShotInterval;
        }
    }

    private bool IsHeld(Keys key) => Input.IsKeyDown(key) || Input.IsKeyPress(key);

    private void FinishStage()
    {
        _enemyBullets.Clear();
        _bullets.Clear();
        _isMoving = false;
        _shotTime = ShotDuration;
        foreach (G2Audio sound in _shotSounds) sound.Stop();
        foreach (G2Audio sound in _footstepSounds) sound.Stop();
    }

    private void RestartStage()
    {
        FinishStage();
        _stage.Reset();
        _hitEffect?.Reset();
        _position = Stage1Session.PlayerStart;
        _facesLeft = false;
        _paused = false;
        _animationTime = _enemyAnimationTime = _crowAnimationTime = 0;
        _crowX = 200;
        _shotCooldown = _footstepCooldown = 0;
        _nextShotSound = _nextFootstepSound = 0;
    }

    private void UpdateEnemyFire(float elapsed)
    {
        if (_player.IsDead) return;
        // 먼저 날아가던 탄환을 처리한 뒤 살아 있는 적만 새 탄환을 발사합니다.
        for (int i = _enemyBullets.Count - 1; i >= 0; i--)
        {
            var bullet = _enemyBullets[i];
            Vector2 previous = bullet.Position;
            bullet.Position += bullet.Velocity * elapsed;
            if (PlayerCombat.Intersects(previous, bullet.Position, _position))
            {
                _enemyBullets.RemoveAt(i);
                if (_player.TakeHit())
                    _hitEffect?.Trigger(_position + new Vector2(64, 90), Math.Sign(bullet.Velocity.X), _player.IsDead);
                if (_player.IsDead)
                {
                    _enemyBullets.Clear();
                    _bullets.Clear();
                    _isMoving = false;
                    _shotTime = ShotDuration;
                    return;
                }
            }
            else if (bullet.Position.X < -32 || bullet.Position.X > ScreenSize.Width + 32
                || bullet.Position.Y < -32 || bullet.Position.Y > ScreenSize.Height + 32)
                _enemyBullets.RemoveAt(i);
            else
                _enemyBullets[i] = bullet;
        }
        if (Form.ActiveForm == null) return;
        foreach (EnemyCombat enemy in _enemies)
        {
            if (!enemy.TryShoot(elapsed, _position, out Vector2 velocity)) continue;
            _enemyBullets.Add((enemy.Muzzle, velocity));
            if (_shotSounds.Count > 0)
            {
                _shotSounds[_nextShotSound].Play();
                _nextShotSound = (_nextShotSound + 1) % _shotSounds.Count;
            }
        }
    }

    protected override void Render()
    {
        Matrix3x2 screenTransform = RenderTarget.Transform;
        Vector2 shake = _hitEffect?.Shake ?? Vector2.Zero;
        RenderTarget.Transform = Matrix3x2.CreateTranslation(shake) * screenTransform;
        // 원본 배경(2172 x 724)을 화면 전체에 표시합니다.
        _background?.Draw(
            // 흔들릴 때 화면 가장자리가 비지 않도록 배경에 여유를 둡니다.
            new Rect(-6, -6, ScreenSize.Width + 12, ScreenSize.Height + 12),
            new Rect(0, 0, 2172, 724));

        // 2 x 2 이미지에서 한 장씩 잘라 초당 8장으로 날갯짓을 표시합니다.
        int crowFrame = (int)(_crowAnimationTime * 8) % 4;
        _crow?.Draw(
            new Rect(_crowX, 10, CrowDrawSize, CrowDrawSize),
            new Rect((crowFrame % 2) * CrowFrameSize,
                (crowFrame / 2) * CrowFrameSize, CrowFrameSize, CrowFrameSize),
            opacity: CrowOpacity,
            interpolationMode: Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor);

        // 발 위치가 위쪽인 캐릭터부터 그려 앞뒤 관계를 유지합니다.
        foreach (EnemyCombat enemy in _enemies.OrderBy(enemy => enemy.Position.Y))
            if (enemy.Position.Y <= _position.Y) DrawEnemy(enemy);

        int frameCount = _isMoving ? WalkFrameCount : IdleFrameCount;
        int frame = (int)(_animationTime * (_isMoving ? 10 : 8)) % frameCount;
        G2Texture? characterTexture = _isMoving ? _gangsterWalk : _gangster;
        if (_shotTime < ShotDuration)
        {
            characterTexture = _gangsterShot;
            frame = Math.Min((int)(_shotTime / ShotDuration * 4), 3);
        }
        Matrix3x2 originalTransform = RenderTarget.Transform;
        if (_facesLeft)
        {
            RenderTarget.Transform = Matrix3x2.CreateScale(
                -1, 1, new Vector2(_position.X + IdleFrameSize / 2f, 0)) * originalTransform;
        }
        characterTexture?.Draw(
            new Rect(_position.X, _position.Y, IdleFrameSize, IdleFrameSize),
            new Rect(frame * IdleFrameSize, 0, IdleFrameSize, IdleFrameSize),
            opacity: _player.IsDead ? 0.35f : _player.Invulnerability > 0 && (int)(_player.Invulnerability * 20) % 2 == 0 ? 0.5f : 1f,
            interpolationMode: Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor,
            whiteSilhouette: _player.FlashRemaining > 0);
        RenderTarget.Transform = originalTransform;
        foreach (EnemyCombat enemy in _enemies.OrderBy(enemy => enemy.Position.Y))
            if (enemy.Position.Y > _position.Y) DrawEnemy(enemy);
        foreach (var bullet in _bullets)
        {
            // 큰 불꽃 4칸을 빠르게 바꾸고, 발사마다 시작 모양을 달리합니다.
            if (bullet.Direction < 0)
            {
                RenderTarget.Transform = Matrix3x2.CreateScale(
                    -1, 1, bullet.Position) * originalTransform;
            }
            int flameFrame = ((int)(bullet.Age * 24) + bullet.FlameOffset) % 4;
            Rect flameSource = new(480 + flameFrame * 32, 192, 32, 32);
            // 잔상은 총구를 지나온 거리만큼만 늘어나고 탄환과 함께 사라집니다.
            float trailLength = Math.Min(26, bullet.Age * BulletSpeed);
            for (int trail = 2; trail >= 1; trail--)
            {
                float offset = trailLength * trail / 2;
                _bulletEffect?.Draw(
                    new Rect(bullet.Position.X - 32 - offset, bullet.Position.Y - 8, 32, 16),
                    flameSource, opacity: trail == 2 ? 0.15f : 0.3f,
                    interpolationMode: Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor);
            }
            _bulletEffect?.Draw(
                new Rect(bullet.Position.X - 32, bullet.Position.Y - 9.6f, 32, 19.2f),
                flameSource,
                interpolationMode: Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor);
            RenderTarget.Transform = originalTransform;
        }
        if (_shotTime < MuzzleFlashDuration)
        {
            if (_muzzleDirection < 0)
                RenderTarget.Transform = Matrix3x2.CreateScale(-1, 1, _muzzlePosition) * originalTransform;
            int flashFrame = Math.Min(3, (int)(_shotTime / MuzzleFlashDuration * 4));
            float flashSize = 1 - (float)(_shotTime / MuzzleFlashDuration) * 0.35f;
            _bulletEffect?.Draw(
                new Rect(_muzzlePosition.X - 6, _muzzlePosition.Y - 16 * flashSize,
                    48 * flashSize, 32 * flashSize),
                new Rect(480 + flashFrame * 32, 192, 32, 32),
                opacity: 1 - (float)(_shotTime / MuzzleFlashDuration) * 0.6f,
                interpolationMode: Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor);
            RenderTarget.Transform = originalTransform;
        }
        foreach (var bullet in _enemyBullets)
        {
            // 적 탄환은 작고 둥근 불꽃으로 플레이어의 긴 탄환과 구별합니다.
            _bulletEffect?.Draw(new Rect(bullet.Position.X - 7, bullet.Position.Y - 7, 14, 14),
                new Rect(0, 16, 16, 16),
                interpolationMode: Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor);
        }
        _hitEffect?.Draw(RenderTarget, GrayscaleEnabled);
        // 글자와 TV 효과는 화면 흔들림에 영향을 받지 않습니다.
        RenderTarget.Transform = screenTransform;
        // 배경과 캐릭터를 모두 그린 뒤 효과를 덮습니다.
        _crtEffect?.Draw(RenderTarget, ScreenSize.Width, ScreenSize.Height, TotalTime, GrayscaleEnabled);
        foreach (EnemyCombat enemy in _enemies) DrawEnemyStatus(enemy);
        DrawHud();
    }

    private void DrawHud()
    {
        if (_hudBrush == null) return;
        _hudBrush.Color = new Color4(0.02f, 0.02f, 0.025f, 0.82f);
        RenderTarget.FillRectangle(new Rect(0, 0, 1200, 88), _hudBrush);
        Color4 white = new(0.95f, 0.95f, 0.92f, 1);
        Color4 gold = new(1, 0.8f, 0.4f, 1);
        _volumeFont?.DrawText("LIFE OF MAFIA  /  STAGE 1 DEMO",
            new Rect(18, 10, 650, 25), gold);
        _volumeFont?.DrawText($"목표: 적 전원 처치   {_stage.DefeatedCount} / {_enemies.Count}    경과 {_stage.PlaySeconds:0.0}초",
            new Rect(18, 36, 650, 25), white);
        _volumeFont?.DrawText("이동 방향키 / WASD   사격 Z / SPACE   재시작 R   일시정지 ESC",
            new Rect(18, 62, 880, 25), white);
        _volumeFont?.DrawText($"HP  {_player.Health} / 100",
            new Rect(920, 10, 260, 25), _player.Health <= 30 ? new Color4(1, 0.4f, 0.3f, 1) : white);
        _hudBrush.Color = new Color4(0.25f, 0.12f, 0.12f, 1);
        RenderTarget.FillRectangle(new Rect(920, 39, 260, 8), _hudBrush);
        _hudBrush.Color = new Color4(0.85f, 0.65f, 0.3f, 1);
        RenderTarget.FillRectangle(new Rect(920, 39, 260 * _player.Health / 100f, 8), _hudBrush);
        _volumeFont?.DrawText($"음악 {_musicVolumePercent}%  F5 - / F6 +",
            new Rect(920, 60, 270, 25), white);

        if (!_stage.Finished && !_paused && !_focusPaused) return;
        _hudBrush.Color = new Color4(0, 0, 0, 0.65f);
        RenderTarget.FillRectangle(new Rect(0, 88, 1200, 312), _hudBrush);
        bool cleared = _stage.Result == Stage1Session.Outcome.Cleared;
        string title = _stage.Finished ? cleared ? "STAGE 1 CLEAR" : "GAME OVER" : "PAUSED";
        _titleFont?.DrawText(title, new Rect(160, 150, 880, 60),
            cleared ? gold : _player.IsDead ? new Color4(1, 0.35f, 0.25f, 1) : white);
        string detail = _stage.Finished
            ? cleared ? $"모든 적을 처치했습니다.  |  기록 {_stage.PlaySeconds:0.0}초"
                : $"쓰러졌습니다.  |  처치 {_stage.DefeatedCount} / {_enemies.Count}"
            : _focusPaused ? "게임 창을 선택하면 돌아옵니다." : "ESC를 눌러 계속하기";
        _messageFont?.DrawText(detail, new Rect(160, 225, 880, 30), white);
        _messageFont?.DrawText("R  스테이지 1 다시 시작", new Rect(160, 265, 880, 30), gold);
    }

    private void DrawEnemyStatus(EnemyCombat enemy)
    {
        Vector2 position = enemy.Position;
        if (enemy.IsDead) return;
        string enemyStatus = $"HP {enemy.Health}/{EnemyCombat.MaxHealth}";
        _volumeFont?.DrawText(enemyStatus,
            new Rect(position.X + 18, position.Y + 26, 160, 28),
            new Color4(1f, 1f, 1f, 1f));
        if (enemy.HitTime < 0.6)
        {
            _volumeFont?.DrawText($"-{enemy.LastDamage}",
                new Rect(position.X + 48, position.Y - (float)enemy.HitTime * 35, 80, 28),
                new Color4(1f, 0.25f, 0.2f, 1f));
        }
    }

    private void DrawEnemy(EnemyCombat enemy)
    {
        Vector2 position = enemy.Position;
        int frame = (int)(_enemyAnimationTime * 8) % EnemyIdleFrameCount;
        G2Texture? texture = _enemyIdle;
        if (enemy.IsDead)
        {
            texture = _enemyDead;
            // 사망 5프레임을 한 번 재생한 뒤 마지막 자세를 유지합니다.
            frame = Math.Min((int)(enemy.DeathTime / EnemyCombat.DeathDuration * 5), 4);
        }
        else if (enemy.HitTime < EnemyCombat.HurtDuration)
        {
            texture = _enemyHurt;
            frame = Math.Min((int)(enemy.HitTime / EnemyCombat.HurtDuration * 4), 3);
        }
        else if (!_stage.Finished && (enemy.IsAiming || enemy.ShotTime < 0.2))
        {
            texture = _enemyAttack;
            frame = enemy.ShotTime < 0.2 ? 2 : 1;
        }
        Matrix3x2 originalTransform = RenderTarget.Transform;
        // 원본은 오른쪽을 향하므로 플레이어가 왼쪽에 있으면 뒤집습니다.
        if (enemy.FacesLeft)
        {
            RenderTarget.Transform = Matrix3x2.CreateScale(
                -1, 1, new Vector2(position.X + EnemyFrameSize / 2f, 0)) * originalTransform;
        }
        texture?.Draw(
            new Rect(position.X, position.Y, EnemyFrameSize, EnemyFrameSize),
            new Rect(frame * EnemyFrameSize, 0, EnemyFrameSize, EnemyFrameSize),
            interpolationMode: Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor,
            whiteSilhouette: enemy.FlashRemaining > 0);
        if (!_stage.Finished && !enemy.IsDead && enemy.HitTime >= EnemyCombat.HurtDuration
            && (enemy.IsAiming || enemy.ShotTime < 0.2))
        {
            // 팔을 뻗는 동작 위에 권총을 올립니다. 좌우 반전도 함께 적용됩니다.
            _enemyGun?.Draw(new Rect(position.X + 78, position.Y + 64, 32, 32),
                new Rect(0, 0, 32, 32),
                interpolationMode: Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor);
            if (enemy.ShotTime < 0.09)
                _bulletEffect?.Draw(new Rect(position.X + 101, position.Y + 64, 26, 22),
                    new Rect(480, 192, 32, 32),
                    interpolationMode: Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor);
        }
        RenderTarget.Transform = originalTransform;
    }

    public override void Dispose()
    {
        _hudBrush?.Dispose();
        _titleFont?.Dispose();
        _messageFont?.Dispose();
        _enemyAttack?.Dispose();
        _enemyAttack = null;
        _enemyGun?.Dispose();
        _enemyGun = null;
        _hitEffect?.Dispose();
        _hitEffect = null;
        _enemyHurt?.Dispose();
        _enemyHurt = null;
        _enemyDead?.Dispose();
        _enemyDead = null;
        _enemyIdle?.Dispose();
        _enemyIdle = null;
        _volumeFont?.Dispose();
        _volumeFont = null;
        foreach (G2Audio sound in _footstepSounds)
            sound.Dispose();
        _footstepSounds.Clear();
        _crow?.Dispose();
        _crow = null;
        _backgroundMusic?.Dispose();
        _backgroundMusic = null;
        _crtEffect?.Dispose();
        _crtEffect = null;
        foreach (G2Audio sound in _shotSounds)
            sound.Dispose();
        _shotSounds.Clear();
        _bulletEffect?.Dispose();
        _bulletEffect = null;
        _gangsterShot?.Dispose();
        _gangsterShot = null;
        _gangsterWalk?.Dispose();
        _gangsterWalk = null;
        _gangster?.Dispose();
        _gangster = null;
        _background?.Dispose();
        _background = null;
        base.Dispose();
    }
}
