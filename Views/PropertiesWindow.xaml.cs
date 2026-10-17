using System.Windows;
using lplayer.Helpers;
using lplayer.Models;

namespace lplayer.Views;

public partial class PropertiesWindow : Window
{
    public PropertiesWindow(AudioFileInfo info)
    {
        InitializeComponent();
        DataContext = new PropertiesViewModel(info);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed class PropertiesViewModel
{
    public PropertiesViewModel(AudioFileInfo info)
    {
        FileName = info.FileName;
        FilePath = info.FilePath;
        FileSizeText = info.FileSizeText;
        Format = info.Format;
        Codec = info.Codec;
        SampleRateText = info.SampleRateText;
        BitsPerSampleText = info.BitsPerSampleText;
        ChannelsText = info.ChannelsText;
        BitrateText = info.BitrateText;
        DurationText = TimeFormatter.Format(info.DurationSeconds, info.DurationSeconds);
    }

    public string FileName { get; }
    public string FilePath { get; }
    public string FileSizeText { get; }
    public string Format { get; }
    public string Codec { get; }
    public string SampleRateText { get; }
    public string BitsPerSampleText { get; }
    public string ChannelsText { get; }
    public string BitrateText { get; }
    public string DurationText { get; }
}
