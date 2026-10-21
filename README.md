# LEMAR Uninstaller — v0.1 source

Free Windows desktop uninstaller starter application using C#, .NET 10 and WPF.

## Build
1. On Windows 10/11, install the .NET 10 SDK (with Windows desktop support) or Visual Studio with the .NET desktop development workload.
2. Open `LEMAR.Uninstaller.csproj` in Visual Studio, or run `dotnet run` in this folder.
3. For a distributable Windows executable, run:
   `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
4. Find the output in `bin/Release/net10.0-windows/win-x64/publish/`.

## Included
- Dark hybrid interface, Windows desktop registry scanning, filtering, uninstall command launching, and confirmation.

## Limitations / safety
- This starter has not been compiled or run in this environment (Windows/.NET SDK unavailable here).
- No leftover deletion, forced uninstall, Microsoft Store app management, batch uninstall, restore-point creation, or theme toggle yet.
- Registered uninstall commands originate from installed software; only run uninstallers you trust.
- Some uninstallers use unusual commands that the initial parser will not support. It never invokes cmd.exe.
- This software does not delete registry keys or files itself.
- Scan and uninstall are best tested on a Windows VM with snapshots before everyday use.
