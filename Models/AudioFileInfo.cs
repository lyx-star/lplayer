namespace lplayer.Models;

public sealed class AudioFileInfo
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string FolderPath { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public double DurationSeconds { get; init; }
    public string Format { get; init; } = string.Empty;
    public string Codec { get; init; } = string.Empty;
    public int SampleRate { get; init; }
    public int BitsPerSample { get; init; }
    public int Channels { get; init; }
    public int BitrateKbps { get; init; }

    public string ChannelsText => Channels switch
    {
        1 => "单声道 (Mono)",
        2 => "立体声 (Stereo)",
        <= 0 => "未知",
        _ => $"{Channels} 声道"
    };

    public string FileSizeText
    {
        get
        {
            double size = FileSizeBytes;
            if (size >= 1024 * 1024 * 1024)
                return $"{size / (1024.0 * 1024 * 1024):0.##} GB";
            if (size >= 1024 * 1024)
                return $"{size / (1024.0 * 1024):0.##} MB";
            if (size >= 1024)
                return $"{size / 1024.0:0.##} KB";
            return $"{size} B";
        }
    }

    public string SampleRateText => SampleRate > 0 ? $"{SampleRate} Hz" : "未知";
    public string BitsPerSampleText => BitsPerSample > 0 ? $"{BitsPerSample} bit" : "未知";
    public string BitrateText => BitrateKbps > 0 ? $"{BitrateKbps} kbps" : "未知";
}
