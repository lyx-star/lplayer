using NAudio.Wave;

namespace lplayer.Services;

/// <summary>
/// 轻量倍速：声明更高/更低的输出采样率，WaveOut 按该速率播放，
/// 原始采样以不同速率送入 → 速度与音高同时变化（无重依赖）。
/// </summary>
internal sealed class SpeedSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public SpeedSampleProvider(ISampleProvider source, double rate)
    {
        _source = source;
        if (rate <= 0.1) rate = 0.1;
        if (rate > 4.0) rate = 4.0;
        int sr = (int)Math.Round(source.WaveFormat.SampleRate * rate);
        if (sr < 8000) sr = 8000;
        if (sr > 192000) sr = 192000;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sr, source.WaveFormat.Channels);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
        => _source.Read(buffer, offset, count);
}
