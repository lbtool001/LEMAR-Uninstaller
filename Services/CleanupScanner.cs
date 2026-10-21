using System.IO;
using LEMAR.Uninstaller.Models;
using Microsoft.Win32;
namespace LEMAR.Uninstaller.Services;
public sealed class LeftoverItem
{
    public string Path { get; init; } = "";
    public bool IsRegistry { get; init; }
    public string Kind => IsRegistry ? "Registry key" : "Folder";
    public bool Selected { get; set; }
}
public static class CleanupScanner
{
    // Deliberately conservative: only exact app-named per-user folders and keys.
    // Do not infer remnants from publisher, substring matches or shared locations.
    public static List<LeftoverItem> Scan(InstalledApp app)
    {
        var results = new List<LeftoverItem>();
        string name = app.Name.Trim();
        if (name.Equals("LEMAR Uninstaller", StringComparison.OrdinalIgnoreCase) || name.Length < 3 || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('\\') || name.Contains('/')) return results;
        foreach (var baseFolder in new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) })
        {
            if (string.IsNullOrWhiteSpace(baseFolder)) continue;
            string candidate = Path.Combine(baseFolder, name);
            if (Directory.Exists(candidate) && SafeUserFolder(candidate, baseFolder))
                results.Add(new LeftoverItem { Path = candidate });
        }
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\" + name);
        if (key is not null) results.Add(new LeftoverItem { IsRegistry = true, Path = @"HKEY_CURRENT_USER\Software\" + name });
        return results;
    }
    public static bool SafeUserFolder(string path, string root)
    {
        try
        {
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            string parent = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full), parent, StringComparison.OrdinalIgnoreCase)) return false;
            var attr = File.GetAttributes(full);
            return (attr & FileAttributes.ReparsePoint) == 0 && (attr & FileAttributes.Directory) != 0;
        }
        catch { return false; }
    }
    public static string BackupAndDelete(LeftoverItem item)
    {
        string backup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEMAR Uninstaller", "Backups", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(backup);
        if (item.IsRegistry)
        {
            if (!item.Path.StartsWith(@"HKEY_CURRENT_USER\Software\", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsupported registry location.");
            string name = item.Path[@"HKEY_CURRENT_USER\Software\".Length..];
            if (name.Length < 3 || name.Contains('\\') || name is "." or "..") throw new InvalidOperationException("Unsafe registry key.");
            string output = Path.Combine(backup, "registry-backup.reg");
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("reg.exe") {
                ArgumentList = { "export", item.Path, output, "/y" }, UseShellExecute = false, CreateNoWindow = true });
            if (process is null) throw new InvalidOperationException("Registry backup could not start.");
            process.WaitForExit();
            if (process.ExitCode != 0 || !File.Exists(output)) throw new InvalidOperationException("Registry backup failed. Nothing was deleted.");
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\" + name, false);
        }
        else
        {
            var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) };
            if (Path.GetFileName(item.Path).Equals("LEMAR Uninstaller", StringComparison.OrdinalIgnoreCase) || !roots.Any(r => SafeUserFolder(item.Path, r))) throw new InvalidOperationException("Unsafe or changed folder location.");
            // Move to an app-managed backup instead of permanently deleting.
            string destination = Path.Combine(backup, Path.GetFileName(item.Path));
            Directory.Move(item.Path, destination);
        }
        return backup;
    }
}
