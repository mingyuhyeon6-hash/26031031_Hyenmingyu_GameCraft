using Vortice.Multimedia;
using Vortice.XAudio2;

// Seamless synthesized engine and filtered air loops; no external sound assets.
internal sealed class DrivingAudio : IDisposable
{
    private const float EngineVolumeScale = 0.3f;
    private const float WindVolumeScale = 0.2f;
    private readonly Loop? _engine;
    private readonly Loop? _wind;

    public DrivingAudio()
    {
        if (G2AudioContext.Instance?.Audio is not { } audio) return;
        _engine = new Loop(audio, MakeWave(false));
        _wind = new Loop(audio, MakeWave(true));
    }

    public void Start() { _engine?.Start(); _wind?.Start(); Update(0, false); }
    public void Stop() { _engine?.Stop(); _wind?.Stop(); }

    public void Update(float speed, bool throttle)
    {
        float amount = Math.Clamp(Math.Abs(speed) / 840, 0, 1);
        _engine?.Mix((0.1f + amount * 0.22f + (throttle ? 0.05f : 0)) * EngineVolumeScale,
            0.75f + amount * 1.1f + (throttle ? 0.12f : 0));
        _wind?.Mix((0.01f + amount * amount * 0.38f) * WindVolumeScale, 0.85f + amount * 0.45f);
    }

    private static byte[] MakeWave(bool wind)
    {
        const int rate = 22050;
        const int count = rate * 4;
        const int overlap = 2205;
        var samples = new float[count + overlap];
        var random = new Random(wind ? 81 : 39);
        double low = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i / rate;
            double noise = random.NextDouble() * 2 - 1;
            low += 0.09 * (noise - low);
            double value = wind
                ? (low * 2.8 + noise * 0.06) * (0.8 + 0.15 * Math.Sin(t * Math.PI * 1.5))
                : 0.36 * Math.Sin(2 * Math.PI * 55 * t)
                    + 0.22 * Math.Sin(2 * Math.PI * 110 * t)
                    + 0.12 * Math.Sin(2 * Math.PI * 165 * t)
                    + 0.06 * Math.Sin(2 * Math.PI * 330 * t) + low * 0.2;
            samples[i] = (float)Math.Clamp(value, -0.95, 0.95);
        }
        // Blend the final overlap into the beginning to remove wrap clicks.
        for (int i = 0; i < overlap; i++)
        {
            float blend = (float)i / overlap;
            samples[i] = samples[count + i] * (1 - blend) + samples[i] * blend;
        }
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
        writer.Write((short)2); writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
        for (int i = 0; i < count; i++) writer.Write((short)(samples[i] * 26000));
        return stream.ToArray();
    }

    private sealed class Loop : IDisposable
    {
        private readonly AudioBuffer _buffer;
        private readonly IXAudio2SourceVoice _voice;
        public Loop(IXAudio2 audio, byte[] wav)
        {
            using var stream = new SoundStream(new MemoryStream(wav));
            using var data = stream.ToDataStream();
            _buffer = new AudioBuffer(data) { LoopCount = (uint)XAudio2.LoopInfinite };
            _voice = audio.CreateSourceVoice(stream.Format ?? throw new InvalidDataException("Invalid generated audio."), maxFrequencyRatio: 2f);
        }
        public void Start() { Stop(); _voice.SubmitSourceBuffer(_buffer); _voice.Start(); }
        public void Stop() { _voice.Stop(); _voice.FlushSourceBuffers(); }
        public void Mix(float volume, float pitch) { _voice.SetVolume(volume); _voice.SetFrequencyRatio(pitch, 0); }
        public void Dispose() { _voice.Dispose(); _buffer.Dispose(); }
    }

    public void Dispose() { _engine?.Dispose(); _wind?.Dispose(); }
}
