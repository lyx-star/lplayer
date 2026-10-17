using System.IO;
using System.Text.Json;
using lplayer.Models;

namespace lplayer.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    string ConfigPath { get; }
    void Load();
    void Save();
    event EventHandler? SettingsSaved;
}

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public AppSettings Settings { get; private set; } = new();

    public string ConfigPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "lplayer",
            "settings.json");

    public event EventHandler? SettingsSaved;

    public void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (s is not null)
                {
                    // 校验
                    s.SeekSeconds = s.SeekSeconds is 2 or 5 or 10 or 15 or 20 or 30 ? s.SeekSeconds : 10;
                    s.PlaybackRate = s.PlaybackRate is 0.5 or 0.75 or 1.0 or 1.25 or 1.5 or 2.0
                        ? s.PlaybackRate : 1.0;
                    s.IsDarkTheme = false;
                    Settings = s;
                }
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(ConfigPath, json);
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // 配置写入失败不致命
        }
    }
}
