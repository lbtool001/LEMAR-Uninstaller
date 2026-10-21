using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LEMAR.Uninstaller.Models;

namespace LEMAR.Uninstaller.Services;

public static class InstallationLocator
{
    public static string? Find(InstalledApp app)
    {
        foreach (string candidate in Candidates(app))
        {
            try
            {
                string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(candidate.Trim().Trim('"')));
                if (!Directory.Exists(full) || !IsPlausibleInstallDirectory(full)) continue;
                return full;
            }
            catch (Exception) { /* Invalid metadata is not a valid folder. */ }
        }
        return null;
    }

    private static IEnumerable<string> Candidates(InstalledApp app)
    {
        if (!string.IsNullOrWhiteSpace(app.InstallLocation)) yield return app.InstallLocation;
        string? icon = ExecutablePath(app.DisplayIcon);
        if (icon is not null && File.Exists(icon))
        {
            string? dir = Path.GetDirectoryName(icon);
            if (dir is not null) yield return dir;
        }
        string? uninstall = ExecutablePath(app.UninstallString);
        if (uninstall is not null && File.Exists(uninstall) &&
            !Path.GetFileName(uninstall).Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase))
        {
            string? dir = Path.GetDirectoryName(uninstall);
            if (dir is not null) yield return dir;
        }
    }

    private static string? ExecutablePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string s = Environment.ExpandEnvironmentVariables(value.Trim());
        if (s.StartsWith('"'))
        {
            int end = s.IndexOf('"', 1);
            return end > 1 ? s[1..end] : null;
        }
        // DisplayIcon commonly ends in ',0' or ',1'.
        s = Regex.Replace(s, @",\s*-?\d+\s*$", "");
        var match = Regex.Match(s, @"^(.+?\.(?:exe|dll|ico))(?:\s|$)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool IsPlausibleInstallDirectory(string path)
    {
        string p = Path.TrimEndingDirectorySeparator(path);
        string[] blocked = {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };
        if (Path.GetPathRoot(p)?.TrimEnd('\\') == p.TrimEnd('\\')) return false;
        if (blocked.Any(x => !string.IsNullOrWhiteSpace(x) &&
            string.Equals(Path.TrimEndingDirectorySeparator(x), p, StringComparison.OrdinalIgnoreCase))) return false;
        return (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0;
    }
}
