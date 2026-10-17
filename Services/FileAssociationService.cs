using System.Diagnostics;
using System.IO;
using System.Reflection;
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

    public IReadOnlyList<AssociationResult> AssociateAll()
    {
        var results = new List<AssociationResult>();

        // 尝试写入 HKCU（当前用户，通常无需管理员）
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
                results.Add(new AssociationResult(ext, true, "已关联（若系统仍提示选择默认应用，请在“默认应用”中确认 lplayer）"));
            }
            catch (Exception ex)
            {
                results.Add(new AssociationResult(ext, false, ex.Message));
            }
        }

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
            // 删除 ProgID
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false);
        }
        catch { /* ignore */ }

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
        using var icon = key.CreateSubKey("DefaultIcon");
        icon.SetValue("", $"\"{ExePath}\",0");
        using var shell = key.CreateSubKey("shell");
        using var open = shell.CreateSubKey("open");
        using var cmd = open.CreateSubKey("command");
        cmd.SetValue("", $"\"{ExePath}\" \"%1\"");
    }

    private static void RegisterOpenCommand()
    {
        // 备用路径：Software\Classes 下扩展名直写（部分场景有效）
        // UserChoice 受系统保护，无法直接覆盖；注册 ProgID 后引导用户确认。
    }

    private static void AssociateExtension(string ext)
    {
        if (!ext.StartsWith('.'))
            ext = "." + ext;

        // 1) 扩展名 → ProgID
        using (var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}"))
        {
            extKey.SetValue("", ProgId);
        }

        // 2) 扩展名 OpenWithProgids
        using (var openWith = Registry.CurrentUser.CreateSubKey(
                   $@"Software\Classes\{ext}\OpenWithProgids"))
        {
            openWith.SetValue(ProgId, Array.Empty<byte>());
        }

        // 3) 尝试清理旧的 open 命令指向，让系统重新评估默认应用
        // UserChoice 由 Windows 保护（Windows 8+ 使用 Hash），不能直接写。
        // 通过注册 ProgID + 打开“打开方式”引导。
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
                Registry.CurrentUser.DeleteSubKey(classesPath, false);
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

    /// <summary>打开系统“默认应用 / 打开方式”设置页，引导用户确认关联。</summary>
    public static void OpenDefaultAppsSettings()
    {
        try
        {
            // ms-settings:defaultapps — Windows 10/11 默认应用
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
