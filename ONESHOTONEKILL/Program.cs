
using System.Windows.Forms;
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
    private const int EnemyFrameSize = 128;
    private const int EnemyIdleFrameCount = 7;
    private readonly EnemyCombat _enemy = new();
    private Vector2 _enemyPosition => _enemy.Position;
    private double _enemyAnimationTime;
    private G2Texture? _bulletEffect;
    private CrtEffect? _crtEffect;
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
    private readonly List<(Vector2 Position, float Direction)> _bullets = new();
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
    private Vector2 _position = new(120, 222);
    private bool _isMoving;
    private bool _facesLeft;
    private double _animationTime;

    public override System.Drawing.Size ScreenSize => new(1200, 400);
    public override string GameName => "ONESHOTONEKILL - Stage 1";

    protected override void Initialize()
    {
        _volumeFont = new G2Font("Malgun Gothic", 16);
        _crtEffect = new CrtEffect(RenderTarget);
        _background = new G2Texture("resource/backgrounds/stage1.png");
        _crow = new G2Texture("resource/backgrounds/crow/crow_fly.png");
        _gangster = new G2Texture("resource/character/Gangsters_1/Idle.png");
        _gangsterWalk = new G2Texture("resource/character/Gangsters_1/Walk.png");
        _gangsterShot = new G2Texture("resource/character/Gangsters_1/Shot.png");
        _enemyIdle = new G2Texture("resource/character/Gangsters_2/Idle.png");
        _enemyHurt = new G2Texture("resource/character/Gangsters_2/Hurt.png");
        _enemyDead = new G2Texture("resource/character/Gangsters_2/Dead.png");
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
        bool shootHeld = Form.ActiveForm != null && IsHeld(Keys.Z);
        // 연사 사이에도 멈춰 있고, 키를 놓아도 마지막 사격 동작이 끝날 때까지 이동을 막습니다.
        bool movementLocked = shootHeld || _shotTime < ShotDuration;
        Vector2 direction = Vector2.Zero;
        if (Form.ActiveForm != null && !movementLocked)
        {
            if (IsHeld(Keys.Left)) direction.X--;
            if (IsHeld(Keys.Right)) direction.X++;
            if (IsHeld(Keys.Up)) direction.Y--;
            if (IsHeld(Keys.Down)) direction.Y++;
        }

        // 대각선 이동도 같은 속도로 유지하고, 긴 프레임 뒤의 순간 이동을 방지합니다.
        float elapsed = (float)Math.Min(DeltaTime, 0.05);
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

        _enemy.Update(elapsed, _position.X);

        // 명중한 총알은 바로 제거하여 한 발이 여러 번 피해를 주지 않게 합니다.
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            Vector2 previousBulletPosition = bullet.Position;
            bullet.Position.X += bullet.Direction * BulletSpeed * elapsed;
            if (_enemy.TryHit(previousBulletPosition, bullet.Position)
                || bullet.Position.X < -32 || bullet.Position.X > ScreenSize.Width + 32)
                _bullets.RemoveAt(i);
            else
                _bullets[i] = bullet;
        }

        _shotTime += elapsed;
        _shotCooldown = Math.Max(0, _shotCooldown - elapsed);
        if (shootHeld && _shotCooldown <= 0)
        {
            float bulletDirection = _facesLeft ? -1f : 1f;
            Vector2 muzzle = _position + new Vector2(_facesLeft ? 48 : 80, 90);
            _bullets.Add((muzzle, bulletDirection));
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

    protected override void Render()
    {
        // 원본 배경(2172 x 724)을 화면 전체에 표시합니다.
        _background?.Draw(
            new Rect(0, 0, ScreenSize.Width, ScreenSize.Height),
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
        if (_enemyPosition.Y <= _position.Y)
            DrawEnemy();

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
            interpolationMode: Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor);
        RenderTarget.Transform = originalTransform;
        if (_enemyPosition.Y > _position.Y)
            DrawEnemy();
        foreach (var bullet in _bullets)
        {
            // 이펙트 시트에서 오른쪽을 향한 불꽃 탄환 한 칸을 사용합니다.
            if (bullet.Direction < 0)
            {
                RenderTarget.Transform = Matrix3x2.CreateScale(
                    -1, 1, bullet.Position) * originalTransform;
            }
            _bulletEffect?.Draw(
                new Rect(bullet.Position.X - 24, bullet.Position.Y - 6, 24, 12),
                new Rect(288, 208, 32, 16),
                interpolationMode: Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor);
            RenderTarget.Transform = originalTransform;
        }
        // 배경과 캐릭터를 모두 그린 뒤 효과를 덮습니다.
        _crtEffect?.Draw(RenderTarget, ScreenSize.Width, ScreenSize.Height, TotalTime, GrayscaleEnabled);
        string volumeText = $"음악 {_musicVolumePercent}%   F5 − / F6 +";
        _volumeFont?.DrawText(volumeText, new Rect(18, 18, 330, 28), new Color4(0f, 0f, 0f, 1f));
        _volumeFont?.DrawText(volumeText, new Rect(16, 16, 330, 28), new Color4(1f, 1f, 1f, 1f));
        string enemyStatus = _enemy.IsDead ? "적 처치" : $"적 HP {_enemy.Health}/{EnemyCombat.MaxHealth}";
        _volumeFont?.DrawText(enemyStatus,
            new Rect(_enemyPosition.X + 18, _enemyPosition.Y + 26, 160, 28),
            new Color4(1f, 1f, 1f, 1f));
        if (_enemy.HitTime < 0.6)
        {
            _volumeFont?.DrawText($"-{_enemy.LastDamage}",
                new Rect(_enemyPosition.X + 48, _enemyPosition.Y - (float)_enemy.HitTime * 35, 80, 28),
                new Color4(1f, 0.25f, 0.2f, 1f));
        }
    }

    private void DrawEnemy()
    {
        int frame = (int)(_enemyAnimationTime * 8) % EnemyIdleFrameCount;
        G2Texture? texture = _enemyIdle;
        if (_enemy.IsDead)
        {
            texture = _enemyDead;
            // 사망 5프레임을 한 번 재생한 뒤 마지막 자세를 유지합니다.
            frame = Math.Min((int)(_enemy.DeathTime / EnemyCombat.DeathDuration * 5), 4);
        }
        else if (_enemy.HitTime < EnemyCombat.HurtDuration)
        {
            texture = _enemyHurt;
            frame = Math.Min((int)(_enemy.HitTime / EnemyCombat.HurtDuration * 4), 3);
        }
        Matrix3x2 originalTransform = RenderTarget.Transform;
        // 원본은 오른쪽을 향하므로 플레이어가 왼쪽에 있으면 뒤집습니다.
        if (_enemy.FacesLeft)
        {
            RenderTarget.Transform = Matrix3x2.CreateScale(
                -1, 1, new Vector2(_enemyPosition.X + EnemyFrameSize / 2f, 0)) * originalTransform;
        }
        texture?.Draw(
            new Rect(_enemyPosition.X, _enemyPosition.Y, EnemyFrameSize, EnemyFrameSize),
            new Rect(frame * EnemyFrameSize, 0, EnemyFrameSize, EnemyFrameSize),
            interpolationMode: Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor);
        RenderTarget.Transform = originalTransform;
    }

    public override void Dispose()
    {
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
