using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using LEMAR.Uninstaller.Models;
using LEMAR.Uninstaller.Services;
namespace LEMAR.Uninstaller;
public partial class MainWindow : Window
{
    private List<InstalledApp> _allApps = new();
    private readonly ObservableCollection<InstalledApp> _visibleApps = new();
    public MainWindow() { InitializeComponent(); AppsGrid.ItemsSource = _visibleApps; Loaded += (_, _) => Refresh(); }
    private void Refresh()
    {
        try { _allApps = InstalledAppScanner.Scan(); ApplyFilter(); StatusText.Text = "Scan complete. Select an application to view its details."; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Scan failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void ApplyFilter()
    {
        var term = SearchBox.Text.Trim();
        _visibleApps.Clear();
        foreach (var app in _allApps.Where(a => string.IsNullOrEmpty(term) ||
            a.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            a.Publisher.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            a.Version.Contains(term, StringComparison.OrdinalIgnoreCase))) _visibleApps.Add(app);
        CountText.Text = $"{_visibleApps.Count} applications";
    }
    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { if (_visibleApps is not null) ApplyFilter(); }
    private void AppsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppsGrid.SelectedItem is InstalledApp app)
            StatusText.Text = $"{app.Name}  •  {app.InstallLocation}  •  {(string.IsNullOrEmpty(app.UninstallString) ? "No uninstaller registered" : "Uninstaller available")}";
    }
    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (AppsGrid.SelectedItem is not InstalledApp app) { MessageBox.Show("Select an application first."); return; }
        if (string.IsNullOrWhiteSpace(app.UninstallString)) { MessageBox.Show("This entry has no registered uninstall command."); return; }
        var confirm = MessageBox.Show($"Launch the registered uninstaller for:\n\n{app.Name}\n{app.Publisher}\n\nWindows may request administrator permission. Continue?", "Confirm uninstall", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;
        try { UninstallLauncher.Launch(app.UninstallString); StatusText.Text = $"Uninstaller launched for {app.Name}. Refresh after it finishes."; }
        catch (Exception ex) { MessageBox.Show($"Could not launch the uninstaller:\n{ex.Message}", "Uninstall error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
