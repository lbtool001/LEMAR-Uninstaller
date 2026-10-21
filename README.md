# LEMAR Uninstaller

Portable Windows uninstaller built with C#, .NET 10, and WPF.

## Features
- Compact non-resizable window, dark/light theme, installed app icons, estimated size and install date when Windows provides them.
- Right-click context menu and double-click uninstall.
- Conservative post-uninstall cleanup: scans **exactly named** folders directly under the current user’s Local/Roaming AppData, and exact HKCU\Software keys. No broad matching, no system-wide registry cleanup.
- Explicit selection and confirmation. Registry keys are exported before deletion; folders are moved to backups under `%LOCALAPPDATA%\LEMAR Uninstaller\Backups`.

## Important limitations
- Cleanup detection is intentionally incomplete. It can miss remnants, and a matching folder/key can still contain shared data. Review before removal.
- `Remove Entry` is intentionally disabled until registry safeguards are implemented.
- An uninstall process exiting does not prove success; some uninstallers spawn child processes.
- Installed size/date are based on optional Windows uninstall registry metadata, not a disk scan.
- This project has not yet undergone a Windows runtime test of these new features. Do not use cleanup on important software without a backup.

## Build
Use the included GitHub Actions workflow, or on Windows with .NET 10 SDK: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`.
