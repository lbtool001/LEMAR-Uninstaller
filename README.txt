LEMAR Uninstaller - targeted cleanup and installation-folder fix

Copy these files into your repository at matching paths:
- Services/CleanupScanner.cs (replace)
- Services/InstallationLocator.cs (new)
- MainWindow.xaml.cs (replace)
- CleanupWindow.xaml.cs (replace)

No XAML or InstalledApp.cs changes required.

Limits: scanner only detects exact-name and derived-alias per-user folders and HKCU\Software keys. It does NOT promise full leftover detection or full force uninstall. Review matches before deletion. Test on disposable applications and back up your PC.
