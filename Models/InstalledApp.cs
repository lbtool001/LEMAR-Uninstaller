using System.Windows.Media;
namespace LEMAR.Uninstaller.Models;
public sealed class InstalledApp
{
    public string Name { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string Version { get; init; } = "";
    public string InstallLocation { get; init; } = "";
    public string UninstallString { get; init; } = "";
    public string RegistryPath { get; init; } = "";
    public string Architecture { get; init; } = "";
    public string DisplayIcon { get; init; } = "";
    public string SizeText { get; init; } = "—";
    public string InstalledOn { get; init; } = "—";
    public ImageSource? Icon { get; set; }
}
