using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;
using Grex.ViewModels;
using Grex.Controls;
using Grex.Services;

namespace Grex
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }
        private AppWindow? _appWindow;
        private bool _isReloadingTabs = false;
        private readonly object _reloadTabsLock = new object();
        private System.Threading.CancellationTokenSource? _refreshLocalizationCancellation;
        private readonly object _refreshLocalizationLock = new object();
        
        // Constants for base window size (at 100% DPI scale)
        private const int baseWidth = 1100;
        private const int baseHeight = 700;
        
        // Calculate scaled window size based on DPI
        // Handles all Windows scaling options: 100%, 125%, 150%, 175%, 200%, 225%, 250%, 300%, 350%, etc.
        // RasterizationScale returns a continuous value (1.0 = 100%, 1.5 = 150%, 3.0 = 300%, 3.5 = 350%, etc.)
        private (int width, int height) GetScaledWindowSize()
        {
            try
            {
                double scaleFactor = 1.0;
                
                // Get DPI scale factor from XamlRoot (most reliable for WinUI 3)
                // This automatically handles all Windows scaling percentages
                if (RootGrid?.XamlRoot != null)
                {
                    scaleFactor = RootGrid.XamlRoot.RasterizationScale;
                }
                else if (_appWindow != null)
                {
                    // Fallback: Use Win32 DPI API if XamlRoot is not available yet
                    try
                    {
                        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                        var dpi = GetDpiForWindow(hwnd);
                        scaleFactor = dpi / 96.0; // 96 DPI = 100% scale, 144 DPI = 150% scale, etc.
                    }
                    catch
                    {
                        // Use default 1.0 if DPI detection fails
                    }
                }
                
                // Ensure scale factor is reasonable (between 0.5 and 5.0 to handle edge cases)
                scaleFactor = Math.Max(0.5, Math.Min(5.0, scaleFactor));
                
                // Calculate scaled dimensions
                var scaledWidth = (int)(baseWidth * scaleFactor);
                var scaledHeight = (int)(baseHeight * scaleFactor);
                
                // Ensure minimum reasonable size (even at very low scales)
                scaledWidth = Math.Max(scaledWidth, 800);
                scaledHeight = Math.Max(scaledHeight, 600);
                
                Log($"GetScaledWindowSize: DPI scale factor = {scaleFactor} ({scaleFactor * 100:F0}%), scaled size = {scaledWidth}x{scaledHeight}");
                
                return (scaledWidth, scaledHeight);
            }
            catch (Exception ex)
            {
                Log($"GetScaledWindowSize ERROR: {ex}, using base size");
                return (baseWidth, baseHeight);
            }
        }
        
        // Win32 API to get DPI for a window (fallback method)
        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);

        // Win32 API for window positioning
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private static readonly IntPtr HWND_TOP = new IntPtr(0);

        public MainWindow()
        {
            try
            {
                Log("MainWindow constructor: Starting");
                this.InitializeComponent();
                Log("MainWindow constructor: InitializeComponent completed");
                
                // Set title after InitializeComponent - use hardcoded title to avoid any resource lookup issues
                // Localization can be applied later if needed, but not during window construction
                this.Title = "Grex - Tabbed File Search";
                Log("MainWindow constructor: Title set");
                
                ViewModel = new MainViewModel();
                Log("MainWindow constructor: ViewModel created");
                
                // Setup window with system backdrop and title bar
                SetupWindow();
                Log("MainWindow constructor: SetupWindow completed");
                
                // Apply theme and backdrop preference from settings
                var preference = SettingsService.GetThemePreference();
                ApplyThemeAndBackdrop(preference);
                UpdateTitleBarButtonColors(preference);
                Log("MainWindow constructor: Theme and backdrop applied from settings");
                
                // Set initial navigation selection to Search
                if (MainNavigationView != null)
                {
                    MainNavigationView.SelectedItem = SearchNavItem;
                    
                    // Wire up NavigationView pane toggle to control SplitView
                    // This makes the hamburger menu button work
                    MainNavigationView.PaneOpening += (s, e) => MainSplitView.IsPaneOpen = true;
                    MainNavigationView.PaneClosing += (s, e) => MainSplitView.IsPaneOpen = false;
                    
                    // Ensure pane is closed after NavigationView loads
                    MainNavigationView.Loaded += (s, e) =>
                    {
                        if (MainSplitView != null)
                        {
                            MainSplitView.IsPaneOpen = false;
                        }
                        
                    };
                }
                
                // Ensure SplitView starts in compact mode (pane closed)
                if (MainSplitView != null)
                {
                    MainSplitView.IsPaneOpen = false;
                    
                    // Also ensure it stays closed after SplitView loads
                    MainSplitView.Loaded += (s, e) =>
                    {
                        MainSplitView.IsPaneOpen = false;
                    };
                }

                LocalizationService.Instance.PropertyChanged += LocalizationService_PropertyChanged;
                this.Closed += MainWindow_Closed;
                
                // Add keyboard handler for F1 to open About page
                RootGrid.KeyDown += RootGrid_KeyDown;
                
                // Set localized title after a delay to avoid resource loading issues
                // Use a very defensive approach with comprehensive error handling
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1000); // Wait longer for everything to be ready
                        
                        // Use dispatcher to update UI on UI thread
                        if (this.DispatcherQueue != null)
                        {
                            this.DispatcherQueue.TryEnqueue(() =>
                            {
                                try
                                {
                                    Log("MainWindow: Attempting to set localized title");
                                    
                                    var localizedTitle = Services.LocalizationService.Instance.GetLocalizedString("MainWindow.Title");
                                    
                                    if (!string.IsNullOrEmpty(localizedTitle) && localizedTitle != "MainWindow.Title")
                                    {
                                        this.Title = localizedTitle;
                                        Log($"MainWindow: Title set to localized version: {localizedTitle}");
                                    }
                                    else
                                    {
                                        Log("MainWindow: Localized title not available, keeping hardcoded title");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log($"MainWindow: Exception setting localized title (non-fatal): {ex.GetType().Name}: {ex.Message}");
                                    // Keep hardcoded title
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"MainWindow: Exception in title localization task (non-fatal): {ex.GetType().Name}: {ex.Message}");
                    }
                });
                
                // Don't initialize tabs here - wait for TabView to load
                // InitializeTabs will be called from MainTabView_Loaded
                Log("MainWindow constructor: SUCCESS - waiting for TabView to load");
            }
            catch (Exception ex)
            {
                Log($"MainWindow constructor ERROR: {ex}");
                Log($"MainWindow constructor ERROR StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Log($"MainWindow constructor ERROR InnerException: {ex.InnerException}");
                }
                throw;
            }
        }

        private void MainTabView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("MainTabView_Loaded: Starting");
                // Initialize tabs after TabView is fully loaded
                InitializeTabs();
                
                
                // Set drag region after TabView is loaded and layout is calculated
                if (_appWindow != null && AppWindowTitleBar.IsCustomizationSupported())
                {
                    // Use dispatcher to ensure layout is complete
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        SetDragRegion();
                    });
                }
                
                // Ensure localization is applied after tabs are initialized
                RefreshLocalization();
                
                Log("MainTabView_Loaded: Completed");
            }
            catch (Exception ex)
            {
                Log($"MainTabView_Loaded ERROR: {ex}");
                Log($"MainTabView_Loaded ERROR StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Log($"MainTabView_Loaded ERROR InnerException: {ex.InnerException}");
                }
            }
        }

        private static void Log(string message)
        {
            try
            {
                var logFile = Path.Combine(Path.GetTempPath(), "Grex.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(logFile, $"[{timestamp}] {message}\n");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        private void SetupWindow()
        {
            try
            {
                Log("SetupWindow: Starting");
                // Get the window handle and AppWindow
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                Log("SetupWindow: Got window handle");
                var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                _appWindow = AppWindow.GetFromWindowId(windowId);
                Log("SetupWindow: Got AppWindow");
                
                // Set the application icon for the taskbar
                try
                {
                    var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Grex.ico");
                    if (File.Exists(iconPath))
                    {
                        _appWindow.SetIcon(iconPath);
                        Log($"SetupWindow: Icon set successfully from {iconPath}");
                    }
                    else
                    {
                        Log($"SetupWindow: Icon file not found at {iconPath}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"SetupWindow: Failed to set icon: {ex}");
                }
                
                // Get screen dimensions using Win32 API
                var screenWidth = GetSystemMetrics(0); // SM_CXSCREEN
                var screenHeight = GetSystemMetrics(1); // SM_CYSCREEN
                
                // Get scaled default window size based on DPI
                var (defaultWidth, defaultHeight) = GetScaledWindowSize();
                var minWidth = defaultWidth;
                var minHeight = defaultHeight;
                
                // Restore window position and size from settings
                var (savedX, savedY, savedWidth, savedHeight) = SettingsService.GetWindowPosition();
                if (savedX.HasValue && savedY.HasValue && savedWidth.HasValue && savedHeight.HasValue)
                {
                    // Validate saved position is reasonable (not off-screen)
                    
                    if (savedX.Value >= -savedWidth.Value && savedX.Value < screenWidth &&
                        savedY.Value >= -savedHeight.Value && savedY.Value < screenHeight &&
                        savedWidth.Value >= minWidth && savedHeight.Value >= minHeight)
                    {
                        // Restore position and size
                        SetWindowPos(hwnd, HWND_TOP, savedX.Value, savedY.Value, savedWidth.Value, savedHeight.Value, 0);
                        Log($"SetupWindow: Restored window position ({savedX.Value}, {savedY.Value}) and size ({savedWidth.Value}, {savedHeight.Value})");
                    }
                    else
                    {
                        // Invalid saved position, use scaled defaults
                        _appWindow.Resize(new Windows.Graphics.SizeInt32(defaultWidth, defaultHeight));
                        Log($"SetupWindow: Invalid saved position, using scaled default size ({defaultWidth}, {defaultHeight})");
                    }
                }
                else
                {
                    // No saved position, center window with scaled default size
                    var centerX = (screenWidth - defaultWidth) / 2;
                    var centerY = (screenHeight - defaultHeight) / 2;
                    SetWindowPos(hwnd, HWND_TOP, centerX, centerY, defaultWidth, defaultHeight, 0);
                    Log($"SetupWindow: No saved position, centered window at ({centerX}, {centerY}) with scaled size ({defaultWidth}, {defaultHeight})");
                }
                
                // Handle window size changes for responsive design and minimum size enforcement
                _appWindow.Changed += AppWindow_Changed;
                UpdateResponsiveLayout();
                Log("SetupWindow: Responsive layout updated");
                
                // Customize title bar to extend content into it
                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    Log("SetupWindow: Configuring title bar");
                    var titleBar = _appWindow.TitleBar;
                    
                    // Extend content into title bar so tabs appear at the top
                    titleBar.ExtendsContentIntoTitleBar = true;
                    
                    // Make title bar transparent so our content shows through
                    titleBar.BackgroundColor = Colors.Transparent;
                    titleBar.InactiveBackgroundColor = Colors.Transparent;
                    
                    // Button colors will be set by UpdateTitleBarButtonColors based on theme
                    
                // Set the custom drag region after the window is loaded
                this.Activated += MainWindow_Activated;
                
                // Update drag region when XamlRoot changes (e.g., DPI changes)
                // Note: We'll update drag region in other events (Activated, AppWindow_Changed, MainTabView_Loaded)
                // This handler can be added later if needed when XamlRoot is available
                    
                    Log("SetupWindow: Title bar configured");
                }
                
                // Backdrop will be set by ApplyThemeAndBackdrop based on theme preference
                Log("SetupWindow: Completed");
            }
            catch (Exception ex)
            {
                Log($"SetupWindow ERROR: {ex}");
                throw;
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                SaveWindowPosition();
            }
            catch (Exception ex)
            {
                Log($"MainWindow_Closed ERROR: {ex}");
            }
            finally
            {
                LocalizationService.Instance.PropertyChanged -= LocalizationService_PropertyChanged;
                this.Closed -= MainWindow_Closed;
            }
        }

        /// <summary>
        /// Saves the current window position and size to settings.
        /// Call this before restarting the application to preserve window state.
        /// </summary>
        public void SaveWindowPosition()
        {
            try
            {
                if (_appWindow != null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    if (GetWindowRect(hwnd, out RECT rect))
                    {
                        var x = rect.Left;
                        var y = rect.Top;
                        var width = rect.Right - rect.Left;
                        var height = rect.Bottom - rect.Top;
                        
                        SettingsService.SetWindowPosition(x, y, width, height);
                        Log($"SaveWindowPosition: Saved window position ({x}, {y}) and size ({width}, {height})");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"SaveWindowPosition ERROR: {ex}");
            }
        }

        private void InitializeTabs()
        {
            try
            {
                Log($"InitializeTabs: Starting, Tabs count: {ViewModel.Tabs.Count}");
                
                if (MainTabView == null)
                {
                    Log("InitializeTabs ERROR: MainTabView is null!");
                    return;
                }
                Log("InitializeTabs: MainTabView is available");
                
                // Create tabs programmatically
                foreach (var tab in ViewModel.Tabs)
                {
                    try
                    {
                        Log($"InitializeTabs: Adding tab: {tab.TabTitle}");
                        AddTabToView(tab);
                        Log($"InitializeTabs: Successfully added tab: {tab.TabTitle}");
                    }
                    catch (Exception ex)
                    {
                        Log($"InitializeTabs ERROR adding tab {tab.TabTitle}: {ex}");
                        Log($"InitializeTabs ERROR StackTrace: {ex.StackTrace}");
                        throw;
                    }
                }
                
                Log($"InitializeTabs: Created {MainTabView.TabItems.Count} tab items");
                
                // Select initial tab - FIX: Select the TabViewItem, not the ViewModel
                if (MainTabView.TabItems.Count > 0)
                {
                    Log("InitializeTabs: About to select first tab");
                    var firstTabItem = MainTabView.TabItems[0] as TabViewItem;
                    if (firstTabItem != null)
                    {
                        Log("InitializeTabs: First tab item found, setting as selected");
                        MainTabView.SelectedItem = firstTabItem;
                        if (firstTabItem.Content is SearchTabContent content && content.ViewModel != null)
                        {
                            ViewModel.SelectedTab = content.ViewModel;
                            content.BindViewModel();
                            // Subscribe to the ViewModel StatusText changes
                            content.ViewModel.PropertyChanged += SelectedTab_PropertyChanged;
                            // Update InfoBar with current status
                            UpdateStatusInfoBar(content.ViewModel.StatusText);
                            Log("InitializeTabs: Bound ViewModel to content and updated InfoBar");
                        }
                        Log("InitializeTabs: Selected initial tab");
                    }
                    else
                    {
                        Log("InitializeTabs WARNING: First tab item is null");
                    }
                }
                else
                {
                    Log("InitializeTabs WARNING: No tab items created");
                }
                Log("InitializeTabs: Completed");
            }
            catch (Exception ex)
            {
                Log($"InitializeTabs ERROR: {ex}");
                Log($"InitializeTabs ERROR StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Log($"InitializeTabs ERROR InnerException: {ex.InnerException}");
                }
                throw;
            }
        }

        private void AddTabToView(TabViewModel tabViewModel)
        {
            try
            {
                Log($"AddTabToView: Creating tab for {tabViewModel.TabTitle}");
                
                SearchTabContent? searchContent = null;
                try
                {
                    searchContent = new SearchTabContent { ViewModel = tabViewModel };
                    Log("AddTabToView: SearchTabContent created");
                }
                catch (Exception ex)
                {
                    Log($"AddTabToView ERROR creating SearchTabContent: {ex}");
                    Log($"AddTabToView ERROR StackTrace: {ex.StackTrace}");
                    throw;
                }
                
                TabViewItem? tabItem = null;
                try
                {
                    Log("AddTabToView: About to create TabViewItem");
                    
                    // Set ViewModel AFTER creating SearchTabContent but BEFORE setting as Content
                    // This way DataContextChanged fires before the control is added to visual tree
                    Log("AddTabToView: Setting ViewModel on SearchTabContent");
                    searchContent.ViewModel = tabViewModel;
                    Log("AddTabToView: ViewModel set on SearchTabContent");
                    
                    tabItem = new TabViewItem();
                    Log("AddTabToView: TabViewItem instance created");
                    
                    // Add cursor handlers for tab close button
                    tabItem.PointerEntered += TabItem_PointerEntered;
                    tabItem.PointerExited += TabItem_PointerExited;
                    
                    // Create a TextBlock for the header with proper vertical alignment
                    var headerTextBlock = new TextBlock
                    {
                        Text = tabViewModel.TabTitle,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    tabItem.Header = headerTextBlock;
                    Log("AddTabToView: TabViewItem Header set");
                    
                    // Ensure content stretches to fill the TabViewItem
                    tabItem.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    tabItem.VerticalContentAlignment = VerticalAlignment.Stretch;
                    
                    tabItem.Content = searchContent;
                    Log("AddTabToView: TabViewItem Content set");
                    Log("AddTabToView: TabViewItem fully created");
                }
                catch (Exception ex)
                {
                    Log($"AddTabToView ERROR creating TabViewItem: {ex}");
                    Log($"AddTabToView ERROR StackTrace: {ex.StackTrace}");
                    throw;
                }
                
                try
                {
                    Log("AddTabToView: About to subscribe to PropertyChanged");
                    // Subscribe to title changes
                    tabViewModel.PropertyChanged += (s, e) =>
                    {
                        try
                        {
                            if (e.PropertyName == nameof(TabViewModel.TabTitle))
                            {
                                if (tabItem.Header is TextBlock headerTextBlock)
                                {
                                    headerTextBlock.Text = tabViewModel.TabTitle;
                                }
                                else
                                {
                                    // Create a TextBlock for the header with proper vertical alignment
                                    var newHeaderTextBlock = new TextBlock
                                    {
                                        Text = tabViewModel.TabTitle,
                                        VerticalAlignment = VerticalAlignment.Center,
                                        Margin = new Thickness(0, 2, 0, 0)
                                    };
                                    tabItem.Header = newHeaderTextBlock;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"AddTabToView PropertyChanged handler ERROR: {ex}");
                        }
                    };
                    Log("AddTabToView: PropertyChanged subscribed");
                    
                }
                catch (Exception ex)
                {
                    Log($"AddTabToView ERROR subscribing to PropertyChanged: {ex}");
                    // Don't throw - this is not critical
                }
                
                try
                {
                    Log($"AddTabToView: About to add tab to MainTabView. Current count: {MainTabView.TabItems.Count}");
                    Log($"AddTabToView: MainTabView.IsLoaded: {MainTabView.IsLoaded}");
                    MainTabView.TabItems.Add(tabItem);
                    Log($"AddTabToView: Tab added to TabView, total tabs: {MainTabView.TabItems.Count}");
                }
                catch (Exception ex)
                {
                    Log($"AddTabToView ERROR adding to TabItems: {ex}");
                    Log($"AddTabToView ERROR StackTrace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Log($"AddTabToView ERROR InnerException: {ex.InnerException}");
                    }
                    NotificationService.Instance.ShowError(
                        GetString("TabCreationErrorTitle"),
                        GetString("TabCreationErrorMessage", ex.Message));
                    throw;
                }
            }
            catch (Exception ex)
            {
                Log($"AddTabToView FATAL ERROR: {ex}");
                Log($"AddTabToView FATAL ERROR StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Log($"AddTabToView FATAL ERROR InnerException: {ex.InnerException}");
                    Log($"AddTabToView FATAL ERROR InnerException StackTrace: {ex.InnerException.StackTrace}");
                }
                NotificationService.Instance.ShowError(
                    GetString("TabCreationErrorTitle"),
                    GetString("TabCreationCriticalMessage", ex.Message));
                // Don't rethrow - try to continue
                Log("AddTabToView: Continuing despite error");
            }
        }

        private void TabView_AddTabButtonClick(TabView sender, object args)
        {
            ViewModel.AddTab();
            var newTab = ViewModel.SelectedTab;
            if (newTab != null)
            {
                AddTabToView(newTab);
                MainTabView.SelectedItem = MainTabView.TabItems.LastOrDefault();
            }
        }

        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Tab is TabViewItem tabItem && tabItem.Content is SearchTabContent content && content.ViewModel is TabViewModel tabToClose)
            {
                // Ensure at least one tab remains
                if (ViewModel.Tabs.Count <= 1)
                {
                    return;
                }

                // Unsubscribe from ViewModel PropertyChanged if this is the selected tab
                if (ViewModel.SelectedTab == tabToClose)
                {
                    tabToClose.PropertyChanged -= SelectedTab_PropertyChanged;
                }

                // Unbind the tab content
                content.UnbindViewModel();
                
                // Remove the tab
                ViewModel.RemoveTab(tabToClose);
                MainTabView.TabItems.Remove(tabItem);
                
                // Update selected tab in TabView
                if (ViewModel.SelectedTab != null)
                {
                    // Find the TabViewItem for the selected tab
                    foreach (TabViewItem item in MainTabView.TabItems)
                    {
                        if (item.Content is SearchTabContent searchContent && searchContent.ViewModel == ViewModel.SelectedTab)
                        {
                            MainTabView.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }

        private void TabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabView.SelectedItem is TabViewItem selectedItem && selectedItem.Content is SearchTabContent content)
            {
                // Unsubscribe from previous tab's ViewModel if it exists
                if (ViewModel.SelectedTab != null)
                {
                    ViewModel.SelectedTab.PropertyChanged -= SelectedTab_PropertyChanged;
                }
                
                ViewModel.SelectedTab = content.ViewModel;
                if (content.ViewModel != null)
                {
                    content.BindViewModel();
                    // Subscribe to the new tab's ViewModel StatusText changes
                    content.ViewModel.PropertyChanged += SelectedTab_PropertyChanged;
                    // Update InfoBar with current status
                    UpdateStatusInfoBar(content.ViewModel.StatusText);
                }
            }
            else
            {
                // No tab selected, clear InfoBar
                UpdateStatusInfoBar("Ready");
            }
        }

        private void SelectedTab_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TabViewModel.StatusText) && sender is TabViewModel viewModel)
            {
                UpdateStatusInfoBar(viewModel.StatusText);
            }
        }

        private void UpdateStatusInfoBar(string statusText)
        {
            if (StatusInfoBar == null)
                return;

            StatusInfoBar.Message = statusText;

            if (string.IsNullOrWhiteSpace(statusText))
            {
                StatusInfoBar.Severity = InfoBarSeverity.Informational;
                return;
            }

            if (statusText.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
            {
                StatusInfoBar.Severity = InfoBarSeverity.Error;
            }
            else if (statusText.StartsWith("Found ", StringComparison.OrdinalIgnoreCase) ||
                     statusText.StartsWith("Replaced ", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(statusText, @"(?:Found|Replaced)\s+(\d+)\s+matches");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int matchCount))
                {
                    StatusInfoBar.Severity = matchCount < 0 ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
                }
                else
                {
                    StatusInfoBar.Severity = InfoBarSeverity.Informational;
                }
            }
            else
            {
                StatusInfoBar.Severity = InfoBarSeverity.Informational;
            }
        }

        private void NavigationItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Set cursor to hand using reflection to access ProtectedCursor
            if (sender is UIElement element)
            {
                try
                {
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && element != null)
                    {
                        var cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                        prop.SetValue(element, cursor);
                    }
                }
                catch
                {
                    // If reflection fails, do nothing
                }
            }
        }

        private void NavigationItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // Reset cursor to arrow
            if (sender is UIElement element)
            {
                try
                {
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && element != null)
                    {
                        var cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
                        prop.SetValue(element, cursor);
                    }
                }
                catch
                {
                    // If reflection fails, do nothing
                }
            }
        }

        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // F1 opens the About page
            if (e.Key == Windows.System.VirtualKey.F1)
            {
                NavigateToAbout();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Navigates to the About page
        /// </summary>
        public void NavigateToAbout()
        {
            if (MainNavigationView != null && AboutNavItem != null)
            {
                MainNavigationView.SelectedItem = AboutNavItem;
            }
        }

        private void TabView_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Set cursor to hand for TabView (add button area)
            if (sender is UIElement element)
            {
                try
                {
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && element != null)
                    {
                        var cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                        prop.SetValue(element, cursor);
                    }
                }
                catch
                {
                    // If reflection fails, do nothing
                }
            }
        }

        private void TabView_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // Reset cursor to arrow
            if (sender is UIElement element)
            {
                try
                {
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && element != null)
                    {
                        var cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
                        prop.SetValue(element, cursor);
                    }
                }
                catch
                {
                    // If reflection fails, do nothing
                }
            }
        }

        private void TabItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Set cursor to hand for TabViewItem (close button area)
            if (sender is UIElement element)
            {
                try
                {
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && element != null)
                    {
                        var cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                        prop.SetValue(element, cursor);
                    }
                }
                catch
                {
                    // If reflection fails, do nothing
                }
            }
        }

        private void TabItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // Reset cursor to arrow
            if (sender is UIElement element)
            {
                try
                {
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && element != null)
                    {
                        var cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
                        prop.SetValue(element, cursor);
                    }
                }
                catch
                {
                    // If reflection fails, do nothing
                }
            }
        }
        
        private static T? FindChildByType<T>(DependencyObject parent, Func<T, bool>? predicate = null) where T : FrameworkElement
        {
            if (parent == null) return null;
            
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T element && (predicate == null || predicate(element)))
                {
                    return element;
                }
                
                var result = FindChildByType<T>(child, predicate);
                if (result != null)
                {
                    return result;
                }
            }
            
            return null;
        }
        
        private static T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;
            
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T element && element.Name == name)
                {
                    return element;
                }
                
                var result = FindChildByName<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            
            return null;
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                if (tag == "Search")
                {
                    SearchContentGrid.Visibility = Visibility.Visible;
                    RegexBuilderContentGrid.Visibility = Visibility.Collapsed;
                    SettingsContentGrid.Visibility = Visibility.Collapsed;
                    AboutContentGrid.Visibility = Visibility.Collapsed;
                    // Show InfoBar when on Search page
                    if (StatusInfoBar != null)
                    {
                        StatusInfoBar.Visibility = Visibility.Visible;
                        // Update InfoBar with current tab's status if available
                        if (ViewModel.SelectedTab != null)
                        {
                            UpdateStatusInfoBar(ViewModel.SelectedTab.StatusText);
                        }
                    }
                }
                else if (tag == "RegexBuilder")
                {
                    SearchContentGrid.Visibility = Visibility.Collapsed;
                    RegexBuilderContentGrid.Visibility = Visibility.Visible;
                    SettingsContentGrid.Visibility = Visibility.Collapsed;
                    AboutContentGrid.Visibility = Visibility.Collapsed;
                    // Hide InfoBar when on Regex Builder page
                    if (StatusInfoBar != null)
                    {
                        StatusInfoBar.Visibility = Visibility.Collapsed;
                    }
                }
                else if (tag == "Settings")
                {
                    SearchContentGrid.Visibility = Visibility.Collapsed;
                    RegexBuilderContentGrid.Visibility = Visibility.Collapsed;
                    SettingsContentGrid.Visibility = Visibility.Visible;
                    AboutContentGrid.Visibility = Visibility.Collapsed;
                    // Hide InfoBar when on Settings page
                    if (StatusInfoBar != null)
                    {
                        StatusInfoBar.Visibility = Visibility.Collapsed;
                    }
                }
                else if (tag == "About")
                {
                    SearchContentGrid.Visibility = Visibility.Collapsed;
                    RegexBuilderContentGrid.Visibility = Visibility.Collapsed;
                    SettingsContentGrid.Visibility = Visibility.Collapsed;
                    AboutContentGrid.Visibility = Visibility.Visible;
                    // Hide InfoBar when on About page
                    if (StatusInfoBar != null)
                    {
                        StatusInfoBar.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        public void NavigateToSettings()
        {
            // Find the Settings item in the NavigationView
            if (MainNavigationView != null)
            {
                // Check primary menu items
                foreach (var item in MainNavigationView.MenuItems)
                {
                    if (item is NavigationViewItem navItem && navItem.Tag is string tag && tag == "Settings")
                    {
                        MainNavigationView.SelectedItem = navItem;
                        return;
                    }
                }
                
                // Check footer menu items (Settings might be there)
                foreach (var item in MainNavigationView.FooterMenuItems)
                {
                    if (item is NavigationViewItem navItem && navItem.Tag is string tag && tag == "Settings")
                    {
                        MainNavigationView.SelectedItem = navItem;
                        return;
                    }
                }
            }
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (_appWindow != null && AppWindowTitleBar.IsCustomizationSupported())
            {
                // Set the drag region after the window is activated and layout is calculated
                // Use dispatcher to ensure layout is complete
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    SetDragRegion();
                });
            }
        }
        

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidSizeChange)
            {
                // Enforce minimum window size as a fallback (Win32 API should handle this, but this ensures it)
                var currentSize = sender.Size;
                
                // Get current scaled minimum size
                var (scaledMinWidth, scaledMinHeight) = GetScaledWindowSize();
                
                if (currentSize.Width < scaledMinWidth || currentSize.Height < scaledMinHeight)
                {
                    var newWidth = Math.Max(currentSize.Width, scaledMinWidth);
                    var newHeight = Math.Max(currentSize.Height, scaledMinHeight);
                    sender.Resize(new Windows.Graphics.SizeInt32(newWidth, newHeight));
                }
                
                UpdateResponsiveLayout();
                // Update drag region when window size changes
                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    SetDragRegion();
                }
            }
        }

        private void SetDragRegion()
        {
            if (_appWindow == null || MainTabView == null)
                return;

            try
            {
                var titleBar = _appWindow.TitleBar;
                if (titleBar == null)
                    return;

                var scaleAdjustment = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
                
                // Get the tab strip height (typically around 48 pixels)
                var tabStripHeight = 48.0; // Standard TabView tab strip height
                
                // Get window width in logical pixels
                var windowWidth = _appWindow.Size.Width;
                
                // Get the insets (LeftInset is usually 0, RightInset is the system buttons width)
                var leftInset = titleBar.LeftInset;
                var rightInset = titleBar.RightInset;
                
                // Calculate drag region: from left inset to window width minus right inset
                // All values are in logical pixels, convert to physical pixels for SetDragRectangles
                var dragRegionX = (int)(leftInset * scaleAdjustment);
                var dragRegionWidth = (int)((windowWidth - leftInset - rightInset) * scaleAdjustment);
                var dragRegionHeight = (int)(tabStripHeight * scaleAdjustment);
                
                // Create drag rectangle covering the entire top bar area (excluding system buttons)
                var dragRect = new Windows.Graphics.RectInt32(
                    dragRegionX, // Start at left inset (usually 0)
                    0, // Start at top
                    dragRegionWidth, // Width to just before system buttons
                    dragRegionHeight // Height of tab strip
                );

                _appWindow.TitleBar.SetDragRectangles(new[] { dragRect });
            }
            catch (Exception ex)
            {
                Log($"SetDragRegion ERROR: {ex}");
            }
        }

        private void UpdateResponsiveLayout()
        {
            if (_appWindow == null)
                return;

            // Note: Padding is handled by SearchTabContent's internal Grid
            // We don't set TabView.Padding as it affects the tab strip alignment
            // The add button needs to be inline with tabs, so tab strip should have no padding
        }

        private static readonly SolidColorBrush DarkBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 24));
        private static readonly SolidColorBrush LightBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 245, 245, 245));
        
        // Gentle Gecko theme colors - Green/Black scheme (based on Black Knight)
        private static readonly SolidColorBrush GentleGeckoBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)); // #000000
        private static readonly Color GentleGeckoSecondaryColor = Color.FromArgb(255, 0, 51, 34); // #003322 Dark Green
        private static readonly Color GentleGeckoTertiaryColor = Color.FromArgb(255, 0, 89, 61); // #00593D Medium Green
        private static readonly Color GentleGeckoTextColor = Color.FromArgb(255, 255, 255, 255); // #FFFFFF
        private static readonly Color GentleGeckoAccentColor = Color.FromArgb(255, 0, 184, 107); // #00B86B Emerald Green
        
        // Black Knight theme colors - Modified to use Blue/Black scheme
        private static readonly SolidColorBrush BlackKnightBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)); // #000000
        private static readonly Color BlackKnightSecondaryColor = Color.FromArgb(255, 0, 51, 102); // #003366 Dark Blue
        private static readonly Color BlackKnightTertiaryColor = Color.FromArgb(255, 0, 71, 143); // #00478F Medium Blue
        private static readonly Color BlackKnightTextColor = Color.FromArgb(255, 255, 255, 255); // #FFFFFF
        private static readonly Color BlackKnightAccentColor = Color.FromArgb(255, 0, 120, 212); // #0078D4 Windows Blue
        
        // Paranoid theme colors - Purple/Lavender cyberpunk
        private static readonly SolidColorBrush ParanoidBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 29, 29, 78)); // #1D1D4E Port Gore
        private static readonly Color ParanoidSecondaryColor = Color.FromArgb(255, 63, 63, 136); // #3F3F88 Victoria
        private static readonly Color ParanoidTertiaryColor = Color.FromArgb(255, 95, 95, 191); // #5F5FBF Blue Violet
        private static readonly Color ParanoidTextColor = Color.FromArgb(255, 210, 210, 244); // #D2D2F4 Moon Raker
        private static readonly Color ParanoidAccentColor = Color.FromArgb(255, 154, 154, 224); // #9A9AE0 Dull Lavender
        
        // Diamond theme colors - Cool blue gradients
        private static readonly SolidColorBrush DiamondBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 45, 91, 103)); // #2D5B67 Casal
        private static readonly Color DiamondSecondaryColor = Color.FromArgb(255, 79, 127, 140); // #4F7F8C Smalt Blue
        private static readonly Color DiamondTertiaryColor = Color.FromArgb(255, 124, 162, 177); // #7CA2B1 Bali Hai
        private static readonly Color DiamondTextColor = Color.FromArgb(255, 185, 218, 233); // #B9DAE9 Spindle
        private static readonly Color DiamondAccentColor = Color.FromArgb(255, 165, 197, 213); // #A5C5D5 Pigeon Post
        
        // Subspace theme colors - Deep purples with soft lilacs
        private static readonly SolidColorBrush SubspaceBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 46, 26, 71)); // #2E1A47 Valhalla
        private static readonly Color SubspaceSecondaryColor = Color.FromArgb(255, 74, 42, 106); // #4A2A6A Jacarta
        private static readonly Color SubspaceTertiaryColor = Color.FromArgb(255, 121, 75, 139); // #794B8B Affair
        private static readonly Color SubspaceTextColor = Color.FromArgb(255, 226, 199, 230); // #E2C7E6 Snuff
        private static readonly Color SubspaceAccentColor = Color.FromArgb(255, 183, 123, 180); // #B77BB4 Bouquet
        
        // Red Velvet theme colors - Rich reds against dark neutrals
        private static readonly SolidColorBrush RedVelvetBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 26, 15, 15)); // #1A0F0F Deep red-black
        private static readonly Color RedVelvetSecondaryColor = Color.FromArgb(255, 60, 20, 20); // #3C1414 Dark maroon
        private static readonly Color RedVelvetTertiaryColor = Color.FromArgb(255, 139, 35, 35); // #8B2323 Firebrick
        private static readonly Color RedVelvetTextColor = Color.FromArgb(255, 255, 220, 220); // #FFDCDC Light pink-white
        private static readonly Color RedVelvetAccentColor = Color.FromArgb(255, 220, 60, 60); // #DC3C3C Bright red
        
        // Dreams theme colors - Neon pinks fading into cosmic purples
        private static readonly SolidColorBrush DreamsBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 33, 11, 75)); // #210B4B Violet
        private static readonly Color DreamsSecondaryColor = Color.FromArgb(255, 63, 28, 109); // #3F1C6D Meteorite
        private static readonly Color DreamsTertiaryColor = Color.FromArgb(255, 106, 42, 152); // #6A2A98 Daisy Bush
        private static readonly Color DreamsTextColor = Color.FromArgb(255, 255, 61, 148); // #FF3D94 Wild Strawberry
        private static readonly Color DreamsAccentColor = Color.FromArgb(255, 181, 48, 126); // #B5307E Medium Red Violet
        
        // Tiefling theme colors - Electric purples, hot pinks, glowing yellows
        private static readonly SolidColorBrush TieflingBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 58, 10, 77)); // #3A0A4D Jagger
        private static readonly Color TieflingSecondaryColor = Color.FromArgb(255, 113, 29, 154); // #711D9A Seance
        private static readonly Color TieflingTertiaryColor = Color.FromArgb(255, 164, 45, 180); // #A42DB4 Purple Heart
        private static readonly Color TieflingTextColor = Color.FromArgb(255, 249, 197, 78); // #F9C54E Saffron Mango
        private static readonly Color TieflingAccentColor = Color.FromArgb(255, 255, 92, 138); // #FF5C8A Wild Watermelon
        
        // Vibes theme colors - High-energy neon mix
        private static readonly SolidColorBrush VibesBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 15, 15, 30)); // #0F0F1E Dark blue-black
        private static readonly Color VibesSecondaryColor = Color.FromArgb(255, 30, 30, 60); // #1E1E3C Dark purple
        private static readonly Color VibesTertiaryColor = Color.FromArgb(255, 204, 0, 255); // #CC00FF Electric Violet
        private static readonly Color VibesTextColor = Color.FromArgb(255, 0, 255, 204); // #00FFCC Bright Turquoise
        private static readonly Color VibesAccentColor = Color.FromArgb(255, 255, 204, 0); // #FFCC00 Supernova
        
        // Store current theme for access by child controls
        public static Services.ThemePreference CurrentTheme { get; private set; } = Services.ThemePreference.System;

        private static void ApplyThemeToElement(FrameworkElement? element, ElementTheme theme, bool applyBackground = false)
        {
            if (element != null)
            {
                element.RequestedTheme = theme;

                if (applyBackground)
                {
                    var backgroundBrush = theme == ElementTheme.Dark ? DarkBackgroundBrush : LightBackgroundBrush;

                    switch (element)
                    {
                        case Panel panel:
                            panel.Background = backgroundBrush;
                            break;
                        case Control control:
                            control.Background = backgroundBrush;
                            break;
                    }
                }
            }
        }

        private static bool IsSystemThemeDark()
        {
            try
            {
                var uiSettings = new Windows.UI.ViewManagement.UISettings();
                var backgroundColor = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
                // Simple luminance check
                var luminance = 0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B;
                return luminance < 128;
            }
            catch
            {
                return false;
            }
        }

        // Track whether we're currently in a high-contrast theme
        private bool _wasHighContrastTheme = false;
        
        public void ApplyThemeAndBackdrop(Services.ThemePreference preference)
        {
            try
            {
                // Check if we're switching away from a high-contrast theme
                bool switchingFromHighContrast = _wasHighContrastTheme;
                
                // Store current theme
                CurrentTheme = preference;
                
                // Check for high-contrast themes first
                if (IsHighContrastTheme(preference))
                {
                    _wasHighContrastTheme = true;
                    ApplyHighContrastTheme(preference);
                    return;
                }
                
                // Only clear high-contrast resources if we were previously in a high-contrast theme
                if (switchingFromHighContrast)
                {
                    ClearHighContrastResources();
                    ForceThemeRefresh();
                }
                
                _wasHighContrastTheme = false;

                // Determine whether we should use dark theme
                bool useDarkTheme;
                switch (preference)
                {
                    case Services.ThemePreference.Light:
                        useDarkTheme = false;
                        break;
                    case Services.ThemePreference.Dark:
                        useDarkTheme = true;
                        break;
                    default:
                        useDarkTheme = IsSystemThemeDark();
                        break;
                }

                var elementTheme = useDarkTheme ? ElementTheme.Dark : ElementTheme.Light;

                ApplyThemeToElement(RootGrid, elementTheme, applyBackground: true);
                ApplyThemeToElement(MainSplitView, elementTheme, applyBackground: true);
                ApplyThemeToElement(SearchContentGrid, elementTheme, applyBackground: true);
                ApplyThemeToElement(RegexBuilderContentGrid, elementTheme, applyBackground: true);
                ApplyThemeToElement(SettingsContentGrid, elementTheme, applyBackground: true);
                ApplyThemeToElement(AboutContentGrid, elementTheme, applyBackground: true);
                ApplyThemeToElement(StatusInfoBar, elementTheme, applyBackground: true);
                
                // Apply backdrop based on theme preference
                // Light/System-Light: Mica backdrop
                // Dark/System-Dark: Solid dark background
                if (!useDarkTheme)
                {
                    // Light Mode: Use Mica backdrop
                    if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
                    {
                        try
                        {
                            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
                            ApplyThemeToElement(RootGrid, elementTheme, applyBackground: false);
                            Log("ApplyThemeAndBackdrop: Set Mica backdrop for Light mode");
                        }
                        catch (Exception ex)
                        {
                            Log($"ApplyThemeAndBackdrop: Failed to set Mica backdrop: {ex}");
                        }
                    }
                }
                else
                {
                    // Dark Mode or System: No backdrop
                    this.SystemBackdrop = null;
                    ApplyThemeToElement(RootGrid, elementTheme, applyBackground: true);
                    Log("ApplyThemeAndBackdrop: Removed backdrop for Dark/System mode");
                }
                
                // Update title bar button colors to match theme
                UpdateTitleBarButtonColors(preference);
                
                Log($"ApplyThemeAndBackdrop: Applied theme {(useDarkTheme ? "Dark" : "Light")} (preference: {preference})");
            }
            catch (Exception ex)
            {
                Log($"ApplyThemeAndBackdrop ERROR: {ex}");
                // Continue with default theme
            }
        }

        private static bool IsHighContrastTheme(Services.ThemePreference preference)
        {
            return preference == Services.ThemePreference.GentleGecko ||
                   preference == Services.ThemePreference.BlackKnight ||
                   preference == Services.ThemePreference.Paranoid ||
                   preference == Services.ThemePreference.Diamond ||
                   preference == Services.ThemePreference.Subspace ||
                   preference == Services.ThemePreference.RedVelvet ||
                   preference == Services.ThemePreference.Dreams ||
                   preference == Services.ThemePreference.Tiefling ||
                   preference == Services.ThemePreference.Vibes;
        }

        /// <summary>
        /// Event raised when the high-contrast theme changes, allowing child controls to update
        /// </summary>
        public static event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
        
        private void ApplyHighContrastTheme(Services.ThemePreference preference)
        {
            try
            {
                // Store current theme for child controls to access
                CurrentTheme = preference;
                
                // All high-contrast themes use dark element theme for proper control rendering
                var elementTheme = ElementTheme.Dark;
                
                // Remove any backdrop for high-contrast themes
                this.SystemBackdrop = null;
                
                // Get the appropriate colors
                var (backgroundBrush, secondaryBrush, tertiaryBrush, textBrush, accentBrush) = GetHighContrastColors(preference);

                // Apply theme to elements
                ApplyThemeToElement(RootGrid, elementTheme, applyBackground: false);
                ApplyThemeToElement(MainSplitView, elementTheme, applyBackground: false);
                ApplyThemeToElement(SearchContentGrid, elementTheme, applyBackground: false);
                ApplyThemeToElement(RegexBuilderContentGrid, elementTheme, applyBackground: false);
                ApplyThemeToElement(SettingsContentGrid, elementTheme, applyBackground: false);
                ApplyThemeToElement(AboutContentGrid, elementTheme, applyBackground: false);
                ApplyThemeToElement(StatusInfoBar, elementTheme, applyBackground: false);

                // Apply custom background colors
                if (RootGrid != null) RootGrid.Background = backgroundBrush;
                if (MainSplitView != null) MainSplitView.Background = backgroundBrush;
                if (SearchContentGrid != null) SearchContentGrid.Background = backgroundBrush;
                if (RegexBuilderContentGrid != null) RegexBuilderContentGrid.Background = backgroundBrush;
                if (SettingsContentGrid != null) SettingsContentGrid.Background = backgroundBrush;
                if (AboutContentGrid != null) AboutContentGrid.Background = backgroundBrush;
                
                // Apply to TabView - need to apply to the TabStrip background
                if (MainTabView != null)
                {
                    MainTabView.Background = secondaryBrush;
                    // Apply a custom style to the TabView to ensure proper coloring
                    ApplyTabViewHighContrastStyle(MainTabView, secondaryBrush, tertiaryBrush, textBrush);
                }
                
                // Apply to InfoBar with custom styling
                if (StatusInfoBar != null)
                {
                    ApplyInfoBarHighContrastStyle(StatusInfoBar, secondaryBrush, textBrush, accentBrush);
                }
                
                // Apply to NavigationView
                if (MainNavigationView != null)
                {
                    MainNavigationView.Background = backgroundBrush;
                    
                    // Set local resources for selection indicator
                    if (MainNavigationView.Resources == null)
                        MainNavigationView.Resources = new ResourceDictionary();
                    MainNavigationView.Resources["NavigationViewSelectionIndicatorForeground"] = accentBrush;
                    MainNavigationView.Resources["NavigationViewItemForeground"] = textBrush;
                    MainNavigationView.Resources["NavigationViewItemForegroundPointerOver"] = textBrush;
                    MainNavigationView.Resources["NavigationViewItemForegroundPressed"] = textBrush;
                    MainNavigationView.Resources["NavigationViewItemForegroundSelected"] = textBrush;
                    MainNavigationView.Resources["NavigationViewItemForegroundSelectedPointerOver"] = textBrush;
                    MainNavigationView.Resources["NavigationViewItemForegroundSelectedPressed"] = textBrush;
                }
                
                // Update title bar button colors for high-contrast theme
                UpdateTitleBarButtonColors(preference);
                
                // Apply theme resources
                ApplyHighContrastResources(preference, backgroundBrush, secondaryBrush, tertiaryBrush, textBrush, accentBrush);
                
                var themeArgs = new ThemeChangedEventArgs(preference, backgroundBrush, secondaryBrush, tertiaryBrush, textBrush, accentBrush);
                
                // Notify controls directly before raising event
                NotifyThemeAwareControls(themeArgs);
                
                // Raise event to notify child controls
                ThemeChanged?.Invoke(this, themeArgs);
                
                Log($"ApplyHighContrastTheme: Applied high-contrast theme {preference}");
            }
            catch (Exception ex)
            {
                Log($"ApplyHighContrastTheme ERROR: {ex}");
            }
        }
        
        private void ApplyTabViewHighContrastStyle(TabView tabView, SolidColorBrush background, SolidColorBrush selectedBackground, SolidColorBrush foreground)
        {
            try
            {
                // Apply background to the entire TabView
                tabView.Background = background;
                
                // Update resources for TabView items
                if (tabView.Resources == null)
                    tabView.Resources = new ResourceDictionary();
                    
                tabView.Resources["TabViewItemHeaderBackground"] = background;
                tabView.Resources["TabViewItemHeaderBackgroundSelected"] = selectedBackground;
                tabView.Resources["TabViewItemHeaderBackgroundPointerOver"] = selectedBackground;
                tabView.Resources["TabViewItemHeaderBackgroundPressed"] = selectedBackground;
                tabView.Resources["TabViewItemHeaderForeground"] = foreground;
                tabView.Resources["TabViewItemHeaderForegroundSelected"] = foreground;
                tabView.Resources["TabViewItemHeaderForegroundPointerOver"] = foreground;
                tabView.Resources["TabViewBackground"] = background;
                tabView.Resources["TabViewBorderBrush"] = background;
                
                // Force update of existing tabs
                foreach (var item in tabView.TabItems)
                {
                    if (item is TabViewItem tab)
                    {
                        tab.Background = background;
                        tab.Foreground = foreground;
                        
                        // Apply foreground to all text in the tab header
                        ApplyForegroundToVisualTree(tab, foreground);
                    }
                }
                
                // Apply to TabStrip area as well
                ApplyForegroundToVisualTree(tabView, foreground);
                
                Log($"ApplyTabViewHighContrastStyle: Applied style to TabView");
            }
            catch (Exception ex)
            {
                Log($"ApplyTabViewHighContrastStyle ERROR: {ex}");
            }
        }
        
        private void ApplyInfoBarHighContrastStyle(InfoBar infoBar, SolidColorBrush background, SolidColorBrush foreground, SolidColorBrush iconColor)
        {
            try
            {
                infoBar.Background = background;
                infoBar.Foreground = foreground;
                
                // Update InfoBar resources
                if (infoBar.Resources == null)
                    infoBar.Resources = new ResourceDictionary();
                    
                infoBar.Resources["InfoBarInformationalSeverityBackgroundBrush"] = background;
                infoBar.Resources["InfoBarInformationalSeverityIconBackground"] = background;
                infoBar.Resources["InfoBarInformationalSeverityIconForeground"] = iconColor;
                infoBar.Resources["InfoBarTitleForeground"] = foreground;
                infoBar.Resources["InfoBarMessageForeground"] = foreground;
                
                // Also apply to all child TextBlocks directly
                ApplyForegroundToVisualTree(infoBar, foreground);
                
                Log($"ApplyInfoBarHighContrastStyle: Applied style to InfoBar");
            }
            catch (Exception ex)
            {
                Log($"ApplyInfoBarHighContrastStyle ERROR: {ex}");
            }
        }
        
        private static void ApplyForegroundToVisualTree(DependencyObject parent, SolidColorBrush foreground)
        {
            try
            {
                var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < count; i++)
                {
                    var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                    
                    if (child is TextBlock textBlock)
                    {
                        textBlock.Foreground = foreground;
                    }
                    else if (child is ContentPresenter contentPresenter)
                    {
                        contentPresenter.Foreground = foreground;
                    }
                    else if (child is CheckBox checkBox)
                    {
                        checkBox.Foreground = foreground;
                    }
                    else if (child is Button button)
                    {
                        button.Foreground = foreground;
                    }
                    
                    // Recurse into children
                    ApplyForegroundToVisualTree(child, foreground);
                }
            }
            catch
            {
                // Ignore errors during visual tree traversal
            }
        }
        
        private static (SolidColorBrush background, SolidColorBrush secondary, SolidColorBrush tertiary, SolidColorBrush text, SolidColorBrush accent) GetHighContrastColors(Services.ThemePreference preference)
        {
            return preference switch
            {
                Services.ThemePreference.GentleGecko => (
                    GentleGeckoBackgroundBrush,
                    new SolidColorBrush(GentleGeckoSecondaryColor),
                    new SolidColorBrush(GentleGeckoTertiaryColor),
                    new SolidColorBrush(GentleGeckoTextColor),
                    new SolidColorBrush(GentleGeckoAccentColor)
                ),
                Services.ThemePreference.BlackKnight => (
                    BlackKnightBackgroundBrush,
                    new SolidColorBrush(BlackKnightSecondaryColor),
                    new SolidColorBrush(BlackKnightTertiaryColor),
                    new SolidColorBrush(BlackKnightTextColor),
                    new SolidColorBrush(BlackKnightAccentColor)
                ),
                Services.ThemePreference.Paranoid => (
                    ParanoidBackgroundBrush,
                    new SolidColorBrush(ParanoidSecondaryColor),
                    new SolidColorBrush(ParanoidTertiaryColor),
                    new SolidColorBrush(ParanoidTextColor),
                    new SolidColorBrush(ParanoidAccentColor)
                ),
                Services.ThemePreference.Diamond => (
                    DiamondBackgroundBrush,
                    new SolidColorBrush(DiamondSecondaryColor),
                    new SolidColorBrush(DiamondTertiaryColor),
                    new SolidColorBrush(DiamondTextColor),
                    new SolidColorBrush(DiamondAccentColor)
                ),
                Services.ThemePreference.Subspace => (
                    SubspaceBackgroundBrush,
                    new SolidColorBrush(SubspaceSecondaryColor),
                    new SolidColorBrush(SubspaceTertiaryColor),
                    new SolidColorBrush(SubspaceTextColor),
                    new SolidColorBrush(SubspaceAccentColor)
                ),
                Services.ThemePreference.RedVelvet => (
                    RedVelvetBackgroundBrush,
                    new SolidColorBrush(RedVelvetSecondaryColor),
                    new SolidColorBrush(RedVelvetTertiaryColor),
                    new SolidColorBrush(RedVelvetTextColor),
                    new SolidColorBrush(RedVelvetAccentColor)
                ),
                Services.ThemePreference.Dreams => (
                    DreamsBackgroundBrush,
                    new SolidColorBrush(DreamsSecondaryColor),
                    new SolidColorBrush(DreamsTertiaryColor),
                    new SolidColorBrush(DreamsTextColor),
                    new SolidColorBrush(DreamsAccentColor)
                ),
                Services.ThemePreference.Tiefling => (
                    TieflingBackgroundBrush,
                    new SolidColorBrush(TieflingSecondaryColor),
                    new SolidColorBrush(TieflingTertiaryColor),
                    new SolidColorBrush(TieflingTextColor),
                    new SolidColorBrush(TieflingAccentColor)
                ),
                Services.ThemePreference.Vibes => (
                    VibesBackgroundBrush,
                    new SolidColorBrush(VibesSecondaryColor),
                    new SolidColorBrush(VibesTertiaryColor),
                    new SolidColorBrush(VibesTextColor),
                    new SolidColorBrush(VibesAccentColor)
                ),
                _ => (
                    DarkBackgroundBrush,
                    new SolidColorBrush(Color.FromArgb(255, 40, 40, 40)),
                    new SolidColorBrush(Color.FromArgb(255, 60, 60, 60)),
                    new SolidColorBrush(Colors.White),
                    new SolidColorBrush(Color.FromArgb(255, 100, 100, 100))
                )
            };
        }
        
        private static void SetBrushResources(ResourceDictionary resources, SolidColorBrush brush, params string[] keys)
        {
            foreach (var key in keys)
            {
                SetOrUpdateBrushResource(resources, key, brush);
            }
        }

        private static void SetOrUpdateBrushResource(ResourceDictionary resources, string key, SolidColorBrush brush)
        {
            // Always create a new brush - existing system brushes may be frozen/immutable
            // and will throw UnauthorizedAccessException if we try to modify them
            resources[key] = new SolidColorBrush(brush.Color)
            {
                Opacity = brush.Opacity
            };
        }

        private void ApplyHighContrastResources(Services.ThemePreference preference, SolidColorBrush background, SolidColorBrush secondary, SolidColorBrush tertiary, SolidColorBrush text, SolidColorBrush accent)
        {
            try
            {
                // Apply to application resources for global effect
                var resources = Application.Current.Resources;
                
                // Override theme brushes
                SetBrushResources(resources, background,
                    "ApplicationPageBackgroundThemeBrush",
                    "SolidBackgroundFillColorBaseBrush");
                SetBrushResources(resources, secondary,
                    "CardBackgroundFillColorDefaultBrush",
                    "ControlFillColorDefaultBrush",
                    "LayerFillColorDefaultBrush");
                SetBrushResources(resources, text, "TextFillColorPrimaryBrush");
                SetBrushResources(resources, accent, "TextFillColorSecondaryBrush");
                
                // InfoBar specific resources
                SetBrushResources(resources, secondary,
                    "InfoBarInformationalSeverityBackgroundBrush",
                    "InfoBarInformationalSeverityIconBackground",
                    "InfoBarBackground");
                SetBrushResources(resources, accent, "InfoBarInformationalSeverityIconForeground");
                SetBrushResources(resources, text,
                    "InfoBarTitleForeground",
                    "InfoBarMessageForeground",
                    "InfoBarForeground");
                
                // TabView specific resources  
                SetBrushResources(resources, secondary,
                    "TabViewBackground",
                    "TabViewItemHeaderBackground");
                SetBrushResources(resources, tertiary,
                    "TabViewItemHeaderBackgroundSelected",
                    "TabViewItemHeaderBackgroundPointerOver");
                SetBrushResources(resources, text,
                    "TabViewItemHeaderForeground",
                    "TabViewItemHeaderForegroundSelected");
                
                // ListView/DataGrid specific resources
                SetBrushResources(resources, background, "ListViewItemBackground");
                SetBrushResources(resources, secondary, "ListViewItemBackgroundPointerOver");
                SetBrushResources(resources, tertiary, "ListViewItemBackgroundSelected");
                SetBrushResources(resources, text, "ListViewItemForeground");
                
                // NavigationView specific resources
                SetBrushResources(resources, background, "NavigationViewContentBackground");
                SetBrushResources(resources, secondary, "NavigationViewDefaultPaneBackground");
                SetBrushResources(resources, accent, "NavigationViewSelectionIndicatorForeground");
                SetBrushResources(resources, text,
                    "NavigationViewItemForeground",
                    "NavigationViewItemForegroundPointerOver",
                    "NavigationViewItemForegroundPressed",
                    "NavigationViewItemForegroundSelected",
                    "NavigationViewItemForegroundSelectedPointerOver",
                    "NavigationViewItemForegroundSelectedPressed");
                
                // Button specific resources
                SetBrushResources(resources, text,
                    "ButtonForeground",
                    "ButtonForegroundPointerOver",
                    "ButtonForegroundPressed");
                SetBrushResources(resources, accent, "ButtonForegroundDisabled");
                SetBrushResources(resources, secondary, "ButtonBackground");
                SetBrushResources(resources, tertiary,
                    "ButtonBackgroundPointerOver",
                    "ButtonBackgroundPressed");
                
                // CheckBox specific resources
                SetBrushResources(resources, text,
                    "CheckBoxForeground",
                    "CheckBoxForegroundPointerOver",
                    "CheckBoxForegroundPressed",
                    "CheckBoxCheckGlyphForegroundChecked",
                    "CheckBoxCheckGlyphForegroundCheckedPointerOver",
                    "CheckBoxCheckGlyphForegroundCheckedPressed");
                SetBrushResources(resources, accent,
                    "CheckBoxForegroundDisabled",
                    "CheckBoxCheckBackgroundFillChecked",
                    "CheckBoxCheckBackgroundFillCheckedPointerOver",
                    "CheckBoxCheckBackgroundFillCheckedPressed");
                
                // ComboBox specific resources
                SetBrushResources(resources, text,
                    "ComboBoxForeground",
                    "ComboBoxForegroundPointerOver",
                    "ComboBoxForegroundPressed");
                SetBrushResources(resources, secondary, "ComboBoxBackground");
                SetBrushResources(resources, tertiary, "ComboBoxBackgroundPointerOver");
                
                // TextBox specific resources
                SetBrushResources(resources, text,
                    "TextBoxForeground",
                    "TextBoxForegroundPointerOver",
                    "TextBoxForegroundFocused",
                    "TextControlForeground",
                    "TextControlForegroundPointerOver",
                    "TextControlForegroundFocused");
                
                // AccentButton specific resources (used by Browse and Filter Options buttons)
                SetBrushResources(resources, text,
                    "AccentButtonForeground",
                    "AccentButtonForegroundPointerOver",
                    "AccentButtonForegroundPressed");
                SetBrushResources(resources, accent, "AccentButtonBackground");
                SetBrushResources(resources, tertiary,
                    "AccentButtonBackgroundPointerOver",
                    "AccentButtonBackgroundPressed");
                
                // AppBarToggleButton specific resources (used by Filter Options toggle button)
                SetBrushResources(resources, text,
                    "AppBarToggleButtonForeground",
                    "AppBarToggleButtonForegroundPointerOver",
                    "AppBarToggleButtonForegroundPressed",
                    "AppBarToggleButtonForegroundChecked",
                    "AppBarToggleButtonForegroundCheckedPointerOver",
                    "AppBarToggleButtonForegroundCheckedPressed",
                    "AppBarToggleButtonRevealForeground",
                    "AppBarToggleButtonRevealForegroundPointerOver",
                    "AppBarToggleButtonRevealForegroundPressed",
                    "AppBarToggleButtonRevealForegroundChecked",
                    "AppBarToggleButtonRevealForegroundCheckedPointerOver",
                    "AppBarToggleButtonRevealForegroundCheckedPressed");
                SetBrushResources(resources, accent,
                    "AppBarToggleButtonBackground",
                    "AppBarToggleButtonBackgroundChecked",
                    "AppBarToggleButtonRevealBackground",
                    "AppBarToggleButtonRevealBackgroundChecked");
                SetBrushResources(resources, tertiary,
                    "AppBarToggleButtonBackgroundPointerOver",
                    "AppBarToggleButtonBackgroundPressed",
                    "AppBarToggleButtonBackgroundCheckedPointerOver",
                    "AppBarToggleButtonBackgroundCheckedPressed",
                    "AppBarToggleButtonRevealBackgroundPointerOver",
                    "AppBarToggleButtonRevealBackgroundPressed",
                    "AppBarToggleButtonRevealBackgroundCheckedPointerOver",
                    "AppBarToggleButtonRevealBackgroundCheckedPressed");
                
                Log($"ApplyHighContrastResources: Applied resource overrides for {preference}");
            }
            catch (Exception ex)
            {
                Log($"ApplyHighContrastResources ERROR: {ex}");
            }
        }

        private void NotifyThemeAwareControls(ThemeChangedEventArgs args)
        {
            try
            {
                SettingsView?.ApplyThemeFromHost(args);
            }
            catch (Exception ex)
            {
                Log($"NotifyThemeAwareControls SettingsView ERROR: {ex}");
            }

            try
            {
                RegexBuilderView?.ApplyThemeFromHost(args);
            }
            catch (Exception ex)
            {
                Log($"NotifyThemeAwareControls RegexBuilderView ERROR: {ex}");
            }

            try
            {
                AboutView?.ApplyThemeFromHost(args);
            }
            catch (Exception ex)
            {
                Log($"NotifyThemeAwareControls AboutView ERROR: {ex}");
            }

            ApplyThemeToSearchTabs(args);
        }

        private void ApplyThemeToSearchTabs(ThemeChangedEventArgs args)
        {
            try
            {
                if (MainTabView == null || MainTabView.TabItems == null || MainTabView.TabItems.Count == 0)
                {
                    return;
                }

                foreach (var tab in MainTabView.TabItems.OfType<TabViewItem>())
                {
                    if (tab.Content is SearchTabContent searchContent)
                    {
                        try
                        {
                            searchContent.ApplyThemeFromHost(args);
                        }
                        catch (Exception ex)
                        {
                            Log($"ApplyThemeToSearchTabs ITEM ERROR: {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"ApplyThemeToSearchTabs ERROR: {ex}");
            }
        }
        
        private void ClearHighContrastResources()
        {
            try
            {
                var resources = Application.Current.Resources;
                
                // Remove all overridden resources by setting them to null or removing
                var keysToRemove = new[]
                {
                    "ApplicationPageBackgroundThemeBrush", "CardBackgroundFillColorDefaultBrush",
                    "ControlFillColorDefaultBrush", "LayerFillColorDefaultBrush",
                    "SolidBackgroundFillColorBaseBrush", "TextFillColorPrimaryBrush",
                    "TextFillColorSecondaryBrush", "InfoBarInformationalSeverityBackgroundBrush",
                    "InfoBarInformationalSeverityIconBackground", "InfoBarInformationalSeverityIconForeground",
                    "InfoBarTitleForeground", "InfoBarMessageForeground", "InfoBarBackground", "InfoBarForeground",
                    "TabViewBackground", "TabViewItemHeaderBackground",
                    "TabViewItemHeaderBackgroundSelected", "TabViewItemHeaderBackgroundPointerOver",
                    "TabViewItemHeaderForeground", "TabViewItemHeaderForegroundSelected",
                    "ListViewItemBackground", "ListViewItemBackgroundPointerOver",
                    "ListViewItemBackgroundSelected", "ListViewItemForeground",
                    "NavigationViewContentBackground", "NavigationViewDefaultPaneBackground",
                    "NavigationViewSelectionIndicatorForeground", "NavigationViewItemForeground",
                    "NavigationViewItemForegroundPointerOver", "NavigationViewItemForegroundPressed",
                    "NavigationViewItemForegroundSelected", "NavigationViewItemForegroundSelectedPointerOver",
                    "NavigationViewItemForegroundSelectedPressed",
                    "ButtonForeground", "ButtonForegroundPointerOver", "ButtonForegroundPressed",
                    "ButtonForegroundDisabled", "ButtonBackground", "ButtonBackgroundPointerOver",
                    "ButtonBackgroundPressed", "CheckBoxForeground", "CheckBoxForegroundPointerOver",
                    "CheckBoxForegroundPressed", "CheckBoxForegroundDisabled",
                    "CheckBoxCheckGlyphForegroundChecked", "CheckBoxCheckGlyphForegroundCheckedPointerOver",
                    "CheckBoxCheckGlyphForegroundCheckedPressed", "CheckBoxCheckBackgroundFillChecked",
                    "CheckBoxCheckBackgroundFillCheckedPointerOver", "CheckBoxCheckBackgroundFillCheckedPressed",
                    "ComboBoxForeground", "ComboBoxForegroundPointerOver", "ComboBoxForegroundPressed",
                    "ComboBoxBackground", "ComboBoxBackgroundPointerOver",
                    "TextBoxForeground", "TextBoxForegroundPointerOver", "TextBoxForegroundFocused",
                    "TextControlForeground", "TextControlForegroundPointerOver", "TextControlForegroundFocused",
                    "AccentButtonForeground", "AccentButtonForegroundPointerOver", "AccentButtonForegroundPressed",
                    "AccentButtonBackground", "AccentButtonBackgroundPointerOver", "AccentButtonBackgroundPressed"
                };
                
                foreach (var key in keysToRemove)
                {
                    if (resources.ContainsKey(key))
                    {
                        resources.Remove(key);
                    }
                }
                
                // Clear MainWindow UI elements
                ClearMainWindowHighContrastStyles();
                
                // Notify child controls to clear their theme colors
                var clearArgs = new ThemeChangedEventArgs(
                    CurrentTheme,
                    new SolidColorBrush(Colors.Transparent),
                    new SolidColorBrush(Colors.Transparent),
                    new SolidColorBrush(Colors.Transparent),
                    new SolidColorBrush(Colors.Transparent),
                    new SolidColorBrush(Colors.Transparent)
                );
                
                NotifyThemeAwareControls(clearArgs);
                ThemeChanged?.Invoke(this, clearArgs);
                
                Log("ClearHighContrastResources: Cleared all high-contrast resource overrides");
            }
            catch (Exception ex)
            {
                Log($"ClearHighContrastResources ERROR: {ex}");
            }
        }
        
        private void ForceThemeRefresh()
        {
            try
            {
                if (Content is FrameworkElement rootElement)
                {
                    // Store current theme
                    var currentTheme = rootElement.RequestedTheme;
                    
                    // Toggle to force refresh
                    rootElement.RequestedTheme = currentTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
                    
                    // Toggle back - this forces WinUI to re-evaluate all theme resources
                    rootElement.RequestedTheme = currentTheme;
                    
                    Log($"ForceThemeRefresh: Toggled theme to force refresh, restored to {currentTheme}");
                }
            }
            catch (Exception ex)
            {
                Log($"ForceThemeRefresh ERROR: {ex}");
            }
        }
        
        private void ClearMainWindowHighContrastStyles()
        {
            try
            {
                // Clear NavigationView styling
                if (MainNavigationView != null)
                {
                    MainNavigationView.ClearValue(NavigationView.BackgroundProperty);
                    MainNavigationView.Resources?.Clear();
                    
                    // Clear navigation items
                    foreach (var item in MainNavigationView.MenuItems.OfType<NavigationViewItem>())
                    {
                        item.ClearValue(NavigationViewItem.ForegroundProperty);
                        item.ClearValue(NavigationViewItem.BackgroundProperty);
                    }
                    
                    ClearForegroundFromElement(MainNavigationView);
                }
                
                // Clear TabView styling
                if (MainTabView != null)
                {
                    MainTabView.ClearValue(TabView.BackgroundProperty);
                    MainTabView.Resources?.Clear();
                    
                    // Clear each tab item's header
                    foreach (var item in MainTabView.TabItems.OfType<TabViewItem>())
                    {
                        item.ClearValue(TabViewItem.ForegroundProperty);
                        item.ClearValue(TabViewItem.BackgroundProperty);
                        if (item.Header is TextBlock headerText)
                        {
                            headerText.ClearValue(TextBlock.ForegroundProperty);
                        }
                    }
                }
                
                // Clear InfoBar styling
                if (StatusInfoBar != null)
                {
                    StatusInfoBar.ClearValue(InfoBar.BackgroundProperty);
                    StatusInfoBar.ClearValue(InfoBar.ForegroundProperty);
                    StatusInfoBar.Resources?.Clear();
                    ClearForegroundFromElement(StatusInfoBar);
                }
                
                // Clear SplitView
                if (MainSplitView != null)
                {
                    MainSplitView.ClearValue(SplitView.BackgroundProperty);
                    MainSplitView.ClearValue(SplitView.PaneBackgroundProperty);
                }
                
                // Clear main content grids
                SearchContentGrid?.ClearValue(Grid.BackgroundProperty);
                RegexBuilderContentGrid?.ClearValue(Grid.BackgroundProperty);
                SettingsContentGrid?.ClearValue(Grid.BackgroundProperty);
                AboutContentGrid?.ClearValue(Grid.BackgroundProperty);
                
                // Clear root grid
                if (Content is Grid rootGrid)
                {
                    rootGrid.ClearValue(Grid.BackgroundProperty);
                    ClearForegroundFromElement(rootGrid);
                }
            }
            catch (Exception ex)
            {
                Log($"ClearMainWindowHighContrastStyles ERROR: {ex}");
            }
        }
        
        private void ClearForegroundFromElement(DependencyObject parent)
        {
            try
            {
                var count = VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    
                    if (child is TextBlock textBlock)
                    {
                        textBlock.ClearValue(TextBlock.ForegroundProperty);
                    }
                    else if (child is ContentPresenter contentPresenter)
                    {
                        contentPresenter.ClearValue(ContentPresenter.ForegroundProperty);
                    }
                    else if (child is Button button)
                    {
                        button.ClearValue(Button.ForegroundProperty);
                        button.ClearValue(Button.BackgroundProperty);
                    }
                    else if (child is CheckBox checkBox)
                    {
                        checkBox.ClearValue(CheckBox.ForegroundProperty);
                        checkBox.ClearValue(CheckBox.BackgroundProperty);
                    }
                    else if (child is ComboBox comboBox)
                    {
                        comboBox.ClearValue(ComboBox.ForegroundProperty);
                        comboBox.ClearValue(ComboBox.BackgroundProperty);
                    }
                    
                    ClearForegroundFromElement(child);
                }
            }
            catch
            {
                // Ignore errors during visual tree traversal
            }
        }
        
        /// <summary>
        /// Gets the theme colors for the current high-contrast theme (for child controls to use)
        /// </summary>
        public static (SolidColorBrush background, SolidColorBrush secondary, SolidColorBrush tertiary, SolidColorBrush text, SolidColorBrush accent) GetCurrentThemeColors()
        {
            if (!IsHighContrastTheme(CurrentTheme))
            {
                return (DarkBackgroundBrush, DarkBackgroundBrush, DarkBackgroundBrush, new SolidColorBrush(Colors.White), new SolidColorBrush(Colors.Gray));
            }
            return GetHighContrastColors(CurrentTheme);
        }

        private void UpdateTitleBarButtonColors(Services.ThemePreference preference)
        {
            try
            {
                if (_appWindow == null || !AppWindowTitleBar.IsCustomizationSupported())
                    return;

                var titleBar = _appWindow.TitleBar;
                
                // Handle high-contrast themes
                if (IsHighContrastTheme(preference))
                {
                    UpdateTitleBarButtonColorsForHighContrast(titleBar, preference);
                    return;
                }
                
                // Determine whether we should use dark theme
                bool useDarkTheme;
                switch (preference)
                {
                    case Services.ThemePreference.Light:
                        useDarkTheme = false;
                        break;
                    case Services.ThemePreference.Dark:
                        useDarkTheme = true;
                        break;
                    default:
                        useDarkTheme = IsSystemThemeDark();
                        break;
                }

                if (useDarkTheme)
                {
                    // Dark mode: Light buttons on dark background
                    titleBar.ButtonBackgroundColor = Colors.Transparent;
                    titleBar.ButtonForegroundColor = Colors.White;
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 50, 50, 50);
                    titleBar.ButtonHoverForegroundColor = Colors.White;
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 70, 70, 70);
                    titleBar.ButtonPressedForegroundColor = Colors.White;
                    titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                    titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
                }
                else
                {
                    // Light mode: Dark buttons on light background
                    titleBar.ButtonBackgroundColor = Colors.Transparent;
                    titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0); // Black
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 240, 240, 240); // Light gray hover
                    titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0); // Black
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 220, 220, 220); // Slightly darker gray
                    titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0); // Black
                    titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                    titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 160, 160, 160); // Gray for inactive
                }
                
                Log($"UpdateTitleBarButtonColors: Updated button colors for {(useDarkTheme ? "Dark" : "Light")} mode");
            }
            catch (Exception ex)
            {
                Log($"UpdateTitleBarButtonColors ERROR: {ex}");
            }
        }

        private void UpdateTitleBarButtonColorsForHighContrast(AppWindowTitleBar titleBar, Services.ThemePreference preference)
        {
            Color textColor, accentColor, secondaryColor;
            
            switch (preference)
            {
                case Services.ThemePreference.GentleGecko:
                    textColor = GentleGeckoTextColor;
                    accentColor = GentleGeckoAccentColor;
                    secondaryColor = GentleGeckoSecondaryColor;
                    break;
                case Services.ThemePreference.BlackKnight:
                    textColor = BlackKnightTextColor;
                    accentColor = BlackKnightAccentColor;
                    secondaryColor = BlackKnightSecondaryColor;
                    break;
                case Services.ThemePreference.Paranoid:
                    textColor = ParanoidTextColor;
                    accentColor = ParanoidAccentColor;
                    secondaryColor = ParanoidSecondaryColor;
                    break;
                case Services.ThemePreference.Diamond:
                    textColor = DiamondTextColor;
                    accentColor = DiamondAccentColor;
                    secondaryColor = DiamondSecondaryColor;
                    break;
                case Services.ThemePreference.Subspace:
                    textColor = SubspaceTextColor;
                    accentColor = SubspaceAccentColor;
                    secondaryColor = SubspaceSecondaryColor;
                    break;
                case Services.ThemePreference.RedVelvet:
                    textColor = RedVelvetTextColor;
                    accentColor = RedVelvetAccentColor;
                    secondaryColor = RedVelvetSecondaryColor;
                    break;
                case Services.ThemePreference.Dreams:
                    textColor = DreamsTextColor;
                    accentColor = DreamsAccentColor;
                    secondaryColor = DreamsSecondaryColor;
                    break;
                case Services.ThemePreference.Tiefling:
                    textColor = TieflingTextColor;
                    accentColor = TieflingAccentColor;
                    secondaryColor = TieflingSecondaryColor;
                    break;
                case Services.ThemePreference.Vibes:
                    textColor = VibesTextColor;
                    accentColor = VibesAccentColor;
                    secondaryColor = VibesSecondaryColor;
                    break;
                default:
                    return;
            }
            
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = textColor;
            titleBar.ButtonHoverBackgroundColor = secondaryColor;
            titleBar.ButtonHoverForegroundColor = textColor;
            titleBar.ButtonPressedBackgroundColor = accentColor;
            titleBar.ButtonPressedForegroundColor = Colors.White;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = accentColor;
            
            Log($"UpdateTitleBarButtonColorsForHighContrast: Updated button colors for {preference} theme");
        }

        private static string GetString(string key) =>
            LocalizationService.Instance.GetLocalizedString(key);

        private static string GetString(string key, params object[] args) =>
            LocalizationService.Instance.GetLocalizedString(key, args);

        /// <summary>
        /// Refreshes the UI when the application language changes
        /// </summary>
        public void RefreshLocalization()
        {
            try
            {
                var locService = LocalizationService.Instance;
                LocalizedToolTipRegistry.RefreshRegisteredToolTips();
                
                // Update window title
                var localizedTitle = locService.GetLocalizedString("MainWindow.Title");
                if (!string.IsNullOrEmpty(localizedTitle) && localizedTitle != "MainWindow.Title")
                {
                    this.Title = localizedTitle;
                }
                
                // Update navigation items
                if (SearchNavItem != null)
                {
                    SearchNavItem.Content = locService.GetLocalizedString("SearchNavItem.Content");
                }
                
                if (RegexBuilderNavItem != null)
                {
                    RegexBuilderNavItem.Content = locService.GetLocalizedString("RegexBuilderNavItem.Content");
                }
                
                if (SettingsNavItem != null)
                {
                    SettingsNavItem.Content = locService.GetLocalizedString("SettingsNavItem.Content");
                }
                
                if (AboutNavItem != null)
                {
                    AboutNavItem.Content = locService.GetLocalizedString("AboutNavItem.Content");
                }
                
                // Update StatusInfoBar
                if (StatusInfoBar != null)
                {
                    StatusInfoBar.Title = locService.GetLocalizedString("StatusInfoBar.Title");
                    // Don't update Message as it's dynamic based on search state
                }
                
                // Refresh all child views that might have localized content
                RefreshChildViews(locService);
                
                // Force layout update on the root grid
                if (RootGrid != null)
                {
                    RootGrid.InvalidateArrange();
                    RootGrid.InvalidateMeasure();
                    RootGrid.UpdateLayout();
                }
            }
            catch (Exception ex)
            {
                Log($"RefreshLocalization error: {ex.Message}");
            }
        }

        private void RefreshChildViews(LocalizationService locService)
        {
            try
            {
                SettingsView?.HostRefreshLocalization();
                AboutView?.RefreshLocalization();
                RefreshTabViewModels();
                ReloadRegexBuilderView();
                ReloadSearchTabs();
            }
            catch (Exception ex)
            {
                Log($"RefreshChildViews error: {ex.Message}");
            }
        }

        private void RefreshTabViewModels()
        {
            try
            {
                foreach (var tab in ViewModel.Tabs)
                {
                    tab.RefreshLocalization();
                }
            }
            catch (Exception ex)
            {
                Log($"RefreshTabViewModels error: {ex.Message}");
            }
        }

        private void ReloadRegexBuilderView()
        {
            try
            {
                if (RegexBuilderContentGrid == null)
                    return;

                RegexBuilderContentGrid.Children.Clear();
                var newRegexBuilderView = new Controls.RegexBuilderView();
                RegexBuilderView = newRegexBuilderView;
                RegexBuilderContentGrid.Children.Add(newRegexBuilderView);
                // Also call RefreshLocalization to ensure all elements are updated
                newRegexBuilderView.RefreshLocalization();
            }
            catch (Exception ex)
            {
                Log($"ReloadRegexBuilderView error: {ex.Message}");
            }
        }

        private void ReloadSearchTabs()
        {
            // Prevent concurrent execution to avoid crashes
            lock (_reloadTabsLock)
            {
                if (_isReloadingTabs)
                {
                    Log("ReloadSearchTabs: Already reloading, skipping duplicate call");
                    return;
                }

                _isReloadingTabs = true;
            }

            try
            {
                if (MainTabView == null)
                {
                    lock (_reloadTabsLock)
                    {
                        _isReloadingTabs = false;
                    }
                    return;
                }

                var tabItems = MainTabView.TabItems.OfType<TabViewItem>().ToList();
                var selectedItem = MainTabView.SelectedItem;

                foreach (var tabItem in tabItems)
                {
                    if (tabItem.Content is Controls.SearchTabContent oldContent)
                    {
                        // Save the ViewModel before cleaning up
                        var viewModel = oldContent.ViewModel;
                        
                        // Unbind and clean up the old content before replacing it
                        try
                        {
                            oldContent.UnbindViewModel();
                        }
                        catch (Exception ex)
                        {
                            Log($"ReloadSearchTabs: Error unbinding old content: {ex.Message}");
                        }
                        
                        // Create new content first (without ViewModel to avoid premature initialization)
                        var newContent = new Controls.SearchTabContent();
                        
                        // Clear the old content and set the new content
                        // This ensures the old content is unloaded before the new one is added
                        tabItem.Content = null;
                        
                        // Force layout update to ensure old content is fully disposed
                        tabItem.UpdateLayout();
                        
                        // Now set the new content
                        tabItem.Content = newContent;
                        
                        // Set ViewModel after content is added to visual tree
                        // This ensures the control is in the visual tree before DataContextChanged fires
                        newContent.ViewModel = viewModel;
                        
                        // Force layout update to ensure new content is loaded
                        tabItem.UpdateLayout();
                        
                        // Call RefreshLocalization after the content is fully loaded
                        // Use dispatcher to ensure it happens after layout is complete
                        DispatcherQueue?.TryEnqueue(() =>
                        {
                            try
                            {
                                if (newContent != null && tabItem.Content is Controls.SearchTabContent currentContent && currentContent == newContent)
                                {
                                    newContent.RefreshLocalization();
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"ReloadSearchTabs: Error refreshing localization: {ex.Message}");
                            }
                        });
                    }
                }

                if (selectedItem != null)
                {
                    MainTabView.SelectedItem = selectedItem;
                }
            }
            catch (Exception ex)
            {
                Log($"ReloadSearchTabs error: {ex.Message}");
                Log($"ReloadSearchTabs StackTrace: {ex.StackTrace}");
            }
            finally
            {
                // Always reset the flag, even if an exception occurs
                lock (_reloadTabsLock)
                {
                    _isReloadingTabs = false;
                }
            }
        }

        private void LocalizationService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LocalizationService.CurrentCulture))
            {
                try
                {
                    // Cancel any pending refresh to debounce rapid changes
                    lock (_refreshLocalizationLock)
                    {
                        _refreshLocalizationCancellation?.Cancel();
                        _refreshLocalizationCancellation?.Dispose();
                        _refreshLocalizationCancellation = new System.Threading.CancellationTokenSource();
                        var cancellationToken = _refreshLocalizationCancellation.Token;
                        var tokenSource = _refreshLocalizationCancellation;
                        
                        // Debounce: wait 300ms before refreshing to avoid rapid successive calls
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(300, cancellationToken);
                                
                                // Check if this task's token source is still the current one
                                // (if it was replaced, we should not execute)
                                lock (_refreshLocalizationLock)
                                {
                                    if (tokenSource == _refreshLocalizationCancellation && !cancellationToken.IsCancellationRequested)
                                    {
                                        DispatcherQueue?.TryEnqueue(() =>
                                        {
                                            // Final check before executing
                                            lock (_refreshLocalizationLock)
                                            {
                                                if (tokenSource == _refreshLocalizationCancellation && !cancellationToken.IsCancellationRequested)
                                                {
                                                    RefreshLocalization();
                                                }
                                            }
                                        });
                                    }
                                }
                            }
                            catch (System.Threading.Tasks.TaskCanceledException)
                            {
                                // Expected when cancelled - ignore
                            }
                            catch (Exception ex)
                            {
                                Log($"LocalizationService_PropertyChanged debounce error: {ex.Message}");
                            }
                        }, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    Log($"LocalizationService_PropertyChanged error: {ex.Message}");
                }
            }
        }

    }
    
    /// <summary>
    /// Event args for theme change notifications
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public Services.ThemePreference Theme { get; }
        public SolidColorBrush BackgroundBrush { get; }
        public SolidColorBrush SecondaryBrush { get; }
        public SolidColorBrush TertiaryBrush { get; }
        public SolidColorBrush TextBrush { get; }
        public SolidColorBrush AccentBrush { get; }
        
        public ThemeChangedEventArgs(
            Services.ThemePreference theme,
            SolidColorBrush background,
            SolidColorBrush secondary,
            SolidColorBrush tertiary,
            SolidColorBrush text,
            SolidColorBrush accent)
        {
            Theme = theme;
            BackgroundBrush = background;
            SecondaryBrush = secondary;
            TertiaryBrush = tertiary;
            TextBrush = text;
            AccentBrush = accent;
        }
    }
}



