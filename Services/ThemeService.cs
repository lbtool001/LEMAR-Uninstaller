using System.Windows;
using System.Windows.Media;
namespace LEMAR.Uninstaller.Services;
public static class ThemeService
{
    private static string SettingsFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEMAR Uninstaller", "theme.txt");
    public static bool Dark { get; private set; } = true;
    public static void Load()
    {
        try { Dark = File.ReadAllText(SettingsFile).Trim() != "light"; } catch { Dark = true; }
        Apply();
    }
    public static void Toggle()
    {
        Dark = !Dark; Apply();
        try { Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!); File.WriteAllText(SettingsFile, Dark ? "dark" : "light"); } catch { }
    }
    public static void Apply()
    {
        var colors = Dark
            ? new[] { "#10151E", "#19212E", "#F2F5FA", "#AAB9CD", "#6C9FFF", "#222D3D", "#202A39", "#2E415B" }
            : new[] { "#F4F7FB", "#FFFFFF", "#172033", "#506176", "#215CB5", "#E6ECF4", "#EFF3F9", "#D9E6F8" };
        string[] names = { "BackgroundBrush", "PanelBrush", "TextBrush", "MutedBrush", "AccentBrush", "InputBrush", "AlternateBrush", "SelectedBrush" };
        for (int i = 0; i < names.Length; i++) Application.Current.Resources[names[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
    }
}
