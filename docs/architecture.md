---
title: Architecture
layout: default
---

# Architecture

This document covers the technical foundation of Grex: the stack, project layout, and the major components that power search, replace, localization, and UI responsiveness.

## Technology Stack

- **.NET 8** with C# 12
- **WinUI 3** + Windows App SDK 1.8 for modern Windows desktops
- **MVVM** for clear separation of UI (Views), state (ViewModels), and logic (Services)
- **Windows App Runtime** features (toast notifications, Mica backdrop, resource management)
- **WSL integration** via `wsl.exe` and `grep` for Linux-side searches
- **Docker.DotNet** for Docker API access (container exec, grep-based searches)

## Project Structure

```
Grex/
├── App.xaml / App.xaml.cs              # Application entry point
├── MainWindow.xaml / .cs               # Shell + navigation, TabView host
├── Controls/
│   ├── SearchTabContent.xaml / .cs     # Search UI per tab
│   ├── RegexBuilderView.xaml / .cs     # Visual Regex builder
│   ├── SettingsView.xaml / .cs         # Default preferences UI
│   └── ResultsTemplateSelector.cs
├── Models/                             # DTOs for results, suggestions, enums
├── ViewModels/
│   ├── MainViewModel.cs                # Tab orchestration
│   └── TabViewModel.cs                 # Per-tab search state
├── Services/
│   ├── SearchService, GitIgnoreService,
│       RecentPathsService, SettingsService,
│       LocalizationService, EncodingDetectionService,
│       DockerSearchService, AdminHelper,
│       NotificationService, etc.
├── Strings/<culture>/Resources.resw    # Localization dictionaries
├── Tests/, IntegrationTests/, UITests/ # Unit, integration, and UI test suites
└── docs/                               # Detailed documentation
```

## Key Components

### SearchService

- Detects whether a path is Windows, UNC, or WSL.
- Enumerates files with `EnumerationOptions`, applies filename/directory filters, honors `.gitignore`, and respects binary/link toggles.
- Streams each file with `StreamReader`, applying Unicode normalization, diacritic stripping, and custom string comparison before matching.
- Compiles Regex patterns once per search.
- Shells out to `wsl grep` for Linux paths and parses the output back into `SearchResult` objects.
- Performs Replace operations, switching results to Files mode so users can see the impact.

### GitIgnoreService

- Parses `.gitignore` files at every directory level, caches the compiled rules, and mirrors Git's matching semantics (negations, directory-only patterns, root-relative patterns, etc.).
- **Root-relative patterns** – Patterns starting with `/` (e.g., `/storage/app`) only match paths from the repository root, not individual path segments. This prevents `/storage/app` from incorrectly matching files in `/app` directories.
- **Directory patterns** – Patterns ending with `/` (e.g., `build/` or `/storage/app/`) match files inside those directories. Root-relative directory patterns correctly handle path comparisons by removing the leading `/` when checking directory membership.
- **Pattern matching** – Converts gitignore patterns to regex with proper escaping, handles wildcards (`*`, `?`, `**`), bracket patterns, and respects case-insensitive matching. Root-relative patterns skip segment-level matching to ensure accurate results.

### RecentPathsService

- Stores up to 20 recent paths at `%LocalAppData%\Grex\search_path_history.json`.
- Provides filtered suggestions to the AutoSuggestBox with add/remove support.

### SettingsService

- Persists all defaults (search type, filters, comparison settings, theme, Windows Search toggle, etc.) plus column visibility and window geometry in `%LocalAppData%\Grex\settings.json`.
- Saves immediately on change and gracefully recreates defaults if the file is missing/corrupt.

### LocalizationService

- Wraps WinApp SDK’s `ResourceManager` and `.resw` files; supports en-US, es-ES, fr-FR, de-DE out of the box.
- Validates culture codes, falls back to English when a translation is missing, and notifies the UI whenever the app language changes.
- Exposes helper methods for formatted strings and caches resource contexts per culture.

### EncodingDetectionService

- Uses BOM, statistical analysis, and heuristics to detect 30+ encodings (Unicode variants, ISO-8859, Windows code pages, Asian/Cyrillic encodings).
- Converts supported document formats (Office Open XML, OpenDocument, PDF, RTF, ZIP) to text when "Include binary files" is checked.

### DockerSearchService

- Manages Docker container search operations with two strategies: direct grep via the Docker API and local mirror fallback.
- **Docker.DotNet integration** – Uses the `Docker.DotNet` library to execute `grep` directly inside containers via the Docker API, avoiding file copying.
- **Grep availability caching** – Caches grep availability per container to eliminate redundant checks on subsequent searches.
- **Parallel grep execution** – Uses `find -print0 | xargs -0 -P 4 grep` for multi-core parallel searching inside containers.
- **Smart find-level filtering** – Applies system path exclusions (`.git`, `vendor`, `node_modules`, `storage/framework`, `bin`, `obj`), hidden file filters, and binary extension filters at the `find` level before grep runs for maximum performance.
- **Grep availability check** – Runs `which grep` in the container before attempting the direct search; falls back to mirroring if grep is unavailable.
- **Mirror management** – Creates temporary local copies of container paths in `%LocalAppData%\Grex\docker-mirrors` when the fallback is needed.
- **Symlink handling** – Uses `tar --dereference` when mirroring to copy actual file contents, avoiding Windows privilege issues.
- **Automatic cleanup** – Prunes expired mirrors (older than 6 hours) and cleans up after each search.
- **Grep output parsing** – Parses `filename:line:content` format into `SearchResult` objects, handling edge cases like colons in content and binary file markers. Correctly counts multiple matches per line and calculates column positions.

### RegexBuilderView

- Provides dual-pane real-time Regex evaluation: sample text on the left, match output plus visual breakdown on the right.
- Includes presets (Email, Phone, Date, Digits, URL) and toggles for case-insensitive, multiline, and global matches.

### MainWindow & Tab System

- `MainWindow` hosts the SplitView/NavigationView shell, the Search TabView, Regex Builder, Settings, and the shared InfoBar.
- `MainViewModel` manages tab lifecycles, while `TabViewModel` encapsulates all per-tab search configuration, status text, sorting, and result collections.

## Search Flow

1. User enters a path and query in `SearchTabContent`.
2. `TabViewModel` validates input.
3. If Docker mode is active (container selected):
   - First attempts direct grep search via `DockerSearchService.SearchInContainerAsync()` using the Docker API.
   - If grep is available, results are returned directly without file copying.
   - If grep is unavailable, falls back to mirroring the container path locally, then proceeds to step 4.
4. `TabViewModel` calls `SearchService` with the effective path (local or mirrored).
5. `SearchService` determines path type, applies filters, leverages Windows Search when allowed, and streams matching lines/files back.
6. `TabViewModel` populates either `ObservableCollection<SearchResult>` (Content mode) or `ObservableCollection<FileSearchResult>` (Files mode).
7. The UI updates automatically via data binding; column sort/visibility state stays in sync with saved preferences.

## Matching Logic

- **Text searches** – Apply optional Unicode normalization, diacritic stripping (unless diacritic-sensitive), and culture-aware `StringComparison`. Case sensitivity is governed by the global checkbox.
- **Regex searches** – Only case sensitivity matters (adds/removes `RegexOptions.IgnoreCase`). Other comparison settings are ignored because .NET Regex handles them internally.
- **Binary/document formats** – Extracted to text first, then fed through the same pipeline as plain text.
- **WSL paths** – Delegated to `grep` inside WSL; only the case flag translates (`-i`).

## Project Notes

- **File filtering** – Binary formats unsupported by the extractor (images, executables, legacy Office, etc.) are skipped even when “Include binary files” is enabled.
- **Performance** – Streamed IO, parallel file enumeration (max 8), cached `.gitignore` parsing, and optional Windows Search seeding keep searches responsive.
- **User data** – Settings, recent paths, notification diagnostics, and logs all live under `%LocalAppData%` or `%Temp%`.
- **Notifications** – Requires Windows App Runtime 1.8; Grex ships a Test Notification button with detailed diagnostics.
- **Limitations** – Replace has no undo; max 20 recent paths; 500-character snippet limit per result row.

For build pipelines, testing strategy, and asset regeneration instructions, see `docs/build-and-test.md`. For day-to-day workflows, see `docs/usage.md`.***

