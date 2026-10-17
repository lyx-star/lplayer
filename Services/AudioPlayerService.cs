using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Vorbis;
using lplayer.Models;

namespace lplayer.Services;

public interface IAudioPlayerService : IDisposable
{
    event EventHandler? PlaybackStateChanged;
    event EventHandler? PositionChanged;
    bool IsPlaying { get; }
    bool IsLoaded { get; }
    double PositionSeconds { get; }
    double DurationSeconds { get; }
    double Rate { get; set; }
    int Volume { get; set; }
    bool IsMuted { get; set; }
    AudioFileInfo? CurrentFile { get; }

    void OpenFile(string path);
    void Play();
    void Pause();
    void TogglePlayPause();
    void Stop();
    void Seek(double seconds);
    void SeekBy(double deltaSeconds);
    void PumpPosition();
}

public sealed class AudioPlayerService : IAudioPlayerService
{
    private static readonly HashSet<string> SupportedExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma"
    };

    public static bool IsSupported(string path)
        => SupportedExt.Contains(Path.GetExtension(path));

    public static IReadOnlyCollection<string> SupportedExtensions => SupportedExt;

    private readonly object _sync = new();
    private IWavePlayer? _output;
    private ISampleProvider? _outputLastInit;
    private WaveStream? _reader;
    private ISampleProvider? _chain;
    private VolumeSampleProvider? _volumeProvider;
    private bool _isPlaying;
    private bool _isMuted;
    private int _volume = 80;
    private double _rate = 1.0;
    private AudioFileInfo? _currentFile;

    public event EventHandler? PlaybackStateChanged;
    public event EventHandler? PositionChanged;

    public AudioPlayerService() { }

    public bool IsPlaying { get { lock (_sync) return _isPlaying; } }
    public bool IsLoaded { get { lock (_sync) return _reader is not null; } }
    public AudioFileInfo? CurrentFile { get { lock (_sync) return _currentFile; } }

    public double DurationSeconds
    {
        get
        {
            lock (_sync)
            {
                if (_reader is null) return 0;
                try { return Math.Max(0, _reader.TotalTime.TotalSeconds); }
                catch { return 0; }
            }
        }
    }

    public double PositionSeconds
    {
        get
        {
            lock (_sync) return GetPositionNoLock();
            }
    }

    public double Rate
    {
        get { lock (_sync) return _rate; }
        set
        {
            bool rebuild;
            lock (_sync)
            {
                rebuild = Math.Abs(_rate - value) > 0.001;
                _rate = Math.Clamp(value, 0.5, 2.0);
                if (rebuild && _reader is not null)
                    RebuildChainNoLock();
            }
            // 若正在播放且倍速变化，重新初始化输出
            if (rebuild && IsPlaying)
            {
                double pos;
                lock (_sync) pos = GetPositionNoLock();
                lock (_sync)
                {
                    try { _output?.Stop(); } catch { /* ignore */ }
                    _isPlaying = false;
                    if (_reader is not null)
                    {
                        try { _reader.CurrentTime = TimeSpan.FromSeconds(pos); } catch { /* ignore */ }
                    }
                }
                Play();
            }
        }
    }

    public int Volume
    {
        get { lock (_sync) return _volume; }
        set
        {
            lock (_sync)
            {
                _volume = Math.Clamp(value, 0, 100);
                ApplyVolumeNoLock();
            }
        }
    }

    public bool IsMuted
    {
        get { lock (_sync) return _isMuted; }
        set
        {
            lock (_sync)
            {
                _isMuted = value;
                ApplyVolumeNoLock();
            }
        }
    }

    public void OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("找不到音频文件", path);
        if (!IsSupported(path))
            throw new NotSupportedException("不支持的音频格式: " + Path.GetExtension(path));

        var reader = CreateReader(path);

        lock (_sync)
        {
            TearDownNoLock();

            _reader = reader;
            _volumeProvider = new VolumeSampleProvider(reader.ToSampleProvider())
            {
                Volume = _isMuted ? 0f : _volume / 100f
            };
            RebuildChainNoLock();
            _currentFile = BuildFileInfo(path, reader);
        }

        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Play()
    {
        lock (_sync)
        {
            if (_reader is null || _chain is null || _isPlaying) return;

            // 结束后再次播放回到开头
            if (_reader.Length > 0 && _reader.Position >= _reader.Length)
                _reader.Position = 0;

            EnsureOutputNoLock();

            // 链路变化（如倍速）时重新 Init；暂停恢复则直接 Play
            if (!ReferenceEquals(_outputLastInit, _chain))
            {
                try { _output!.Stop(); } catch { /* ignore */ }
                _output!.Init(ToWaveProvider(_chain));
                _outputLastInit = _chain;
            }

            _output!.Play();
            _isPlaying = true;
        }
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        lock (_sync)
        {
            if (!_isPlaying || _output is null) return;
            _output.Pause();
            _isPlaying = false;
        }
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void TogglePlayPause()
    {
        if (IsPlaying) Pause();
        else Play();
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_output is not null && _isPlaying)
            {
                try { _output.Stop(); } catch { /* ignore */ }
            }
            _isPlaying = false;
            if (_reader is not null)
            {
                try { _reader.Position = 0; } catch { /* ignore */ }
            }
        }
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(double seconds)
    {
        bool resume;
        lock (_sync)
        {
            if (_reader is null) return;
            double dur = DurationSecondsNoLock();
            double pos = Math.Clamp(seconds, 0, dur);
            resume = _isPlaying;

            // 播放中跳转：必须重建输出，否则 WaveOut 缓冲导致进度卡住
            if (_output is not null)
            {
                try { _output.Stop(); } catch { /* ignore */ }
                try { _output.Dispose(); } catch { /* ignore */ }
                _output = null;
            }
            _outputLastInit = null;
            _isPlaying = false;

            try { _reader.CurrentTime = TimeSpan.FromSeconds(pos); }
            catch { /* ignore */ }
        }

        if (resume)
            Play();
        else
            PositionChanged?.Invoke(this, EventArgs.Empty);

        // 立刻同步一次位置，避免 UI 等到下一帧
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SeekBy(double deltaSeconds) => Seek(PositionSeconds + deltaSeconds);

    public void PumpPosition()
    {
        bool ended = false;
        lock (_sync)
        {
            if (_isPlaying && _reader is not null && _reader.Length > 0 &&
                _reader.Position >= _reader.Length - 1)
            {
                _isPlaying = false;
                try { _output?.Stop(); } catch { /* ignore */ }
                try { _reader.Position = _reader.Length; } catch { /* ignore */ }
                ended = true;
            }
        }

        if (ended)
        {
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            PositionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (IsPlaying || IsLoaded)
            PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            TearDownNoLock();
            _output?.Dispose();
            _output = null;
        }
    }

    // ---- internal ----

    private void TearDownNoLock()
    {
        if (_output is not null)
        {
            try { _output.Stop(); } catch { /* ignore */ }
            try { _output.Dispose(); } catch { /* ignore */ }
            _output = null;
        }
        _outputLastInit = null;
        _isPlaying = false;

        if (_reader is not null)
        {
            try { _reader.Dispose(); } catch { /* ignore */ }
            _reader = null;
        }
        _volumeProvider = null;
        _chain = null;
    }

    private void EnsureOutputNoLock()
    {
        if (_output is not null) return;
        _output = new WaveOutEvent
        {
            DesiredLatency = 100,
            NumberOfBuffers = 3
        };
        _outputLastInit = null;
    }

    private void ApplyVolumeNoLock()
    {
        if (_volumeProvider is not null)
            _volumeProvider.Volume = _isMuted ? 0f : _volume / 100f;
    }

    private void RebuildChainNoLock()
    {
        if (_volumeProvider is null) { _chain = null; return; }
        if (Math.Abs(_rate - 1.0) < 0.001)
        {
            _chain = _volumeProvider;
            return;
        }
        _chain = new SpeedSampleProvider(_volumeProvider, _rate);
    }

    private static IWaveProvider ToWaveProvider(ISampleProvider sp)
        => new SampleToWaveProvider(sp);

    private double GetPositionNoLock()
    {
        if (_reader is null) return 0;
        try
        {
            double pos = _reader.CurrentTime.TotalSeconds;
            double dur = DurationSecondsNoLock();
            return Math.Clamp(pos, 0, dur);
        }
        catch { return 0; }
    }

    private double DurationSecondsNoLock()
    {
        if (_reader is null) return 0;
        try { return Math.Max(0, _reader.TotalTime.TotalSeconds); }
        catch { return 0; }
    }

    private static WaveStream CreateReader(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        switch (ext)
        {
            case ".wav":
                return new WaveFileReader(path);
            case ".ogg":
                return new VorbisWaveReader(path);
            default:
                // MP3 / M4A / AAC / WMA / FLAC — Windows Media Foundation（Win10+ 支持 FLAC）
                return new MediaFoundationReader(path);
        }
    }

    private static AudioFileInfo BuildFileInfo(string path, WaveStream reader)
    {
        var fi = new FileInfo(path);
        var format = reader.WaveFormat;

        int bitrate = 0;
        try
        {
            double dur = reader.TotalTime.TotalSeconds;
            if (dur > 0.5)
                bitrate = (int)Math.Round(fi.Length * 8.0 / dur / 1000.0);
        }
        catch { /* ignore */ }

        string ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        string codec = format.Encoding switch
        {
            WaveFormatEncoding.Pcm => "PCM",
            WaveFormatEncoding.IeeeFloat => "IEEE Float",
            WaveFormatEncoding.MpegLayer3 => "MP3",
            WaveFormatEncoding.Adpcm => "ADPCM",
            WaveFormatEncoding.Dts => "DTS",
            WaveFormatEncoding.Gsm610 => "GSM 6.10",
            WaveFormatEncoding.WindowsMediaAudio => "WMA",
            (WaveFormatEncoding)0x1600 => "AAC",
            (WaveFormatEncoding)0x1601 => "AAC (ADTS)",
            _ => string.IsNullOrEmpty(ext) ? format.Encoding.ToString() : ext
        };

        return new AudioFileInfo
        {
            FilePath = path,
            FileName = Path.GetFileName(path),
            FolderPath = Path.GetDirectoryName(path) ?? string.Empty,
            FileSizeBytes = fi.Length,
            DurationSeconds = reader.TotalTime.TotalSeconds,
            Format = ext,
            Codec = codec,
            SampleRate = format.SampleRate,
            BitsPerSample = format.BitsPerSample,
            Channels = format.Channels,
            BitrateKbps = bitrate
        };
    }
}
