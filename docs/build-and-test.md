---
title: Build & Test Reference
layout: default
---

# Build & Test Reference

Use this reference when you need to set up Grex locally, run the full test matrix, generate release assets, or troubleshoot platform/runtime requirements.

## Prerequisites

- **Windows 10 1809+** or **Windows 11**
- **.NET 8.0 SDK**
- **Windows App SDK 1.8** (restored via NuGet)
- **Windows App Runtime 1.8 (x64)** for toast notifications and diagnostics  
  `winget install --id Microsoft.WindowsAppRuntime.1.8 -e --source winget`
- **Visual Studio 2022** (recommended) with the “.NET desktop development” workload  
  or VS Code + C# Dev Kit
- **WSL** installed if you plan to search Linux filesystems

## Building the Solution

> The WinApp SDK tooling requires a concrete platform (x86, x64, ARM64). “Any CPU” builds will fail.

```powershell
# Restore
dotnet restore

# Debug build (x64)
dotnet build Grex.sln -p:Platform=x64

# Release build
dotnet build Grex.sln -c Release -p:Platform=x64

# Other platforms
dotnet build Grex.sln -p:Platform=x86
dotnet build Grex.sln -p:Platform=ARM64
```

### Running the App

```powershell
dotnet run --project Grex.csproj -p:Platform=x64
```

Launching from an elevated PowerShell? Use Explorer to drop privileges so Windows toast notifications function:

```powershell
explorer.exe ".\bin\x64\Debug\net8.0-windows10.0.19041.0\Grex.exe"
```

### Clean & Publish

```powershell
dotnet clean Grex.sln
dotnet publish Grex.csproj -c Release --self-contained -r win-x64
```

Publish output lands in `bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/`. This creates a self-contained deployment with all .NET runtime dependencies bundled, so the app can run on any Windows x64 machine without requiring .NET to be installed.

## Testing

Grex ships three test projects covering unit, integration, and UI scenarios. Always specify the platform, just like regular builds.

```powershell
# Run everything
dotnet test Grex.sln -p:Platform=x64

# Individual projects
dotnet test Tests/Grex.Tests.csproj -p:Platform=x64
dotnet test IntegrationTests/Grex.IntegrationTests.csproj -p:Platform=x64
dotnet test UITests/Grex.UITests.csproj -p:Platform=x64
```

- Unit tests rely on xUnit + Moq + FluentAssertions.
- Integration tests create temporary directories/files and clean them up automatically.
- UI tests exercise ViewModel-driven behavior without requiring WinAppDriver.

### Python Script Tests

The localization scripts include unit tests:

```powershell
# Run with pytest (recommended)
python -m pytest Scripts/test_add_localization_entry.py -v

# Or run directly
python Scripts/test_add_localization_entry.py
```

### Skipped Tests

A few localization tests are intentionally skipped because WinUI resource loaders need a full app context. Integration tests cover those scenarios instead.

## Runtime Requirements & Diagnostics

- **Windows notifications** – Require Windows App Runtime. Use **Settings → Debug → Test Notification** to confirm the environment or get remediation steps/log locations.
- **Administrator warning** – Grex detects elevated launches and warns because toasts don’t fire in admin sessions.
- **WSL searches** – Ensure the `wsl` command works from PowerShell and that your distro is running; Grex shells out to `grep` where appropriate.

## Icon & Asset Generation

Grex includes dozens of logo sizes referenced in `Grex.csproj` and `Package.appxmanifest`. Regenerate everything from `Assets/Grex.png` with ImageMagick:

```powershell
$magick = "C:\Program Files\ImageMagick-7.1.2-Q16-HDRI\magick.exe"
& $magick Assets\Grex.png -alpha on -resize 16x16  -background none -gravity center -extent 16x16  Assets\Square16x16Logo.png
& $magick Assets\Grex.png -alpha on -resize 24x24  -background none -gravity center -extent 24x24  Assets\Square24x24Logo.png
...
& $magick Assets\Grex.png -alpha on -resize 1024x1024 -background none -gravity center -extent 1024x1024 Assets\Square1024x1024.png
& $magick Assets\Grex.png -alpha on -resize 310x150^ -background none -gravity center -extent 310x150 Assets\Square310x150Logo.png
& $magick Assets\Grex.png -alpha on -resize 620x300^ -background none -gravity center -extent 620x300 Assets\SplashScreen.png
& $magick Assets\Grex.png -alpha on -define icon:auto-resize=256,128,96,64,48,32,16 -define icon:format=png -background none Assets\Grex.ico
```

Alternatively, run the `IconGenerator` utility under `IconGenerator/` (`dotnet build && dotnet run`) to produce all assets automatically.

## Windows Search & WSL Notes

- Windows Search acceleration is optional per tab or as a default preference in Settings. It only applies to plain-text searches on indexed Windows folders; Regex searches and WSL paths automatically fall back to the traditional scanner.
- WSL paths (`\\wsl$\Distro\path`, `/mnt/...`) are translated so Windows and Linux workflows stay seamless; Unicode comparison settings are ignored because `grep` performs the matching inside the distro.

## File Locations & Logs

- **Settings** – `%LocalAppData%\Grex\settings.json`
- **Recent paths** – `%LocalAppData%\Grex\search_path_history.json`
- **Diagnostics logs** – `%Temp%\Grex.log` plus `%LocalAppData%\Grex\notification_test.log` when running the notification tester.

Refer back to this document whenever you need the authoritative commands and requirements. For feature walk-throughs see `docs/features.md`, for daily workflows see `docs/usage.md`, and for architectural breakdowns see `docs/architecture.md`.***

