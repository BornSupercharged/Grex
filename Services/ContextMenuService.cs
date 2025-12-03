using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Foundation;

namespace Grex.Services
{
    /// <summary>
    /// Service for displaying Windows Explorer context menus for files and folders.
    /// Handles both Windows native paths and WSL paths with proper conversion.
    /// Uses WinRT StorageFolder and MenuFlyout instead of COM interop.
    /// </summary>
    public class ContextMenuService
    {
        private static readonly string LogFilePath = Path.Combine(Path.GetTempPath(), "Grex.log");

        #region Private Fields

        private readonly NotificationService _notificationService;

        #endregion

        #region Constructor

        public ContextMenuService()
        {
            _notificationService = NotificationService.Instance;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the Windows Explorer context menu for the specified file at the given screen position.
        /// </summary>
        /// <param name="filePath">The full path to the file or folder</param>
        /// <param name="screenX">The X coordinate of the screen position</param>
        /// <param name="screenY">The Y coordinate of the screen position</param>
        /// <param name="targetElement">Optional UIElement to get XamlRoot from. If null, will try to get from Window.Current</param>
        public void ShowContextMenu(string filePath, int screenX, int screenY, UIElement? targetElement = null)
        {
            // Fire and forget - call async method without awaiting
            _ = ShowContextMenuAsync(filePath, screenX, screenY, targetElement);
        }

        /// <summary>
        /// Shows the Windows Explorer context menu for the specified file at the given screen position (async version).
        /// </summary>
        /// <param name="filePath">The full path to the file or folder</param>
        /// <param name="screenX">The X coordinate of the screen position</param>
        /// <param name="screenY">The Y coordinate of the screen position</param>
        /// <param name="targetElement">Optional UIElement to get XamlRoot from. If null, will try to get from Window.Current</param>
        public async Task ShowContextMenuAsync(string filePath, int screenX, int screenY, UIElement? targetElement = null)
        {
            Log($"ShowContextMenuAsync: Starting for '{filePath}' at ({screenX}, {screenY})");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                Log("ShowContextMenuAsync: File path is null or empty");
                return;
            }

            try
            {
                // Normalize WSL paths first
                string normalizedPath;
                try
                {
                    normalizedPath = NormalizeWslPath(filePath);
                    Log($"ShowContextMenuAsync: Normalized path to '{normalizedPath}'");
                }
                catch (NotSupportedException ex)
                {
                    Log($"ShowContextMenuAsync: Unsupported path format (WSL1 or other): '{filePath}'. Error: {ex.Message}");
                    // Silently fail - don't show error to user, just log it
                    return;
                }
                catch (Exception ex)
                {
                    Log($"ShowContextMenuAsync: Failed to normalize path '{filePath}': {ex.Message}");
                    // Silently fail - don't show error to user, just log it
                    return;
                }

                // Check if the file exists
                if (!File.Exists(normalizedPath) && !Directory.Exists(normalizedPath))
                {
                    Log($"ShowContextMenuAsync: File or directory does not exist: {normalizedPath}");
                    _notificationService.ShowError("File Not Found", $"The file or directory '{normalizedPath}' could not be found.");
                    return;
                }

                try
                {
                    // Get folder and file using WinRT Storage APIs
                    var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(normalizedPath) ?? normalizedPath);
                    IStorageItem item;

                    // Check if it's a file or folder
                    if (File.Exists(normalizedPath))
                    {
                        item = await folder.GetFileAsync(Path.GetFileName(normalizedPath));
                    }
                    else if (Directory.Exists(normalizedPath))
                    {
                        item = await StorageFolder.GetFolderFromPathAsync(normalizedPath);
                    }
                    else
                    {
                        Log($"ShowContextMenuAsync: Path is neither file nor directory: {normalizedPath}");
                        ShowCustomMenu(normalizedPath, screenX, screenY);
                        return;
                    }

                    // Get XamlRoot from target element or current window
                    XamlRoot? xamlRoot = null;
                    FrameworkElement? rootElement = null;
                    
                    // First try to get from the provided element
                    if (targetElement is FrameworkElement element)
                    {
                        xamlRoot = element.XamlRoot;
                        rootElement = element;
                    }
                    
                    // If not available, try to get from Window.Current
                    if (xamlRoot == null)
                    {
                        var window = Microsoft.UI.Xaml.Window.Current;
                        if (window?.Content is FrameworkElement windowRoot)
                        {
                            xamlRoot = windowRoot.XamlRoot;
                            rootElement = windowRoot;
                        }
                    }
                    
                    // If still not available, try to find XamlRoot by traversing the visual tree from targetElement
                    if (xamlRoot == null && targetElement != null)
                    {
                        var current = targetElement;
                        while (current != null && xamlRoot == null)
                        {
                            if (current is FrameworkElement fe && fe.XamlRoot != null)
                            {
                                xamlRoot = fe.XamlRoot;
                                rootElement = fe;
                                break;
                            }
                            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current) as UIElement;
                        }
                    }

                    if (xamlRoot == null)
                    {
                        Log("ShowContextMenuAsync: Could not get XamlRoot from target element or current window");
                        ShowCustomMenu(normalizedPath, screenX, screenY, targetElement);
                        return;
                    }

                    // Create context menu using MenuFlyout
                    var contextMenu = new MenuFlyout();
                    contextMenu.LightDismissOverlayMode = LightDismissOverlayMode.Off;
                    contextMenu.XamlRoot = xamlRoot;

                    // Add system context menu items
                    // Note: WinRT doesn't have a direct CreateContextMenuAsync, so we add common items manually
                    var openItem = new MenuFlyoutItem { Text = "Open" };
                    openItem.Click += (s, e) => OpenFile(normalizedPath);
                    contextMenu.Items.Add(openItem);

                    var openWithItem = new MenuFlyoutItem { Text = "Open with..." };
                    openWithItem.Click += (s, e) => OpenFileWith(normalizedPath);
                    contextMenu.Items.Add(openWithItem);

                    contextMenu.Items.Add(new MenuFlyoutSeparator());

                    var copyPathItem = new MenuFlyoutItem { Text = "Copy path" };
                    copyPathItem.Click += (s, e) => CopyPath(normalizedPath);
                    contextMenu.Items.Add(copyPathItem);

                    var copyItem = new MenuFlyoutItem { Text = "Copy" };
                    copyItem.Click += (s, e) => CopyFile(normalizedPath);
                    contextMenu.Items.Add(copyItem);

                    contextMenu.Items.Add(new MenuFlyoutSeparator());

                    var renameItem = new MenuFlyoutItem { Text = "Rename" };
                    renameItem.Click += (s, e) => RenameFile(normalizedPath);
                    contextMenu.Items.Add(renameItem);

                    var deleteItem = new MenuFlyoutItem { Text = "Delete" };
                    deleteItem.Click += (s, e) => DeleteFile(normalizedPath);
                    contextMenu.Items.Add(deleteItem);

                    contextMenu.Items.Add(new MenuFlyoutSeparator());

                    var propertiesItem = new MenuFlyoutItem { Text = "Properties" };
                    propertiesItem.Click += (s, e) => ShowProperties(normalizedPath);
                    contextMenu.Items.Add(propertiesItem);

                    // Show at exact click position (screen coordinates)
                    // With XamlRoot set, we can use null and screen coordinates
                    contextMenu.ShowAt(null, new Point(screenX, screenY));
                    Log($"ShowContextMenuAsync: Context menu displayed successfully at ({screenX}, {screenY})");
                }
                catch (Exception ex)
                {
                    Log($"ShowContextMenuAsync: Failed to create WinRT context menu: {ex.Message}");
                    Log($"ShowContextMenuAsync: StackTrace: {ex.StackTrace}");
                    // Graceful fallback - show custom menu
                    ShowCustomMenu(normalizedPath, screenX, screenY, targetElement);
                }
            }
            catch (Exception ex)
            {
                Log($"ShowContextMenuAsync ERROR: {ex}");
                Log($"ShowContextMenuAsync ERROR StackTrace: {ex.StackTrace}");

                // For WSL paths or other errors, show custom menu as fallback
                if (filePath.StartsWith("\\\\wsl", StringComparison.OrdinalIgnoreCase) ||
                    filePath.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"ShowContextMenuAsync: Showing custom menu for potentially unsupported path format: '{filePath}'");
                    ShowCustomMenu(filePath, screenX, screenY, targetElement);
                    return;
                }

                // For other errors, show notification to user
                _notificationService.ShowError("Context Menu Error", $"Failed to show context menu: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Shows a custom fallback menu when WinRT APIs fail.
        /// </summary>
        private void ShowCustomMenu(string filePath, int screenX, int screenY, UIElement? targetElement = null)
        {
            try
            {
                // Get XamlRoot from target element or current window
                XamlRoot? xamlRoot = null;
                FrameworkElement? rootElement = null;
                
                // First try to get from the provided element
                if (targetElement is FrameworkElement element)
                {
                    xamlRoot = element.XamlRoot;
                    rootElement = element;
                }
                
                // If not available, try to get from Window.Current
                if (xamlRoot == null)
                {
                    var window = Microsoft.UI.Xaml.Window.Current;
                    if (window?.Content is FrameworkElement windowRoot)
                    {
                        xamlRoot = windowRoot.XamlRoot;
                        rootElement = windowRoot;
                    }
                }
                
                // If still not available, try to find XamlRoot by traversing the visual tree
                if (xamlRoot == null && targetElement != null)
                {
                    var current = targetElement;
                    while (current != null && xamlRoot == null)
                    {
                        if (current is FrameworkElement fe && fe.XamlRoot != null)
                        {
                            xamlRoot = fe.XamlRoot;
                            rootElement = fe;
                            break;
                        }
                        current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current) as UIElement;
                    }
                }

                if (xamlRoot == null)
                {
                    Log("ShowCustomMenu: Could not get XamlRoot, cannot show menu");
                    _notificationService.ShowError("Menu Error", "Failed to show context menu.");
                    return;
                }

                var menu = new MenuFlyout();
                menu.LightDismissOverlayMode = LightDismissOverlayMode.Off;
                menu.XamlRoot = xamlRoot;

                var openItem = new MenuFlyoutItem { Text = "Open", Tag = filePath };
                openItem.Click += (s, e) => OpenFile(filePath);
                menu.Items.Add(openItem);

                var copyPathItem = new MenuFlyoutItem { Text = "Copy Path", Tag = filePath };
                copyPathItem.Click += (s, e) => CopyPath(filePath);
                menu.Items.Add(copyPathItem);

                // Show menu using screen coordinates (XamlRoot is already set)
                menu.ShowAt(null, new Point(screenX, screenY));
                Log($"ShowCustomMenu: Custom menu displayed for '{filePath}'");
            }
            catch (Exception ex)
            {
                Log($"ShowCustomMenu ERROR: {ex.Message}");
                _notificationService.ShowError("Menu Error", "Failed to show context menu.");
            }
        }

        /// <summary>
        /// Opens a file using the default application.
        /// </summary>
        private void OpenFile(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
                Log($"OpenFile: Opened '{filePath}'");
            }
            catch (Exception ex)
            {
                Log($"OpenFile ERROR: {ex.Message}");
                _notificationService.ShowError("Open Error", $"Failed to open file: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens a file with the "Open with" dialog.
        /// </summary>
        private void OpenFileWith(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32.dll,OpenAs_RunDLL {filePath}",
                    UseShellExecute = true
                });
                Log($"OpenFileWith: Opened 'Open with' dialog for '{filePath}'");
            }
            catch (Exception ex)
            {
                Log($"OpenFileWith ERROR: {ex.Message}");
                _notificationService.ShowError("Open Error", $"Failed to open 'Open with' dialog: {ex.Message}");
            }
        }

        /// <summary>
        /// Copies the file path to clipboard.
        /// </summary>
        private void CopyPath(string filePath)
        {
            try
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(filePath);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                Log($"CopyPath: Copied path '{filePath}' to clipboard");
            }
            catch (Exception ex)
            {
                Log($"CopyPath ERROR: {ex.Message}");
                _notificationService.ShowError("Copy Error", $"Failed to copy path: {ex.Message}");
            }
        }

        /// <summary>
        /// Copies the file to clipboard.
        /// </summary>
        private void CopyFile(string filePath)
        {
            try
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                if (File.Exists(filePath))
                {
                    var file = StorageFile.GetFileFromPathAsync(filePath).GetAwaiter().GetResult();
                    dataPackage.SetStorageItems(new[] { file });
                }
                else if (Directory.Exists(filePath))
                {
                    var folder = StorageFolder.GetFolderFromPathAsync(filePath).GetAwaiter().GetResult();
                    dataPackage.SetStorageItems(new[] { folder });
                }
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                Log($"CopyFile: Copied file '{filePath}' to clipboard");
            }
            catch (Exception ex)
            {
                Log($"CopyFile ERROR: {ex.Message}");
                _notificationService.ShowError("Copy Error", $"Failed to copy file: {ex.Message}");
            }
        }

        /// <summary>
        /// Renames a file.
        /// </summary>
        private void RenameFile(string filePath)
        {
            try
            {
                // Open File Explorer and select the file for renaming
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
                Log($"RenameFile: Opened File Explorer for '{filePath}'");
            }
            catch (Exception ex)
            {
                Log($"RenameFile ERROR: {ex.Message}");
                _notificationService.ShowError("Rename Error", $"Failed to open File Explorer: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a file.
        /// </summary>
        private void DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Log($"DeleteFile: Deleted file '{filePath}'");
                }
                else if (Directory.Exists(filePath))
                {
                    Directory.Delete(filePath, true);
                    Log($"DeleteFile: Deleted directory '{filePath}'");
                }
            }
            catch (Exception ex)
            {
                Log($"DeleteFile ERROR: {ex.Message}");
                _notificationService.ShowError("Delete Error", $"Failed to delete: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows file properties.
        /// </summary>
        private void ShowProperties(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
                // Note: To show properties directly, we'd need to use shell32.dll,ShellExec_RunDLL
                // For now, just open File Explorer with the file selected
                Log($"ShowProperties: Opened File Explorer for '{filePath}'");
            }
            catch (Exception ex)
            {
                Log($"ShowProperties ERROR: {ex.Message}");
                _notificationService.ShowError("Properties Error", $"Failed to show properties: {ex.Message}");
            }
        }

        /// <summary>
        /// Normalizes WSL paths to Windows format.
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>The normalized path</returns>
        /// <exception cref="NotSupportedException">Thrown for WSL1 paths or other unsupported formats</exception>
        private string NormalizeWslPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            string normalizedPath = path.Replace('/', '\\').TrimEnd('\\');

            // 1. Modern UNC → Legacy UNC (preserves distro name)
            if (normalizedPath.StartsWith(@"\\wsl.localhost\", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedPath.Replace(@"\\wsl.localhost\", @"\\wsl$\", StringComparison.OrdinalIgnoreCase);
            }

            // 2. Already \\wsl$\ - good
            if (normalizedPath.StartsWith(@"\\wsl$\", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedPath;
            }

            // 3. WSL1 - unsupported
            if (normalizedPath.Contains(@"\AppData\Local\lxss\", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("WSL1 paths unsupported");
            }

            // 4. Raw Linux path → Extract distro + convert
            // Check original path format (preserve / for Linux paths)
            string originalPath = path.TrimEnd('/');
            if (originalPath.StartsWith("/") && (originalPath.Contains("/home/") || originalPath.Contains("/mnt/")))
            {
                return ConvertLinuxPathToWsl(originalPath);
            }

            // 5. Check if it's a backslash Linux path (already normalized)
            if (normalizedPath.StartsWith('\\') && !normalizedPath.StartsWith(@"\\") &&
                (normalizedPath.Contains("\\home\\") || normalizedPath.Contains("\\mnt\\")))
            {
                // Convert back to forward slash for wslpath command
                return ConvertLinuxPathToWsl(normalizedPath.Replace('\\', '/'));
            }

            // Not a WSL path, return normalized (Windows path)
            return normalizedPath;
        }

        /// <summary>
        /// Converts a Linux path to WSL \\wsl$\ format using wslpath command or fallback.
        /// </summary>
        /// <param name="linuxPath">The Linux path to convert</param>
        /// <returns>The converted path in \\wsl$\ format</returns>
        private string ConvertLinuxPathToWsl(string linuxPath)
        {
            try
            {
                // Run `wsl wslpath -w` to get Windows path with correct distro
                // wslpath is a Linux command, so it must be run through wsl.exe
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "wsl",
                        Arguments = $"wslpath -w \"{linuxPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string result = process.StandardOutput.ReadToEnd().Trim();
                string error = process.StandardError.ReadToEnd().Trim();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(result))
                {
                    // Ensure it's in \\wsl$\ format
                    if (result.StartsWith(@"\\wsl$\", StringComparison.OrdinalIgnoreCase))
                    {
                        return result;
                    }
                    // If wslpath returned a different format, try to convert it
                    if (result.StartsWith(@"\\wsl.localhost\", StringComparison.OrdinalIgnoreCase))
                    {
                        return result.Replace(@"\\wsl.localhost\", @"\\wsl$\", StringComparison.OrdinalIgnoreCase);
                    }
                }

                Log($"ConvertLinuxPathToWsl: wslpath command failed or returned unexpected format. Exit code: {process.ExitCode}, Output: {result}, Error: {error}");
            }
            catch (Exception ex)
            {
                Log($"ConvertLinuxPathToWsl: Exception running wsl wslpath command: {ex.Message}");
            }

            // Fallback: assume common distro (user can override)
            // Normalize the Linux path (forward slashes to backslashes, remove leading slash)
            string normalized = linuxPath.Replace('/', '\\').TrimStart('\\');

            // Default to Ubuntu-24.04 if we can't determine
            string defaultDistribution = "Ubuntu-24.04";
            return $@"\\wsl$\{defaultDistribution}\{normalized}";
        }

        /// <summary>
        /// Logs a message to the Grex log file.
        /// </summary>
        private static void Log(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(LogFilePath, $"[{timestamp}] ContextMenuService: {message}\n");
            }
            catch
            {
                // Ignore logging errors to prevent infinite loops
            }
        }

        #endregion
    }
}
