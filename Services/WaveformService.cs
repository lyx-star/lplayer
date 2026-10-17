using System.IO;
using NAudio.Wave;
using NAudio.Vorbis;

namespace lplayer.Services;

public interface IWaveformService
{
    float[]? Peaks { get; }
    bool IsReady { get; }
    event EventHandler? PeaksUpdated;
    void Cancel();
    void GenerateAsync(string path, int barCount = 1400);
}

public sealed class WaveformService : IWaveformService
{
    private CancellationTokenSource? _cts;
    private float[]? _peaks;
    private readonly object _sync = new();

    public event EventHandler? PeaksUpdated;

    public float[]? Peaks { get { lock (_sync) return _peaks; } }
    public bool IsReady { get { lock (_sync) return _peaks is not null; } }

    public void Cancel()
    {
        CancellationTokenSource? old;
        lock (_sync)
        {
            old = _cts;
            _cts = null;
            _peaks = null;
        }
        try { old?.Cancel(); old?.Dispose(); } catch { /* ignore */ }
    }

    public void GenerateAsync(string path, int barCount = 1400)
    {
        if (barCount < 16) barCount = 16;

        var cts = new CancellationTokenSource();
        CancellationTokenSource? old;
        lock (_sync)
        {
            old = _cts;
            _cts = cts;
            _peaks = null;
        }
        try { old?.Cancel(); old?.Dispose(); } catch { /* ignore */ }

        Task.Run(() =>
        {
            try
            {
                var peaks = Generate(path, barCount, cts.Token);
                if (peaks is null || cts.IsCancellationRequested) return;

                lock (_sync)
                {
                    if (!ReferenceEquals(_cts, cts)) return;
                    _peaks = peaks;
                }
                PeaksUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_cts, cts)) _peaks = null;
                }
            }
        }, CancellationToken.None);
    }

    private static float[]? Generate(string path, int barCount, CancellationToken token)
    {
        using WaveStream reader = CreateReader(path);

        ISampleProvider samples = reader is ISampleProvider isp
            ? isp
            : reader.ToSampleProvider();

        if (samples.WaveFormat.Channels > 1)
            samples = new DownmixToMonoSampleProvider(samples);

        double durationSec = reader.TotalTime.TotalSeconds;
        if (durationSec <= 0 || durationSec > 3600 * 6)
        {
            // 时长异常或过长：仍尝试有限读取
            if (durationSec <= 0) return null;
        }

        long sampleRate = samples.WaveFormat.SampleRate;
        long totalOut = (long)(durationSec * sampleRate);
        if (totalOut <= 0) return null;

        var peaks = new float[barCount];
        var buffer = new float[16384];
        long readTotal = 0;
        long maxRead = sampleRate * 3600L * 2; // 2 小时安全阀

        int n;
        while ((n = samples.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();

            for (int i = 0; i < n; i++)
            {
                long pos = readTotal + i;
                int bar = (int)((double)pos * barCount / totalOut);
                if (bar < 0) bar = 0;
                if (bar >= barCount) bar = barCount - 1;
                float a = Math.Abs(buffer[i]);
                if (a > peaks[bar]) peaks[bar] = a;
            }

            readTotal += n;
            if (readTotal >= maxRead) break;
        }

        float max = 0f;
        foreach (var p in peaks)
            if (p > max) max = p;
        if (max > 0.001f)
        {
            for (int i = 0; i < peaks.Length; i++)
                peaks[i] = Math.Min(1f, peaks[i] / max);
        }

        // 3 点平滑
        var smoothed = new float[barCount];
        for (int i = 0; i < barCount; i++)
        {
            float sum = 0;
            int c = 0;
            for (int j = i - 1; j <= i + 1; j++)
            {
                if (j < 0 || j >= barCount) continue;
                sum += peaks[j];
                c++;
            }
            smoothed[i] = sum / Math.Max(1, c);
        }

        return smoothed;
    }

    private static WaveStream CreateReader(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".wav" => new WaveFileReader(path),
            ".ogg" => new VorbisWaveReader(path),
            _ => new MediaFoundationReader(path)
        };
    }
}

internal sealed class DownmixToMonoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private float[] _buffer = Array.Empty<float>();

    public DownmixToMonoSampleProvider(ISampleProvider source)
    {
        _source = source;
        _channels = Math.Max(1, source.WaveFormat.Channels);
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_channels < 2)
            return _source.Read(buffer, offset, count);

        int need = count * _channels;
        if (_buffer.Length < need)
            _buffer = new float[need];

        int read = _source.Read(_buffer, 0, need);
        int frames = read / _channels;
        for (int f = 0; f < frames; f++)
        {
            float sum = 0;
            for (int c = 0; c < _channels; c++)
                sum += _buffer[f * _channels + c];
            buffer[offset + f] = sum / _channels;
        }
        return frames;
    }
}
