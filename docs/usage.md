---
title: Usage Guide
layout: default
---

# Usage Guide

This guide walks through the complete workflow for running searches, managing tabs, and taking advantage of Grex’s advanced capabilities.

## 1. Pick Your Search Location

1. Open Grex and click **Browse…** to choose a folder, or paste a path into the AutoSuggestBox.
2. The AutoSuggestBox auto-completes from your last 20 locations (Windows, UNC, and WSL paths are all supported).
3. Examples:
   - Windows: `C:\Users\You\source`
   - UNC: `\\server\share\logs`
   - WSL: `\\wsl$\Ubuntu\home\user\project` or `/mnt/c/Users/You/source`

## 2. Choose Search & Results Modes

- **Search Type** dropdown: Text Search (default) or Regex Search.
- **Results Mode** dropdown: Content (per-line hits) or Files (aggregated by file).
- Switch at any time; Grex re-renders the view without re-running the search.

## 3. Configure Filter Options

Click **Filter Options** in the CommandBar to expand the full set of toggles:

- Respect `.gitignore`, include system files, include subfolders, include hidden items, include binary files, include symbolic links.
- Filename filter (`Match Files`) and directory exclusion (`Exclude Dirs`) inputs.
- Size limit selector (No Limit, Less Than, Equal To, Greater Than) with KB/MB/GB units.
- Windows Search toggle (enabled automatically when the active path and search type qualify).
- String comparison defaults (comparison mode, Unicode normalization, diacritic sensitivity, culture) live under Settings → Filter Options if you want those defaults applied to new tabs.

**Tip**: Set default values for `Match Files` and `Exclude Dirs` in Settings → Filter Options. These defaults automatically populate new tabs, saving you from re-entering common patterns like `*.cs;*.json` or `^(.git|node_modules|vendor)$`.

## 4. Enter Your Query

- Type your search text or Regex into the main input box.
- Optional: Enter replacement text underneath to enable the Replace workflow.
- Press **Enter** or click **Search** to execute.

## 5. Inspect Results

- **Content Mode** shows filename, line, column, snippet, and relative path.
- **Files Mode** shows size, match count, extension, encoding, and timestamps.
- **Status bar** displays match count, file count, and **elapsed time** (formatted as seconds with milliseconds for fast searches, or minutes/hours for longer ones).
- Sort by clicking column headers, drag edges to resize, or right-click headers to hide/show columns (preferences persist per table).
- Double-click any row to open the file in your default editor.

## 6. Replace Safely

- Enter replacement text, click **Replace**, and review the confirmation dialog.
- Grex switches to Files mode so you can see exactly which files will change.
- The operation respects every filter (gitignore, binary toggle, etc.) that the search used.

## 6a. Stop Long-Running Operations

- While a search is running, the **Search** button changes to **Stop** with a stop icon.
- While a replace is running, the **Replace** button changes to **Stop** with a stop icon.
- Click **Stop** at any time to immediately cancel the operation.
- Cancellation is graceful—no errors or partial state corruption.
- During a search, the Replace button is disabled; during a replace, the Search button is disabled to prevent conflicting operations.

## 7. Manage Tabs

- Click “+” to open a fresh search tab; each tab retains its own filters and results.
- Close tabs with the “×” button (at least one tab always stays open).
- Tab titles automatically abbreviate long paths so you can identify them at a glance.

## 8. Explore the Regex Builder

- Click the **Regex Builder** icon in the navigation pane.
- Left column: sample text and pattern inputs with preset buttons (Email, Phone, Date, Digits, URL).
- Right column: live match preview plus a visual breakdown of your Regex syntax.
- Toggles for case-insensitive, multiline, and global matches let you experiment before running a real search.

## 9. Adjust Settings Once, Reuse Everywhere

- Open **Settings** to change application language, theme, default search type/results, filter defaults, Windows Search preference, and string comparison options.
- Settings are saved instantly to `%LocalAppData%\Grex\settings.json` and applied to all future tabs.
- Theme changes (Light/Dark/System) update the entire UI, including the Mica backdrop on Windows 11.

### Backup & Restore Your Settings

The **Backup & Restore** section in Settings lets you preserve or transfer your configuration:

1. **Export Settings** – Click to save a timestamped copy of your settings (e.g., `settings_2024_01_15_14_30_45.json`). Use a file picker to choose where to save it.
2. **Import Settings** – Click to browse for a previously exported JSON file. Grex validates the file and merges the settings into your current configuration. Some changes (like theme or language) may require a restart to take full effect.
3. **Restore Defaults** – Click to delete your settings.json and restart the application with factory defaults. A confirmation dialog ensures you don't accidentally reset.

**Note**: Window position and size are excluded from imports since they are machine-specific.

## Example Searches

- Find TODOs: search for `TODO` with Content mode.
- Locate SSNs: Regex `\b\d{3}-\d{2}-\d{4}\b` with Content mode.
- Scan Linux projects: search `\\wsl$\Ubuntu\home\user\repo` while respecting `.gitignore`.
- Compare error casing: toggle **Search case-sensitive** and search `Error`.
- Narrow by size: set **Size Limit → Less Than 10 MB** to skip large binaries.

## Windows Search Integration Tips

- Enable the toggle when searching Windows folders that are indexed (Documents, Desktop, etc.).
- Grex queries the index for candidate files, then still applies every filter and reads the files to confirm matches—so results remain accurate.
- Regex searches, WSL paths, and non-indexed locations automatically fall back to the custom file walker; no manual toggling required.

## Docker Container Search

1. **Enable Docker Search** – Open **Settings → Docker Search** and toggle "Enable Docker Search" once per machine. This preference is saved and applied to all new tabs.
2. **Select a Container** – After enabling Docker search, each tab shows a "Search Target" dropdown beside the path field. The dropdown defaults to "Local Disk" for normal file system searches.
3. **Choose a Running Container** – Select any running Docker container from the dropdown to search inside that container. The dropdown lists all currently running containers with their names and IDs.
4. **Refresh Container List** – Click the refresh button (↻) next to the dropdown to update the list of running containers without restarting the application.
5. **Enter Container Path** – When a container is selected, enter the path you want to search inside the container (e.g., `/var/www/html`, `/app/src`, `/usr/local/bin`). Grex automatically uses the most efficient search method available.
6. **Search Methods** – Grex uses two search strategies for Docker containers, automatically selecting the best one:
   - **Direct Grep (Preferred)**: Uses the Docker API to run `grep` directly inside the container. This is significantly faster because it doesn't require copying files to the host. Grex automatically checks if `grep` is available in the container.
   - **Mirror Fallback**: If `grep` is not available in the container (e.g., minimal Alpine images without coreutils), Grex falls back to mirroring the container path to a temporary local directory (`%LocalAppData%\Grex\docker-mirrors`) and searching the mirrored files.
7. **Search Behavior** – When searching a container:
   - Grex first attempts the direct grep method via the Docker API
   - If grep is unavailable, it automatically falls back to the mirror approach
   - Result paths are displayed as container paths regardless of which method was used
   - The Browse button is automatically disabled to prevent mixing host and container paths
   - Replace operations are disabled (container files are read-only from the host)
   - Windows Search integration is automatically disabled (containers are not indexed by Windows)
8. **Symbolic Links** – When the "Include symbolic links" option is unchecked (the default), Grex uses `tar --dereference` to copy actual file contents instead of creating symlinks. This avoids Windows privilege issues with directories like `node_modules` that contain many symlinks.
9. **Context Menu** – Right-clicking results in Docker mode shows container-specific options:
   - **Copy Container Path** – Copies the container path (e.g., `/var/www/html/file.txt`) to the clipboard
   - **Copy File Name** – Copies just the filename to the clipboard
   - These shortcuts make it easy to jump back into `docker exec` or your IDE with the correct container path
10. **Automatic Cleanup** – Mirrored container paths are automatically cleaned up after searches complete. Old mirrors (older than 6 hours) are pruned automatically.

**Note**: Docker search requires Docker Desktop to be installed and running. The container dropdown will be disabled if Docker is not available. For the fastest searches, use containers that include `grep` (most Linux-based images do).

**Performance**: Docker searches use parallel grep execution (`xargs -P 4`) and grep availability caching for near-instant repeat searches. System paths (`.git`, `vendor`, `node_modules`, `storage/framework`, `bin`, `obj`) are filtered at the `find` level for maximum performance.

## WSL Workflows

- Make sure WSL is installed and the `wsl` command works from PowerShell.
- Use `\\wsl$\Distro\path` or `/mnt/...` paths; Grex converts them and shells out to `grep` when necessary.
- Case-insensitive toggles translate to the `-i` flag; other comparison settings are ignored because `grep` handles the matching.

## Notifications & Diagnostics

- The Settings page includes **Test Notification** to verify Windows toast support and troubleshoot missing Windows App Runtime installations.
- If Grex launches elevated, it warns you because Windows notifications don’t fire under admin privileges; re-open via `explorer.exe` from a non-elevated context for the best experience.

Keep this guide handy for day-to-day workflows. When you need implementation details or architectural context, see `docs/architecture.md` and `docs/build-and-test.md`.***

