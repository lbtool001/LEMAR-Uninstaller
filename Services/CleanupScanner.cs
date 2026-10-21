using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Microsoft.Win32;
using LEMAR.Uninstaller.Models;

namespace LEMAR.Uninstaller.Services;

public sealed class LeftoverItem
{
    public string Path { get; init; } = "";
    public bool IsRegistry { get; init; }
    public bool ReviewOnly { get; init; }
    public string Kind => ReviewOnly ? "Review only" : IsRegistry ? "Registry key" : "Folder";
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
    private static readonly StringComparer Cmp = StringComparer.OrdinalIgnoreCase;
    private static string[] Names(InstalledApp app)
    {
        string name = app.Name.Trim();
        if (!Valid(name)) return Array.Empty<string>();
        var names = new List<string> { name };
        var compact = string.Concat(name.Where(char.IsLetterOrDigit));
        if (Valid(compact) && !names.Contains(compact, Cmp)) names.Add(compact);
        var words = name.Split(new[] {' ', '-', '_'}, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 3)
        {
            string acronym = string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
            if (Valid(acronym) && !names.Contains(acronym, Cmp)) names.Add(acronym);
        }
        return names.ToArray();
    }
    private static bool Valid(string s) => s.Length >= 3 && s.Length <= 100 && s is not "." and not ".." &&
        !s.Equals("LEMAR Uninstaller", StringComparison.OrdinalIgnoreCase) &&
        !s.Equals("Windows", StringComparison.OrdinalIgnoreCase) &&
        !s.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) &&
        s.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !s.Contains('\\') && !s.Contains('/');
    private static string[] Roots() => new[] {
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
    }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(Cmp).ToArray();

    public static List<LeftoverItem> Scan(InstalledApp app) => ScanDetailed(app).Items;
    public static CleanupScanResult ScanDetailed(InstalledApp app)
    {
        var report = new CleanupScanResult();
        var names = Names(app);
        if (names.Length == 0) return report;
        var seen = new HashSet<string>(Cmp);
        foreach (string root in Roots())
        {
            foreach (string name in names)
            {
                string path = Path.Combine(root, name);
                report.LocationsChecked++;
                try
                {
                    if (SafeFolder(path, root) && seen.Add(path))
                    {
                        // Machine-wide and Program Files locations must be reviewed manually.
                        bool perUser = IsUserRoot(root);
                        report.Items.Add(new LeftoverItem { Path = path, ReviewOnly = !perUser });
                    }
                }
                catch (Exception ex) { report.Warnings.Add(path + ": " + ex.Message); }
            }
        }
        // Exact application-specific registry keys, not broad shared Microsoft/Windows keys.
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                foreach (string name in names)
                {
                    string sub = @"Software\" + name;
                    report.LocationsChecked++;
                    using var key = baseKey.OpenSubKey(sub);
                    if (key is null) continue;
                    string path = (hive == RegistryHive.CurrentUser ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE") + @"\" + sub;
                    string id = path + ":" + view;
                    if (seen.Add(id)) report.Items.Add(new LeftoverItem { Path = path, IsRegistry = true, ReviewOnly = hive != RegistryHive.CurrentUser });
                }
            }
            catch (Exception ex) { report.Warnings.Add(hive + " " + view + ": " + ex.Message); }
        }
        // Registered install location may remain after uninstall. Never auto-remove it:
        // it may be shared or the program may still be installed.
        string? install = InstallationLocator.Find(app);
        if (install is not null && !report.Items.Any(x => Cmp.Equals(x.Path, install)))
        {
            report.Items.Add(new LeftoverItem { Path = install, ReviewOnly = true });
        }
        // Inspect limited integration branches for exact references to the application's
        // registered install directory. Findings are informational; shared keys are NEVER deleted.
        ScanSharedReferences(app, report, seen);
        return report;
    }
    private static void ScanSharedReferences(InstalledApp app, CleanupScanResult report, HashSet<string> seen)
    {
        string? install = InstallationLocator.Find(app);
        if (install is null || install.Length < 12) return;
        string needle = Path.TrimEndingDirectorySeparator(install) + "\\";
        string[] branches = { @"Software\Microsoft\Internet Explorer\Low Rights\ElevationPolicy",
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects" };
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                foreach (var branch in branches)
                {
                    using var parent = baseKey.OpenSubKey(branch);
                    if (parent is null) continue;
                    var queue = new Queue<(RegistryKey Key, string Path, int Depth)>();
                    queue.Enqueue((parent, branch, 0));
                    int inspected = 0;
                    while (queue.Count > 0 && inspected < 600)
                    {
                        var (key, path, depth) = queue.Dequeue();
                        using (key)
                        {
                            inspected++;
                            report.LocationsChecked++;
                            try
                            {
                                foreach (string valueName in key.GetValueNames())
                                {
                                    if (key.GetValue(valueName) is string val &&
                                        val.Contains(needle, StringComparison.OrdinalIgnoreCase))
                                    {
                                        string full = (hive == RegistryHive.CurrentUser ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE") + @"\" + path + " [" + (valueName.Length == 0 ? "(Default)" : valueName) + "]";
                                        if (seen.Add(full)) report.Items.Add(new LeftoverItem { Path = full, IsRegistry = true, ReviewOnly = true });
                                    }
                                }
                                if (depth < 2)
                                    foreach (string child in key.GetSubKeyNames())
                                    {
                                        var sub = key.OpenSubKey(child);
                                        if (sub is not null) queue.Enqueue((sub, path + @"\" + child, depth + 1));
                                    }
                            }
                            catch (Exception ex) { if (report.Warnings.Count < 30) report.Warnings.Add(path + ": " + ex.Message); }
                        }
                    }
                }
            }
            catch (Exception ex) { if (report.Warnings.Count < 30) report.Warnings.Add(hive + " " + view + ": " + ex.Message); }
        }
    }
    private static bool IsUserRoot(string root) => Cmp.Equals(root, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) ||
        Cmp.Equals(root, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    private static bool SafeFolder(string path, string root)
    {
        try
        {
            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            if (!Cmp.Equals(Path.GetDirectoryName(full), parent) ||
                Cmp.Equals(Path.GetFileName(full), "LEMAR Uninstaller")) return false;
            var attr = File.GetAttributes(full);
            return (attr & (FileAttributes.ReparsePoint | FileAttributes.Directory)) == FileAttributes.Directory;
        }
        catch { return false; }
    }
    public static string BackupAndDelete(LeftoverItem item)
    {
        if (item.ReviewOnly) throw new InvalidOperationException("This is a review-only finding. No automatic removal is permitted.");
        string backup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LEMAR Uninstaller", "Backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8]);
        if (item.IsRegistry)
        {
            const string prefix = @"HKEY_CURRENT_USER\Software\";
            if (!item.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe registry path.");
            string name = item.Path[prefix.Length..];
            if (!Valid(name)) throw new InvalidOperationException("Unsafe registry key.");
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\" + name);
            if (key is null) throw new InvalidOperationException("Registry key no longer exists.");
            Directory.CreateDirectory(backup);
            string output = Path.Combine(backup, "registry-backup.reg");
            using var process = Process.Start(new ProcessStartInfo("reg.exe") {
                ArgumentList = { "export", item.Path, output, "/y" }, UseShellExecute = false, CreateNoWindow = true });
            if (process is null) throw new InvalidOperationException("Cannot start registry backup.");
            process.WaitForExit();
            if (process.ExitCode != 0 || !File.Exists(output)) throw new InvalidOperationException("Registry backup failed; nothing deleted.");
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\" + name, false);
        }
        else
        {
            if (!Roots().Where(IsUserRoot).Any(root => SafeFolder(item.Path, root)))
                throw new InvalidOperationException("Folder changed or is outside safe user locations.");
            Directory.CreateDirectory(backup);
            Directory.Move(item.Path, Path.Combine(backup, Path.GetFileName(item.Path)));
        }
        return backup;
    }
}
