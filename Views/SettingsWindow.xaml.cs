using System.Windows;
using System.Windows.Controls;
using lplayer.Services;
using lplayer.ViewModels;

namespace lplayer.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _main;

    public SettingsWindow(MainViewModel main)
    {
        InitializeComponent();
        _main = main;
        DataContext = new SettingsViewModel(main);
    }

    private void Seek_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string s && int.TryParse(s, out var n))
            ((SettingsViewModel)DataContext).SeekSeconds = n;
    }

    private void Associate_Click(object sender, RoutedEventArgs e)
    {
        var vm = (SettingsViewModel)DataContext;
        var results = vm.FileAssociation.AssociateAll();
        int ok = results.Count(r => r.Success);
        int fail = results.Count - ok;
        var msg = $"关联完成：成功 {ok} / 失败 {fail}\n\n" +
                  string.Join("\n", results.Select(r =>
                      $"{r.Extension}: {(r.Success ? "✓" : "✗")} {r.Message}"));

        vm.StatusMessage = fail == 0
            ? "全部关联成功。若系统仍提示选择应用，请在“默认应用”中确认 lplayer。"
            : $"部分格式关联失败，请在“默认应用”中手动确认。{string.Join("; ", results.Where(r => !r.Success).Select(r => r.Extension))}";

        MessageBox.Show(msg, "文件关联结果", MessageBoxButton.OK,
            fail == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void Dissociate_Click(object sender, RoutedEventArgs e)
    {
        var vm = (SettingsViewModel)DataContext;
        var results = vm.FileAssociation.DissociateAll();
        int ok = results.Count(r => r.Success);
        vm.StatusMessage = $"已尝试解除 {ok} 项关联";
        MessageBox.Show("已解除关联（若系统默认应用被其他程序占用，请到系统设置中更改）。",
            "lplayer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenDefaultApps_Click(object sender, RoutedEventArgs e)
        => FileAssociationService.OpenDefaultAppsSettings();

    private void About_Click(object sender, RoutedEventArgs e)
        => new AboutWindow { Owner = this }.ShowDialog();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed class SettingsViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private readonly MainViewModel _main;

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        _seekSeconds = main.Settings.SeekSeconds;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public FileAssociationService FileAssociation => new();

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
    }

    private int _seekSeconds;
    public int SeekSeconds
    {
        get => _seekSeconds;
        set
        {
            if (_seekSeconds != value)
            {
                _seekSeconds = value;
                _main.ApplySeekSeconds(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(SeekChoice2));
                OnPropertyChanged(nameof(SeekChoice5));
                OnPropertyChanged(nameof(SeekChoice10));
                OnPropertyChanged(nameof(SeekChoice15));
                OnPropertyChanged(nameof(SeekChoice20));
                OnPropertyChanged(nameof(SeekChoice30));
            }
        }
    }

    public bool SeekChoice2 { get => SeekSeconds == 2; set { if (value) SeekSeconds = 2; } }
    public bool SeekChoice5 { get => SeekSeconds == 5; set { if (value) SeekSeconds = 5; } }
    public bool SeekChoice10 { get => SeekSeconds == 10; set { if (value) SeekSeconds = 10; } }
    public bool SeekChoice15 { get => SeekSeconds == 15; set { if (value) SeekSeconds = 15; } }
    public bool SeekChoice20 { get => SeekSeconds == 20; set { if (value) SeekSeconds = 20; } }
    public bool SeekChoice30 { get => SeekSeconds == 30; set { if (value) SeekSeconds = 30; } }

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
