using System.Collections.ObjectModel;
using System.Windows;
using LEMAR.Uninstaller.Models;
using LEMAR.Uninstaller.Services;
namespace LEMAR.Uninstaller;
public partial class CleanupWindow : Window
{
    private readonly ObservableCollection<LeftoverItem> _items;
    public CleanupWindow(InstalledApp app)
    {
        InitializeComponent();
        Heading.Text = "Review leftovers — " + app.Name;
        _items = new ObservableCollection<LeftoverItem>(CleanupScanner.Scan(app));
        Results.ItemsSource = _items;
        Info.Text = _items.Count == 0 ? "No high-confidence leftovers found" : $"{_items.Count} potential leftovers";
    }
    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        Results.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        foreach (var item in _items) item.Selected = true;
        Results.Items.Refresh();
    }
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        Results.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        Results.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        var selected = _items.Where(x => x.Selected).ToList();
        if (selected.Count == 0) { MessageBox.Show("Select items first."); return; }
        if (MessageBox.Show($"Remove {selected.Count} selected item(s)?\n\nRegistry keys are exported first. Folders are moved to a backup location. This can affect other applications sharing these settings.", "Confirm cleanup", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var errors = new List<string>();
        foreach (var item in selected)
        {
            try { CleanupScanner.BackupAndDelete(item); _items.Remove(item); }
            catch (Exception ex) { errors.Add(item.Path + ": " + ex.Message); }
        }
        Info.Text = errors.Count == 0 ? "Cleanup complete; backups saved under LocalAppData" : $"{errors.Count} item(s) could not be removed";
        if (errors.Count > 0) MessageBox.Show(string.Join("\n", errors), "Cleanup errors", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
