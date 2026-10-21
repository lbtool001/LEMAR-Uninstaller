LEMAR Uninstaller — Universal cleanup detection update

Replace these files in your GitHub repository:
  Services/CleanupScanner.cs
  Services/InstallationLocator.cs
  MainWindow.xaml.cs
  CleanupWindow.xaml.cs

What it does:
- Scans exact application-named folders in AppData, LocalAppData, ProgramData and both Program Files locations.
- Checks exact application registry keys in HKCU/HKLM, both registry views.
- Looks for exact installation-path references in selected Windows integration registry branches.
- Program Files, ProgramData, HKLM, and shared registry findings are REVIEW ONLY; the app will NOT delete them.
- Only direct per-user AppData folders and exact HKCU Software keys can be backed up and removed.
- Force Removal is not yet a complete forced uninstaller.

The results will not match Geek Uninstaller one-for-one. Generic deep registry discovery and safe removal
of shared-key values need additional engineering. No automated Windows build was run here.

Commit changes and use GitHub Actions to build; test only on expendable apps and review each path.
