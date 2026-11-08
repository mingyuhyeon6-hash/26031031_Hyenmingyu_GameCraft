using System.Numerics;
using System.Windows.Forms;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

// Opening prototype: timing is in seconds; gameplay stays paused until completion.
internal sealed class OpeningCutscene : IDisposable
{
    public const double Duration = 22;
    private readonly PixelCaption _text;
    private readonly G2Font _hint = new("Malgun Gothic", 12, FontWeight.Normal);
    private readonly ID2D1SolidColorBrush _brush;
    private readonly List<G2Audio> _keys = new();
    private readonly G2Audio? _step;
    private double _time;
    private int _lineIndex = -1;
    private int _visibleLetters;
    private double _nextLetterTime;
    private int _lastStep = -1;
    private int _soundIndex;
    private readonly double? _previewTime;
    public bool Finished => _time >= Duration;

    private static readonly (double Start, string Text)[] Lines =
    {
        (6, "나는 모든 것을 가지고 있었다."),
        (9, "재산도, 명예도, 가족도."),
        (13, "그리고… 모든 것을 잃었다."),
        (17.5, "이것은 나의 이야기다.")
    };
    public OpeningCutscene(ID2D1RenderTarget target)
    {
        _text = new PixelCaption(target, Lines.Select(line => line.Text));
        // Optional fixed-time preview for inspecting composition without waiting.
        string? preview = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--opening-frame="));
        if (preview != null && double.TryParse(preview.Split('=')[1],
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double time)
            && double.IsFinite(time))
            _previewTime = _time = Math.Clamp(time, 0, Duration - 0.01);
        _brush = target.CreateSolidColorBrush(new Color4(0, 0, 0, 1));
        if (G2AudioContext.Instance?.Audio != null)
        {
            for (int i = 0; i < 4; i++)
            {
                var sound = new G2Audio("resource/music/opening/typewriter.wav");
                sound.SetVolume(0.35f);
                _keys.Add(sound);
            }
            _step = new G2Audio("resource/music/footsteps/stone_walk_left.wav");
            _step.SetVolume(0.25f);
        }
    }

    public void Restart()
    {
        StopSounds();
        _time = 0;
        _lineIndex = -1;
        _visibleLetters = 0;
        _nextLetterTime = 0;

        _lastStep = -1;
    }

    public void Update(double elapsed, bool skip)
    {
        if (skip) { _time = Duration; StopSounds(); return; }
        if (_previewTime.HasValue)
        {
            _time = _previewTime.Value;
            _lineIndex = Array.FindLastIndex(Lines, line => _time >= line.Start);
            _visibleLetters = _lineIndex < 0 ? 0 : Math.Clamp((int)((_time - Lines[_lineIndex].Start) / 0.08), 0, Lines[_lineIndex].Text.Length);
            return;
        }
        _time += Math.Min(elapsed, 0.1);
        if (_time >= 0.5 && _time < 4.5)
        {
            int step = (int)((_time - 0.5) / 0.8);
            if (step != _lastStep) { _step?.Play(); _lastStep = step; }
        }
        int lineIndex = Array.FindLastIndex(Lines, line => _time >= line.Start);
        if (lineIndex != _lineIndex)
        {
            _lineIndex = lineIndex;
            _visibleLetters = 0;
            _nextLetterTime = _time;
        }
        if (_lineIndex >= 0 && _visibleLetters < Lines[_lineIndex].Text.Length && _time >= _nextLetterTime)
        {
            // Reveal exactly one glyph and start its click in the same update.
            char letter = Lines[_lineIndex].Text[_visibleLetters++];
            PlayKey();
            _nextLetterTime = _time + (letter == '\u2026' ? 0.45 : letter == ',' ? 0.2 : 0.08);
        }
        if (Finished) StopSounds();
    }

    private void PlayKey()
    {
        if (_keys.Count > 0) _keys[_soundIndex++ % _keys.Count].Play();
    }

    public void Draw(ID2D1RenderTarget target)
    {
        Fill(target, new Rect(0, 0, 1200, 400), 1);
        if (_lineIndex >= 0)
        {
            float alpha = Math.Clamp((float)(Duration - _time) / 0.8f, 0, 1);
            for (int i = 0; i <= _lineIndex; i++)
                _text.Draw(target, Lines[i].Text,
                    i < _lineIndex ? Lines[i].Text.Length : _visibleLetters, alpha, 90 + i * 60);
        }
        _hint.DrawText("ENTER  건너뛰기     F3  처음부터", new Rect(970, 380, 225, 20), new Color4(0.55f, 0.53f, 0.48f, 1));
    }

    private void Fill(ID2D1RenderTarget target, Rect rect, float opacity)
    {
        _brush.Color = new Color4(0, 0, 0, opacity);
        target.FillRectangle(rect, _brush);
    }


    private void StopSounds() { foreach (var sound in _keys) sound.Stop(); _step?.Stop(); }
    public void Dispose()
    {
        foreach (var sound in _keys) sound.Dispose();
        _step?.Dispose();
        _brush.Dispose(); _text.Dispose(); _hint.Dispose();
    }
}
