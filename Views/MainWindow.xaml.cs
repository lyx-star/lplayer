using System.Windows;
using System.Windows.Input;
using lplayer.ViewModels;

namespace lplayer.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();

        var player = new Services.AudioPlayerService();
        var waveform = new Services.WaveformService();
        var settings = new Services.SettingsService();

        _vm = new MainViewModel(player, waveform, settings);
        DataContext = _vm;

        Closed += (_, _) => _vm.Dispose();

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.WaveformVersion) ||
                e.PropertyName == nameof(MainViewModel.Waveform) ||
                e.PropertyName == nameof(MainViewModel.Duration))
            {
                Waveform.Peaks = _vm.Waveform.Peaks;
                Waveform.Duration = _vm.Duration;
                Waveform.InvalidateVisual();
            }
        };
    }

    public void OpenFileFromExternal(string path) => _vm.OpenFileFromExternal(path);

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            var focused = Keyboard.FocusedElement as FrameworkElement;
            if (focused is System.Windows.Controls.Button)
                focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
        _vm.HandleKeyDown(e.Key);
    }

    private void Waveform_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_vm.Duration > 0)
            Waveform.Cursor = Cursors.Hand;
    }

    private void Waveform_MouseLeave(object sender, MouseEventArgs e)
    {
        Waveform.Cursor = Cursors.Arrow;
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Link;
            e.Handled = true;
        }
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var path = files[0];
            if (Services.AudioPlayerService.IsSupported(path))
                _vm.OpenFile(path);
            else
                MessageBox.Show("不支持的文件类型:\n" + path, "lplayer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
