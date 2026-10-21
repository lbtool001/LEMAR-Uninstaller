using LEMAR.Uninstaller.Models;
using Microsoft.Win32;
namespace LEMAR.Uninstaller.Services;
public static class InstalledAppScanner
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    public static List<InstalledApp> Scan()
    {
        var apps = new List<InstalledApp>();
        foreach (var (hive, view) in new[] {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Registry64),
            (RegistryHive.CurrentUser, RegistryView.Registry32) })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var root = baseKey.OpenSubKey(UninstallKey);
                if (root is null) continue;
                foreach (var name in root.GetSubKeyNames())
                {
                    try
                    {
                        using var key = root.OpenSubKey(name);
                        if (key is null) continue;
                        var displayName = key.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName)) continue;
                        if (Convert.ToInt32(key.GetValue("SystemComponent", 0)) == 1) continue;
                        if (key.GetValue("ParentKeyName") is not null) continue;
                        apps.Add(new InstalledApp {
                            Name = displayName.Trim(),
                            Publisher = key.GetValue("Publisher") as string ?? "",
                            Version = key.GetValue("DisplayVersion") as string ?? "",
                            InstallLocation = key.GetValue("InstallLocation") as string ?? "",
                            UninstallString = key.GetValue("UninstallString") as string ?? "",
                            RegistryPath = $@"{hive}\{UninstallKey}\{name}",
                            Architecture = view == RegistryView.Registry64 ? "64-bit view" : "32-bit view"
                        });
                    }
                    catch { /* Ignore a malformed or inaccessible entry. */ }
                }
            }
            catch { /* Continue scanning accessible registry views. */ }
        }
        return apps.GroupBy(a => a.RegistryPath + "|" + a.Architecture, StringComparer.OrdinalIgnoreCase)
                   .Select(g => g.First()).OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
