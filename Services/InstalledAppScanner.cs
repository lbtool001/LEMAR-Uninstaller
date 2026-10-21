using System.IO;
using LEMAR.Uninstaller.Models;
using Microsoft.Win32;
using System.Globalization;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows;
using System.Runtime.InteropServices;
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
                            Architecture = view == RegistryView.Registry64 ? "64-bit view" : "32-bit view",
                            DisplayIcon = key.GetValue("DisplayIcon") as string ?? "",
                            SizeText = FormatSize(key.GetValue("EstimatedSize")),
                            InstalledOn = FormatDate(key.GetValue("InstallDate") as string)
                        });
                    }
                    catch { /* Ignore a malformed or inaccessible entry. */ }
                }
            }
            catch { /* Continue scanning accessible registry views. */ }
        }
        var result = apps.GroupBy(a => a.RegistryPath + "|" + a.Architecture, StringComparer.OrdinalIgnoreCase)
                   .Select(g => g.First()).OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var app in result) app.Icon = LoadIcon(app.DisplayIcon, app.InstallLocation);
        return result;
    }
    private static string FormatSize(object? value)
    {
        if (!long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var kb) || kb <= 0) return "—";
        double bytes = kb * 1024d;
        return bytes >= 1073741824 ? $"{bytes / 1073741824:0.##} GB" : $"{bytes / 1048576:0.##} MB";
    }
    private static string FormatDate(string? value) =>
        DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
        ? d.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) : "—";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr instance, string file, uint index);
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);
    private static System.Windows.Media.ImageSource? LoadIcon(string iconValue, string folder)
    {
        try
        {
            string candidate = Environment.ExpandEnvironmentVariables(iconValue.Trim());
            if (candidate.StartsWith('"')) { int end = candidate.IndexOf('"', 1); if (end > 1) candidate = candidate[1..end]; }
            else { int comma = candidate.LastIndexOf(','); if (comma > 0 && int.TryParse(candidate[(comma + 1)..], out _)) candidate = candidate[..comma]; }
            if (!File.Exists(candidate)) return null;
            var handle = ExtractIcon(IntPtr.Zero, candidate, 0);
            if (handle == IntPtr.Zero || handle == new IntPtr(1)) return null;
            try { var image = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(20,20)); image.Freeze(); return image; }
            finally { DestroyIcon(handle); }
        }
        catch { return null; }
    }
}
