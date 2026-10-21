using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
namespace LEMAR.Uninstaller.Services;
public static class UninstallLauncher
{
    // Never pass a registry-provided command to cmd.exe or PowerShell.
    public static Process? Launch(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) throw new InvalidOperationException("This program has no registered uninstaller.");
        command = Environment.ExpandEnvironmentVariables(command.Trim());
        string executable, arguments;
        if (command.StartsWith('"'))
        {
            int closing = command.IndexOf('"', 1);
            if (closing < 2) throw new InvalidOperationException("Invalid uninstall command.");
            executable = command[1..closing];
            arguments = command[(closing + 1)..].Trim();
        }
        else
        {
            var match = Regex.Match(command, @"^(.+?\.exe)(?:\s+(.*))?$", RegexOptions.IgnoreCase);
            if (!match.Success) throw new InvalidOperationException("Unsupported uninstall command. No executable was found.");
            executable = match.Groups[1].Value;
            arguments = match.Groups[2].Value;
        }
        if (!executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only executable uninstall commands are supported in this version.");
        // Registered MSI strings often use /I; change to /X for actual removal.
        if (Path.GetFileName(executable).Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase))
            arguments = Regex.Replace(arguments, @"(?i)(?<!\S)/I(?=\s|\{|""|$)", "/X");
        return Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = true, Verb = "runas" });
    }
}
