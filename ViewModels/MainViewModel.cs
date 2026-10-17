using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using lplayer.Helpers;
using lplayer.Models;
using lplayer.Services;
using lplayer.Views;

namespace lplayer.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    public static readonly IReadOnlyList<double> RateOptions = new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 };
    public static readonly IReadOnlyList<int> SeekOptions = new[] { 2, 5, 10, 15, 20, 30 };

    private readonly IAudioPlayerService _player;
    private readonly IWaveformService _waveform;
    private readonly ISettingsService _settings;
    private readonly FileAssociationService _assoc = new();
    private readonly DispatcherTimer _timer;
    private bool _isDraggingPosition;

    public MainViewModel(
        IAudioPlayerService player,
        IWaveformService waveform,
        ISettingsService settings)
    {
        _player = player;
        _waveform = waveform;
        _settings = settings;

        _settings.Load();
        ApplySettingsToServices();

        PlayPauseCommand = new RelayCommand(_ => _player.TogglePlayPause());
        SeekBackCommand = new RelayCommand(_ => _player.SeekBy(-Settings.SeekSeconds));
        SeekForwardCommand = new RelayCommand(_ => _player.SeekBy(Settings.SeekSeconds));
        CycleRateCommand = new RelayCommand(_ => CycleRate());
        OpenPropertiesCommand = new RelayCommand(_ => OpenProperties());
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        OpenAboutCommand = new RelayCommand(_ => OpenAbout());
        BeginSeekCommand = new RelayCommand(_ => _isDraggingPosition = true);
        EndSeekCommand = new RelayCommand(p =>
        {
            if (TryParsePos(p, out var pos))
                SeekFromUi(pos); // 先跳转，再结束拖动态，保证定时器拿到正确位置
            _isDraggingPosition = false;
            // 立刻再同步一次，避免 Seek 后缓冲导致读数短暂不准
            Position = _player.PositionSeconds;
            UpdateTimeText();
        });
        // 拖动中：只刷新界面时间，不打断音频
        DragUpdateCommand = new RelayCommand(p =>
        {
            if (TryParsePos(p, out var pos))
            {
                Position = pos;
                UpdateTimeText();
            }
        });
        SeekToPositionCommand = new RelayCommand(p =>
        {
            if (TryParsePos(p, out var pos))
                SeekFromUi(pos);
        });

        _player.PlaybackStateChanged += OnPlayerStateChanged;
        _player.PositionChanged += OnPlayerPositionChanged;
        _waveform.PeaksUpdated += OnPeaksUpdated;

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _timer.Tick += (_, _) =>
        {
            if (_isDraggingPosition) return;
            _player.PumpPosition();
        };
        _timer.Start();

        UpdateTransportUi();
        UpdateTimeText();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppSettings Settings => _settings.Settings;
    public ISettingsService SettingsService => _settings;
    public IWaveformService Waveform => _waveform;
    public IAudioPlayerService Player => _player;
    public FileAssociationService FileAssociation => _assoc;

    private string _fileName = "未打开文件";
    public string FileName
    {
        get => _fileName;
        private set => Set(ref _fileName, value);
    }

    private string _folderPath = " ";
    public string FolderPath
    {
        get => _folderPath;
        private set => Set(ref _folderPath, value);
    }

    private string _fullPathTooltip = "";
    public string FullPathTooltip
    {
        get => _fullPathTooltip;
        private set => Set(ref _fullPathTooltip, value);
    }

    private string _timeText = "00:00.00 / 00:00.00";
    public string TimeText
    {
        get => _timeText;
        private set => Set(ref _timeText, value);
    }

    private string _statusText = "拖入音频文件即可播放";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (Set(ref _isPlaying, value))
                OnPropertyChanged(nameof(PlayPauseIconText));
        }
    }

    public string PlayPauseIconText => IsPlaying ? "⏸" : "▶";

    private double _position;
    public double Position
    {
        get => _position;
        private set => Set(ref _position, value);
    }

    private double _duration;
    public double Duration
    {
        get => _duration;
        private set
        {
            if (Set(ref _duration, value))
                UpdateTimeText();
        }
    }

    private double _playbackRate = 1.0;
    public double PlaybackRate
    {
        get => _playbackRate;
        set
        {
            double v = RateOptions.Contains(value) ? value : 1.0;
            if (Set(ref _playbackRate, v))
            {
                _player.Rate = v;
                OnPropertyChanged(nameof(RateButtonText));
                SaveSettings();
            }
        }
    }

    public string RateButtonText => $"{PlaybackRate:0.##}x";

    private AudioFileInfo? _currentInfo;
    public AudioFileInfo? CurrentInfo
    {
        get => _currentInfo;
        private set => Set(ref _currentInfo, value);
    }

    public ICommand PlayPauseCommand { get; }
    public ICommand SeekBackCommand { get; }
    public ICommand SeekForwardCommand { get; }
    public ICommand CycleRateCommand { get; }
    public ICommand OpenPropertiesCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenAboutCommand { get; }
    public ICommand BeginSeekCommand { get; }
    public ICommand EndSeekCommand { get; }
    public ICommand SeekToPositionCommand { get; }
    public ICommand DragUpdateCommand { get; }

    private int _waveformVersion;
    public int WaveformVersion
    {
        get => _waveformVersion;
        private set => Set(ref _waveformVersion, value);
    }

    public void OpenFileFromExternal(string path)
    {
        Application.Current?.Dispatcher.Invoke(() => OpenFile(path));
    }

    public void OpenFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                StatusText = "文件不存在";
                MessageBox.Show("文件不存在:\n" + path, "lplayer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!AudioPlayerService.IsSupported(path))
            {
                StatusText = "不支持的格式";
                MessageBox.Show("不支持的音频格式:\n" + Path.GetExtension(path), "lplayer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _player.OpenFile(path);
            CurrentInfo = _player.CurrentFile;
            FileName = CurrentInfo?.FileName ?? Path.GetFileName(path);
            FolderPath = CurrentInfo?.FolderPath ?? Path.GetDirectoryName(path) ?? " ";
            FullPathTooltip = path;
            Duration = _player.DurationSeconds;
            Position = 0;

            _player.Play();
            StatusText = "正在播放";
            UpdateTransportUi();

            _waveform.GenerateAsync(path, 1400);
            OnPropertyChanged(nameof(Waveform));
        }
        catch (Exception ex)
        {
            StatusText = "打开失败";
            MessageBox.Show("无法打开文件:\n" + ex.Message, "lplayer",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void SeekFromUi(double position)
    {
        _player.Seek(position);
        Position = _player.PositionSeconds;
        Duration = _player.DurationSeconds;
        UpdateTimeText();
    }

    private static bool TryParsePos(object? p, out double pos)
    {
        if (p is double d) { pos = d; return true; }
        if (p is string s && double.TryParse(s, out d)) { pos = d; return true; }
        pos = 0;
        return false;
    }

    public void HandleKeyDown(Key key)
    {
        switch (key)
        {
            case Key.Space:
                _player.TogglePlayPause();
                break;
            case Key.Left:
                _player.SeekBy(-Settings.SeekSeconds);
                break;
            case Key.Right:
                _player.SeekBy(Settings.SeekSeconds);
                break;
        }
    }

    public void CycleRate()
    {
        int idx = 0;
        for (int i = 0; i < RateOptions.Count; i++)
        {
            if (Math.Abs(RateOptions[i] - PlaybackRate) < 0.001)
            {
                idx = i;
                break;
            }
        }
        PlaybackRate = RateOptions[(idx + 1) % RateOptions.Count];
    }

    public void ApplySeekSeconds(int seconds)
    {
        Settings.SeekSeconds = seconds;
        SaveSettings();
        OnPropertyChanged(nameof(Settings));
    }

    public void SaveSettings()
    {
        Settings.PlaybackRate = PlaybackRate;
        _settings.Save();
    }

    private void ApplySettingsToServices()
    {
        var s = Settings;
        s.IsDarkTheme = false; // 固定白色主题
        _playbackRate = s.PlaybackRate is 0.5 or 0.75 or 1.0 or 1.25 or 1.5 or 2.0 ? s.PlaybackRate : 1.0;
        _player.Rate = _playbackRate;
        _player.Volume = 80;
        _player.IsMuted = false;
        OnPropertyChanged(nameof(PlaybackRate));
        OnPropertyChanged(nameof(RateButtonText));
        OnPropertyChanged(nameof(Settings));
    }

    private void OpenProperties()
    {
        if (CurrentInfo is null)
        {
            StatusText = "请先打开音频文件";
            return;
        }
        var win = new PropertiesWindow(CurrentInfo) { Owner = Application.Current.MainWindow };
        win.ShowDialog();
    }

    private void OpenSettings()
    {
        var win = new SettingsWindow(this) { Owner = Application.Current.MainWindow };
        win.ShowDialog();
    }

    private void OpenAbout()
    {
        var win = new AboutWindow { Owner = Application.Current.MainWindow };
        win.ShowDialog();
    }

    private void OnPlayerStateChanged(object? sender, EventArgs e)
    {
        RunOnUi(() =>
        {
            IsPlaying = _player.IsPlaying;
            UpdateTransportUi();
        });
    }

    private void OnPlayerPositionChanged(object? sender, EventArgs e)
    {
        RunOnUi(() =>
        {
            if (!_isDraggingPosition)
            {
                Position = _player.PositionSeconds;
            }
            Duration = _player.DurationSeconds;
            UpdateTimeText();
        });
    }

    private void OnPeaksUpdated(object? sender, EventArgs e)
    {
        RunOnUi(() =>
        {
            OnPropertyChanged(nameof(Waveform));
            WaveformVersion++;
            OnPropertyChanged(nameof(WaveformVersion));
        });
    }

    private void UpdateTransportUi()
    {
        OnPropertyChanged(nameof(PlayPauseIconText));
        OnPropertyChanged(nameof(IsPlaying));
    }

    private void UpdateTimeText()
    {
        double total = Duration;
        double cur = _isDraggingPosition ? Position : _player.PositionSeconds;
        if (_isDraggingPosition)
            cur = Position;
        TimeText = TimeFormatter.FormatPair(cur, total);
    }

    private static void RunOnUi(Action action)
    {
        var app = Application.Current;
        if (app is null) { action(); return; }
        if (app.Dispatcher.CheckAccess()) action();
        else app.Dispatcher.BeginInvoke(action);
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _timer.Stop();
        _player.PlaybackStateChanged -= OnPlayerStateChanged;
        _player.PositionChanged -= OnPlayerPositionChanged;
        _waveform.PeaksUpdated -= OnPeaksUpdated;
        SaveSettings();
        _player.Dispose();
    }
}
