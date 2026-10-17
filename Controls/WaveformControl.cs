using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace lplayer.Controls;

/// <summary>
/// 波形进度条。Position 仅由外部绑定更新（OneWay），控件内部不写该依赖属性，
/// 避免本地赋值打断绑定导致进度条停住。
/// </summary>
public sealed class WaveformControl : FrameworkElement
{
    public static readonly DependencyProperty PeaksProperty =
        DependencyProperty.Register(nameof(Peaks), typeof(float[]), typeof(WaveformControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PositionProperty =
        DependencyProperty.Register(nameof(Position), typeof(double), typeof(WaveformControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(nameof(Duration), typeof(double), typeof(WaveformControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlayedBrushProperty =
        DependencyProperty.Register(nameof(PlayedBrush), typeof(Brush), typeof(WaveformControl),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0x4C, 0x8D, 0xFF)),
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RestBrushProperty =
        DependencyProperty.Register(nameof(RestBrush), typeof(Brush), typeof(WaveformControl),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xD8)),
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SeekCommandProperty =
        DependencyProperty.Register(nameof(SeekCommand), typeof(ICommand), typeof(WaveformControl));

    public static readonly DependencyProperty BeginDragCommandProperty =
        DependencyProperty.Register(nameof(BeginDragCommand), typeof(ICommand), typeof(WaveformControl));

    public static readonly DependencyProperty EndDragCommandProperty =
        DependencyProperty.Register(nameof(EndDragCommand), typeof(ICommand), typeof(WaveformControl));

    public static readonly DependencyProperty DragUpdateCommandProperty =
        DependencyProperty.Register(nameof(DragUpdateCommand), typeof(ICommand), typeof(WaveformControl));

    private bool _dragging;
    private double _dragPreviewPos;

    public WaveformControl()
    {
        Focusable = true;
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
    }

    public float[]? Peaks
    {
        get => (float[]?)GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    /// <summary>只读绑定进度；控件内部绝不 SetValue，否则会打断绑定。</summary>
    public double Position
    {
        get => (double)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public double Duration
    {
        get => (double)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public Brush PlayedBrush
    {
        get => (Brush)GetValue(PlayedBrushProperty);
        set => SetValue(PlayedBrushProperty, value);
    }

    public Brush RestBrush
    {
        get => (Brush)GetValue(RestBrushProperty);
        set => SetValue(RestBrushProperty, value);
    }

    public ICommand? SeekCommand
    {
        get => (ICommand?)GetValue(SeekCommandProperty);
        set => SetValue(SeekCommandProperty, value);
    }

    public ICommand? BeginDragCommand
    {
        get => (ICommand?)GetValue(BeginDragCommandProperty);
        set => SetValue(BeginDragCommandProperty, value);
    }

    public ICommand? EndDragCommand
    {
        get => (ICommand?)GetValue(EndDragCommandProperty);
        set => SetValue(EndDragCommandProperty, value);
    }

    public ICommand? DragUpdateCommand
    {
        get => (ICommand?)GetValue(DragUpdateCommandProperty);
        set => SetValue(DragUpdateCommandProperty, value);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == PeaksProperty || e.Property == PositionProperty ||
            e.Property == DurationProperty || e.Property == PlayedBrushProperty ||
            e.Property == RestBrushProperty)
        {
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w < 2 || h < 2) return;

        var peaks = Peaks;
        double duration = Duration;
        // 拖动中用预览位置，否则用绑定的播放位置
        double position = _dragging ? _dragPreviewPos : Position;
        double progress = duration > 0 ? Math.Clamp(position / duration, 0, 1) : 0;
        double playedX = progress * w;

        var midPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), 1);
        dc.DrawLine(midPen, new Point(0, h / 2), new Point(w, h / 2));

        if (peaks is null || peaks.Length == 0)
        {
            var emptyBrush = RestBrush;
            double barW = 3, gap = 2;
            double x = 0;
            while (x < w)
            {
                var r = new Rect(x, h / 2 - 1, Math.Min(barW, w - x), 2);
                dc.DrawRectangle(emptyBrush, null, r);
                x += barW + gap;
            }
            return;
        }

        int barCount = Math.Max(1, (int)(w / 3));
        double barW2 = w / barCount;
        double half = h / 2 - 2;

        Span<float> samples = barCount <= 4096
            ? stackalloc float[Math.Max(barCount, 1)]
            : new float[barCount];

        for (int i = 0; i < barCount; i++)
        {
            double t0 = (double)i / barCount;
            double t1 = (double)(i + 1) / barCount;
            int i0 = (int)(t0 * peaks.Length);
            int i1 = (int)(t1 * peaks.Length);
            if (i1 <= i0) i1 = i0 + 1;
            if (i1 > peaks.Length) i1 = peaks.Length;
            float max = 0;
            for (int j = i0; j < i1; j++)
                if (peaks[j] > max) max = peaks[j];
            samples[i] = max;
        }

        var played = PlayedBrush;
        var rest = RestBrush;

        for (int i = 0; i < barCount; i++)
        {
            double x = i * barW2;
            double center = x + barW2 * 0.5;
            float amp = samples[i];
            double barH = Math.Max(2, amp * half * 2);
            double y = h / 2 - barH / 2;
            double bw = Math.Max(1.5, barW2 * 0.65);

            bool isPlayed = center <= playedX;
            var brush = isPlayed ? played : rest;
            var rect = new Rect(x + (barW2 - bw) / 2, y, bw, barH);
            dc.DrawRoundedRectangle(brush, null, rect, bw / 2, bw / 2);
        }

        if (duration > 0 && position >= 0)
        {
            double headX = Math.Clamp(playedX, 0, w);
            var headPen = new Pen(played, 1.5);
            headPen.Freeze();
            dc.DrawLine(headPen, new Point(headX, 0), new Point(headX, h));
            dc.DrawEllipse(played, headPen, new Point(headX, h / 2), 3.5, 3.5);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Duration <= 0) return;

        CaptureMouse();
        _dragging = true;
        _dragPreviewPos = XToPosition(e.GetPosition(this).X);

        BeginDragCommand?.Execute(null);
        DragUpdateCommand?.Execute(_dragPreviewPos);
        SeekCommand?.Execute(_dragPreviewPos);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || Duration <= 0) return;
        _dragPreviewPos = XToPosition(e.GetPosition(this).X);
        DragUpdateCommand?.Execute(_dragPreviewPos);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        double pos = XToPosition(e.GetPosition(this).X);
        _dragPreviewPos = pos;
        // 结束拖动：内部会 Seek 并恢复定时器更新
        EndDragCommand?.Execute(pos);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Duration <= 0) return;
        double delta = e.Delta > 0 ? 5 : -5;
        double pos = Math.Clamp(Position + delta, 0, Duration);
        SeekCommand?.Execute(pos);
        e.Handled = true;
    }

    private double XToPosition(double x)
    {
        double w = ActualWidth;
        if (w <= 0 || Duration <= 0) return 0;
        double ratio = Math.Clamp(x / w, 0, 1);
        return ratio * Duration;
    }
}
