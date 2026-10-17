namespace lplayer.Helpers;

public static class TimeFormatter
{
    /// <summary>
    /// 格式化时间为 mm:ss.xx 或 hh:mm:ss.xx。
    /// 总时长（第二个参数）达到 60 分钟时使用时分秒格式。
    /// </summary>
    public static string Format(double seconds, double totalSeconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            seconds = 0;
        if (double.IsNaN(totalSeconds) || double.IsInfinity(totalSeconds) || totalSeconds < 0)
            totalSeconds = 0;

        bool useHours = totalSeconds >= 3600;

        // 厘秒：四舍五入到 0.01s，并处理进位导致的 60.00 边界
        int totalCs = (int)Math.Round(seconds * 100.0, MidpointRounding.AwayFromZero);
        if (totalCs < 0) totalCs = 0;

        int cs = totalCs % 100;
        int totalSec = totalCs / 100;
        int s = totalSec % 60;
        int m = (totalSec / 60) % 60;
        int h = totalSec / 3600;

        if (useHours)
            return $"{h:00}:{m:00}:{s:00}.{cs:00}";
        return $"{m:00}:{s:00}.{cs:00}";
    }

    public static string FormatPair(double current, double total)
        => $"{Format(current, total)} / {Format(total, total)}";
}
