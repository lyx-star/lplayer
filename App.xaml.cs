using System.Windows;

namespace lplayer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 命令行参数传入音频文件路径（文件关联双击启动）
        string? filePath = e.Args.Length > 0 && !string.IsNullOrWhiteSpace(e.Args[0])
            ? e.Args[0]
            : null;

        var main = new Views.MainWindow();
        main.Show();

        if (!string.IsNullOrEmpty(filePath))
            main.OpenFileFromExternal(filePath);
    }
}
