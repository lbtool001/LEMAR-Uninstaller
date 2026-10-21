using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LEMAR.Uninstaller.Models;
using LEMAR.Uninstaller.Services;
namespace LEMAR.Uninstaller;
public partial class MainWindow : Window
{
    private List<InstalledApp> _allApps = new();
    private readonly ObservableCollection<InstalledApp> _visibleApps = new();
    public MainWindow()
    {
        ThemeService.Load();
        InitializeComponent();
        ThemeButton.Content = ThemeService.Dark ? "☀ Light" : "☾ Dark";
        AppsGrid.ItemsSource = _visibleApps;
        Loaded += async (_, _) => await RefreshAsync();
    }
    private async Task RefreshAsync()
    {
        StatusText.Text = "Scanning installed applications...";
        try { _allApps = await Task.Run(InstalledAppScanner.Scan); ApplyFilter(); StatusText.Text = "Scan complete. Double-click to uninstall, or right-click for options."; }
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
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { if (_visibleApps is not null) ApplyFilter(); }
    private void Theme_Click(object sender, RoutedEventArgs e) { ThemeService.Toggle(); ThemeButton.Content = ThemeService.Dark ? "☀ Light" : "☾ Dark"; }
    private void AppsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppsGrid.SelectedItem is InstalledApp app) StatusText.Text = app.Name;
    }
    private void AppsGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? node = e.OriginalSource as DependencyObject;
        while (node is not null && node is not DataGridRow) node = VisualTreeHelper.GetParent(node);
        if (node is DataGridRow row) AppsGrid.SelectedItem = row.Item;
    }
    private void AppsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? node = e.OriginalSource as DependencyObject;
        while (node is not null && node is not DataGridRow) node = VisualTreeHelper.GetParent(node);
        if (node is DataGridRow) UninstallSelected();
    }
    private void Uninstall_Click(object sender, RoutedEventArgs e) => UninstallSelected();
    private async void UninstallSelected()
    {
        if (AppsGrid.SelectedItem is not InstalledApp app) return;
        if (string.IsNullOrWhiteSpace(app.UninstallString)) { MessageBox.Show("No registered uninstaller. Try Force Removal for a conservative scan."); return; }
        if (MessageBox.Show($"Launch the registered uninstaller for {app.Name}?", "Confirm uninstall", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            var process = UninstallLauncher.Launch(app.UninstallString);
            StatusText.Text = $"Uninstaller launched for {app.Name}.";
            if (process is null) { MessageBox.Show("The uninstaller launched, but its completion cannot be tracked. Use Force Removal after it finishes."); return; }
            await process.WaitForExitAsync();
            if (MessageBox.Show("The uninstaller process exited. This does not guarantee removal succeeded. Scan for possible leftovers now?", "Post-uninstall scan", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                new CleanupWindow(app) { Owner = this }.ShowDialog();
            await RefreshAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Uninstall error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void ForceRemoval_Click(object sender, RoutedEventArgs e)
    {
        if (AppsGrid.SelectedItem is InstalledApp app) new CleanupWindow(app) { Owner = this }.ShowDialog();
    }
    private void RemoveEntry_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Removing uninstall registry entries is disabled in this safety-focused build. Use Windows' normal uninstaller or the reviewed leftover scanner.", "Protected action");
    }
    private void RegistryEntry_Click(object sender, RoutedEventArgs e)
    {
        if (AppsGrid.SelectedItem is InstalledApp app) { Clipboard.SetText(app.RegistryPath); MessageBox.Show("Registry path copied to clipboard. Open Registry Editor manually if needed.\n\n" + app.RegistryPath); }
    }
    private void InstallationFolder_Click(object sender, RoutedEventArgs e)
    {
        if (AppsGrid.SelectedItem is not InstalledApp app || !Directory.Exists(app.InstallLocation)) { MessageBox.Show("Installation folder is unavailable."); return; }
        try { Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { app.InstallLocation }, UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    private void SearchOnline_Click(object sender, RoutedEventArgs e)
    {
        if (AppsGrid.SelectedItem is InstalledApp app) Process.Start(new ProcessStartInfo("https://www.google.com/search?q=" + Uri.EscapeDataString(app.Name)) { UseShellExecute = true });
    }
}
