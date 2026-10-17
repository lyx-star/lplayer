using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace lplayer.ViewModels;

/// <summary>字符串颜色 → SolidColorBrush 转换器（波形 Rest 颜色）。</summary>
public sealed class BrushConverter : IValueConverter
{
    public static readonly BrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s)!);
                brush.Freeze();
                return brush;
            }
            catch { /* ignore */ }
        }
        return new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
