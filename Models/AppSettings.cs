namespace lplayer.Models;

public sealed class AppSettings
{
    public bool IsDarkTheme { get; set; } = false; // 固定白色主题
    public int SeekSeconds { get; set; } = 10;
    public double PlaybackRate { get; set; } = 1.0;
}
