using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace lplayer.Services;

public sealed record AssociationResult(string Extension, bool Success, string Message);

public interface IFileAssociationService
{
    IReadOnlyList<AssociationResult> AssociateAll();
    IReadOnlyList<AssociationResult> DissociateAll();
    bool IsAssociated(string extension);
}

public sealed class FileAssociationService : IFileAssociationService
{
    public const string ProgId = "lplayer.Audio";

    private static readonly string[] Extensions =
    {
        ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma"
    };

    private static string ExePath =>
        Environment.ProcessPath
        ?? Path.Combine(AppContext.BaseDirectory, "lplayer.exe");

    private static string BaseDir =>
        Path.GetDirectoryName(ExePath) ?? AppContext.BaseDirectory;

    /// <summary>图标资源：优先 lplayer.ico（已随程序输出），否则用 exe 内嵌图标。</summary>
    private static string IconLocation
    {
        get
        {
            var ico = Path.Combine(BaseDir, "lplayer.ico");
            if (File.Exists(ico))
                return $"\"{ico}\",0";
            return $"\"{ExePath}\",0";
        }
    }

    public IReadOnlyList<AssociationResult> AssociateAll()
    {
        var results = new List<AssociationResult>();

        try
        {
            RegisterProgId();
            RegisterOpenCommand();
        }
        catch (Exception ex)
        {
            results.Add(new AssociationResult("(ProgID)", false, "注册 ProgID 失败: " + ex.Message));
            return results;
        }

        foreach (var ext in Extensions)
        {
            try
            {
                AssociateExtension(ext);
                results.Add(new AssociationResult(ext, true,
                    "已关联（若系统仍提示选择应用，请在“默认应用”中确认；图标可能需稍后刷新）"));
            }
            catch (Exception ex)
            {
                results.Add(new AssociationResult(ext, false, ex.Message));
            }
        }

        // 通知 Explorer 刷新图标与关联
        RefreshShellIcons();

        return results;
    }

    public IReadOnlyList<AssociationResult> DissociateAll()
    {
        var results = new List<AssociationResult>();

        foreach (var ext in Extensions)
        {
            try
            {
                DissociateExtension(ext);
                results.Add(new AssociationResult(ext, true, "已解除"));
            }
            catch (Exception ex)
            {
                results.Add(new AssociationResult(ext, false, ex.Message));
            }
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false);
        }
        catch { /* ignore */ }

        RefreshShellIcons();
        return results;
    }

    public bool IsAssociated(string extension)
    {
        try
        {
            using var userChoice = Registry.CurrentUser.OpenSubKey(
                $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\UserChoice");
            var progId = userChoice?.GetValue("ProgId") as string;
            if (!string.IsNullOrEmpty(progId) &&
                progId.Equals(ProgId, StringComparison.OrdinalIgnoreCase))
                return true;

            using var openWith = Registry.CurrentUser.OpenSubKey(
                $@"Software\Classes\{extension}\shell\open\command");
            var cmd = openWith?.GetValue("") as string;
            return cmd is not null &&
                   cmd.Contains("lplayer", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void RegisterProgId()
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}");
        key.SetValue("", "lplayer 音频文件");

        // 文件类型图标 → lplayer.ico（Explorer 会用此图显示关联文件）
        using var icon = key.CreateSubKey("DefaultIcon");
        icon.SetValue("", IconLocation);

        using var shell = key.CreateSubKey("shell");
        using var open = shell.CreateSubKey("open");
        using var cmd = open.CreateSubKey("command");
        cmd.SetValue("", $"\"{ExePath}\" \"%1\"");
    }

    private static void RegisterOpenCommand()
    {
        // UserChoice 受系统保护；注册 ProgID + DefaultIcon 后由用户确认默认应用。
    }

    private static void AssociateExtension(string ext)
    {
        if (!ext.StartsWith('.'))
            ext = "." + ext;

        // 扩展名 → ProgID
        using (var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}"))
        {
            extKey.SetValue("", ProgId);
        }

        // 扩展名图标（部分系统直接读这里）
        using (var iconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}\DefaultIcon"))
        {
            iconKey.SetValue("", IconLocation);
        }

        // OpenWithProgids
        using (var openWith = Registry.CurrentUser.CreateSubKey(
                   $@"Software\Classes\{ext}\OpenWithProgids"))
        {
            openWith.SetValue(ProgId, Array.Empty<byte>());
        }
    }

    private static void DissociateExtension(string ext)
    {
        if (!ext.StartsWith('.'))
            ext = "." + ext;

        try
        {
            var classesPath = $@"Software\Classes\{ext}";
            using var extKey = Registry.CurrentUser.OpenSubKey(classesPath, writable: true);
            var val = extKey?.GetValue("") as string;
            if (val != null && val.Equals(ProgId, StringComparison.OrdinalIgnoreCase))
            {
                Registry.CurrentUser.DeleteSubKeyTree(classesPath, false);
            }
        }
        catch { /* ignore */ }

        try
        {
            Registry.CurrentUser.DeleteSubKey(
                $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\UserChoice",
                false);
        }
        catch { /* ignore */ }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    /// <summary>通知系统关联与图标已变化，促使资源管理器刷新。</summary>
    public static void RefreshShellIcons()
    {
        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch { /* ignore */ }
    }

    public static void OpenDefaultAppsSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("control")
                {
                    Arguments = "/name Microsoft.DefaultPrograms",
                    UseShellExecute = true
                });
            }
            catch { /* ignore */ }
        }
    }

    public static string AppVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}
