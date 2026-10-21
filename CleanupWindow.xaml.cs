using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LEMAR.Uninstaller.Models;
using LEMAR.Uninstaller.Services;

namespace LEMAR.Uninstaller;

public partial class CleanupWindow : Window
{
    private readonly ObservableCollection<LeftoverItem> _items = new();
    private readonly InstalledApp _app;
    private bool _scanFinished;

    public CleanupWindow(InstalledApp app)
    {
        _app = app;
        InitializeComponent();
        Heading.Text = "Review leftovers — " + app.Name;
        Results.ItemsSource = _items;
        Info.Text = "Scanning possible leftovers...";
        Loaded += async (_, _) => await ScanAsync();
    }

    private async Task ScanAsync()
    {
        try
        {
            var report = await Task.Run(() => CleanupScanner.ScanDetailed(_app));
            foreach (var item in report.Items) _items.Add(item);
            _scanFinished = true;
            Info.Text = _items.Count == 0
                ? $"No matching leftovers found ({report.LocationsChecked} checked)"
                : $"{_items.Count} findings (review-only items cannot be removed)";
            if (report.Warnings.Count > 0)
                MessageBox.Show(string.Join("\n", report.Warnings.Take(10)),
                    $"Scan warnings ({report.Warnings.Count})", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            Info.Text = "Scan failed";
            MessageBox.Show(ex.Message, "Scan error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (!_scanFinished) return;
        Results.CommitEdit(DataGridEditingUnit.Row, true);
        foreach (var item in _items.Where(x => !x.ReviewOnly)) item.Selected = true;
        Results.Items.Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!_scanFinished) { MessageBox.Show("Wait for the scan to finish."); return; }
        Results.CommitEdit(DataGridEditingUnit.Cell, true);
        Results.CommitEdit(DataGridEditingUnit.Row, true);
        var selected = _items.Where(x => x.Selected).ToList();
        if (selected.Count == 0) { MessageBox.Show("Select items first."); return; }
        if (MessageBox.Show($"Back up and remove {selected.Count} selected item(s)?\n\n" +
            "Exact-name matches can belong to other software. Registry keys are exported first; " +
            "folders are moved to a backup location. Continue only if you recognize every item.",
            "Confirm cleanup", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var errors = new System.Collections.Generic.List<string>();
        foreach (var item in selected)
        {
            try { CleanupScanner.BackupAndDelete(item); _items.Remove(item); }
            catch (Exception ex) { errors.Add(item.Path + ": " + ex.Message); }
        }
        Info.Text = errors.Count == 0 ? "Cleanup complete; backups saved under LocalAppData" :
            $"{errors.Count} item(s) could not be removed";
        if (errors.Count > 0)
            MessageBox.Show(string.Join("\n", errors), "Cleanup errors", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
