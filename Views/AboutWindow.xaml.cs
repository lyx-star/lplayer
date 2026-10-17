using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using lplayer.Services;

namespace lplayer.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = "v" + FileAssociationService.AppVersion;
        LoadAvatar();
    }

    private void LoadAvatar()
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "Assets", "avatar.png"),
                Path.Combine(baseDir, "avatar.png"),
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();

                AvatarImage.Source = bmp;
                AvatarImage.Visibility = Visibility.Visible;
                return;
            }
        }
        catch
        {
            // 资源损坏时静默回退占位图
        }
    }

    private void Github_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        catch
        {
            MessageBox.Show("无法打开浏览器", "lplayer",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
