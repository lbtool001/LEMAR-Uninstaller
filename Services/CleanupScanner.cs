using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

public sealed class CleanupScanResult
{
    public List<LeftoverItem> Items { get; } = new();
    public List<string> Warnings { get; } = new();
    public int LocationsChecked { get; set; }
}

public static class CleanupScanner
{
    private const string RegistryPrefix = @"HKEY_CURRENT_USER\Software\";

    public static List<LeftoverItem> Scan(InstalledApp app) => ScanDetailed(app).Items;

    public static CleanupScanResult ScanDetailed(InstalledApp app)
    {
        var result = new CleanupScanResult();
        var names = GetCandidateNames(app.Name).ToArray();
        if (names.Length == 0) return result;

        // Exact child folders only. Never search by substring or delete installation folders.
        var roots = UserRoots();
        foreach (string root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            foreach (string name in names)
            {
                string path = Path.Combine(root, name);
                result.LocationsChecked++;
                try
                {
                    if (Directory.Exists(path) && SafeUserFolder(path, root))
                        result.Items.Add(new LeftoverItem { Path = path });
                }
                catch (Exception ex) { result.Warnings.Add($"{path}: {ex.Message}"); }
            }
        }
        foreach (string name in names)
        {
            string path = RegistryPrefix + name;
            result.LocationsChecked++;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\" + name);
                if (key is not null) result.Items.Add(new LeftoverItem { IsRegistry = true, Path = path });
            }
            catch (Exception ex) { result.Warnings.Add($"{path}: {ex.Message}"); }
        }
        return result;
    }

    private static IEnumerable<string> GetCandidateNames(string appName)
    {
        string name = appName.Trim();
        if (!ValidName(name)) yield break;
        yield return name;
        // Only exact aliases derived from the display name, not publisher or partial matches.
        string compact = string.Concat(name.Where(char.IsLetterOrDigit));
        if (compact.Length >= 4 && !compact.Equals(name, StringComparison.OrdinalIgnoreCase) && ValidName(compact))
            yield return compact;
        var words = name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 3)
        {
            string acronym = string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
            if (acronym.Length >= 3 && ValidName(acronym) &&
                !acronym.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                !acronym.Equals(compact, StringComparison.OrdinalIgnoreCase)) yield return acronym;
        }
    }

    private static bool ValidName(string name) => name.Length >= 3 && name.Length <= 100 &&
        name is not "." and not ".." &&
        !name.Equals("LEMAR Uninstaller", StringComparison.OrdinalIgnoreCase) &&
        !name.Equals("Windows", StringComparison.OrdinalIgnoreCase) &&
        !name.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains('\\') && !name.Contains('/') &&
        name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static string[] UserRoots() => new[] {
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
    }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static bool SafeUserFolder(string path, string root)
    {
        try
        {
            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            if (!string.Equals(Path.GetDirectoryName(full), parent, StringComparison.OrdinalIgnoreCase)) return false;
            if (Path.GetFileName(full).Equals("LEMAR Uninstaller", StringComparison.OrdinalIgnoreCase)) return false;
            var attr = File.GetAttributes(full);
            return (attr & FileAttributes.ReparsePoint) == 0 && (attr & FileAttributes.Directory) != 0;
        }
        catch { return false; }
    }

    public static string BackupAndDelete(LeftoverItem item)
    {
        string backupRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LEMAR Uninstaller", "Backups");
        string backup = Path.Combine(backupRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8]);

        if (item.IsRegistry)
        {
            if (!item.Path.StartsWith(RegistryPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unsupported registry location.");
            string name = item.Path[RegistryPrefix.Length..];
            if (!ValidName(name)) throw new InvalidOperationException("Unsafe registry key.");
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\" + name))
                if (key is null) throw new InvalidOperationException("Registry key no longer exists.");
            Directory.CreateDirectory(backup);
            string output = Path.Combine(backup, "registry-backup.reg");
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("reg.exe") {
                ArgumentList = { "export", item.Path, output, "/y" },
                UseShellExecute = false, CreateNoWindow = true
            });
            if (process is null) throw new InvalidOperationException("Registry backup could not start.");
            process.WaitForExit();
            if (process.ExitCode != 0 || !File.Exists(output))
                throw new InvalidOperationException("Registry backup failed. Nothing was deleted.");
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\" + name, false);
        }
        else
        {
            if (!UserRoots().Any(root => SafeUserFolder(item.Path, root)))
                throw new InvalidOperationException("Unsafe or changed folder location.");
            Directory.CreateDirectory(backup);
            string destination = Path.Combine(backup, Path.GetFileName(item.Path));
            Directory.Move(item.Path, destination);
        }
        return backup;
    }
}
