using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Grex.Services;

namespace Grex
{
    public partial class App : Application
    {
        private static readonly string LogFile = Path.Combine(Path.GetTempPath(), "Grex.log");

        public App()
        {
            try
            {
                Log("App constructor: Starting");
                // WinUI 3 apps don't need InitializeComponent for App class
                Log("App constructor: Initialization completed");
                
                // Subscribe to unhandled exceptions
                this.UnhandledException += App_UnhandledException;
                Log("App constructor: UnhandledException handler registered");
            }
            catch (Exception ex)
            {
                Log($"App constructor ERROR: {ex}");
                throw;
            }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                Log("OnLaunched: Starting");

                // Initialize Windows notification infrastructure (if supported)
                NotificationService.Instance.Initialize();
                Log("OnLaunched: NotificationService initialized");
                
                // Initialize localization service (lazy initialization - resources loaded on-demand)
                // Access the instance to ensure it's created, but don't fail if resources aren't available
                try
                {
                    Log("OnLaunched: Attempting to access LocalizationService.Instance");
                    var localizationService = Services.LocalizationService.Instance;
                    Log("OnLaunched: LocalizationService instance created successfully");
                    
                    // Load saved UI language from settings (Bug 2 fix)
                    var savedUILanguage = SettingsService.GetUILanguage();
                    if (!string.IsNullOrEmpty(savedUILanguage))
                    {
                        Log($"OnLaunched: Loading saved UI language: {savedUILanguage}");
                        localizationService.SetCulture(savedUILanguage);
                    }
                    
                    localizationService.Initialize();
                    Log("OnLaunched: LocalizationService initialized");
                }
                catch (Exception ex)
                {
                    Log($"OnLaunched: LocalizationService initialization failed (non-fatal): {ex.Message}");
                    Log($"OnLaunched: LocalizationService exception type: {ex.GetType().Name}");
                    Log($"OnLaunched: LocalizationService stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Log($"OnLaunched: LocalizationService inner exception: {ex.InnerException.Message}");
                        Log($"OnLaunched: LocalizationService inner stack trace: {ex.InnerException.StackTrace}");
                    }
                    // Continue - localization will fall back to keys if resources aren't available
                }
                
                Log("OnLaunched: Creating MainWindow");
                m_window = new MainWindow();
                MainWindowInstance = m_window as MainWindow;
                Log("OnLaunched: MainWindow created");
                
                // Apply theme preference from settings after window is created
                ApplyThemeFromSettings();
                
                // Bug 1 fix: Refresh all views after window is created to apply saved UI language
                if (MainWindowInstance != null)
                {
                    MainWindowInstance.RefreshLocalization();
                }
                
                m_window.Activate();
                Log("OnLaunched: Window activated");
                
                // Window title localization is handled in MainWindow_Loaded event
                // This ensures the window is fully initialized before attempting localization
                
                // Check command line arguments
                var arguments = Environment.GetCommandLineArgs();
                if (arguments.Length > 1 && arguments[1] == "/settings")
                {
                    if (MainWindowInstance != null)
                    {
                        // This needs to be handled by MainWindow to switch to the Settings tab
                        // We'll use a small delay to ensure the window is fully ready
                        m_window.DispatcherQueue.TryEnqueue(async () => 
                        {
                            await Task.Delay(100);
                            MainWindowInstance.NavigateToSettings();
                        });
                    }
                }

                // Check if running as administrator and show warning after window is activated
                if (AdminHelper.IsRunAsAdmin())
                {
                    Log("OnLaunched: Application is running as administrator");
                    // Show warning dialog after window is activated
                    _ = ShowAdminWarningDialogAsync();
                }
            }
            catch (Exception ex)
            {
                Log($"OnLaunched ERROR: {ex}");
                Log($"OnLaunched ERROR StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Log($"OnLaunched ERROR InnerException: {ex.InnerException}");
                    Log($"OnLaunched ERROR InnerException StackTrace: {ex.InnerException.StackTrace}");
                }
                // Show notification before re-throwing
                try
                {
                    NotificationService.Instance.ShowError(
                        GetString("ApplicationStartupErrorTitle"),
                        GetString("ApplicationStartupErrorMessage", ex.Message));
                }
                catch
                {
                    // If notification fails, at least we logged it
                }
                // Re-throw to see the error
                throw;
            }
        }

        private void ApplyThemeFromSettings()
        {
            try
            {
                var preference = Services.SettingsService.GetThemePreference();
                
                // Apply theme through MainWindow instead of Application.RequestedTheme
                // This avoids COMException when setting RequestedTheme before window is activated
                if (MainWindowInstance != null)
                {
                    MainWindowInstance.ApplyThemeAndBackdrop(preference);
                    Log($"ApplyThemeFromSettings: Applied theme via MainWindow (preference: {preference})");
                }
                else
                {
                    // Fallback: try to set Application theme if window not available yet
                    // This should rarely happen, but handle it gracefully
                    ApplicationTheme theme;
                    switch (preference)
                    {
                        case Services.ThemePreference.Light:
                            theme = ApplicationTheme.Light;
                            break;
                        case Services.ThemePreference.Dark:
                            theme = ApplicationTheme.Dark;
                            break;
                        default: // System
                            // Get system theme from Windows settings
                            var uiSettings = new Windows.UI.ViewManagement.UISettings();
                            var systemTheme = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background).ToString();
                            // If background is dark (low brightness), system is in dark mode
                            theme = systemTheme.StartsWith("#FF2") || systemTheme.StartsWith("#FF1") || systemTheme.StartsWith("#FF0")
                                ? ApplicationTheme.Dark
                                : ApplicationTheme.Light;
                            break;
                    }
                    
                    // Only set RequestedTheme if window is activated
                    if (m_window != null)
                    {
                        this.RequestedTheme = theme;
                        Log($"ApplyThemeFromSettings: Applied theme {theme} via Application.RequestedTheme (preference: {preference})");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"ApplyThemeFromSettings ERROR: {ex}");
                // Continue with default theme
            }
        }

        private async Task ShowAdminWarningDialogAsync()
        {
            try
            {
                // Wait a bit for window to be fully activated and rendered
                await Task.Delay(300);

                if (m_window == null || MainWindowInstance == null)
                {
                    Log("ShowAdminWarningDialogAsync: Window not available, cannot show dialog");
                    return;
                }

                var textBlock = new Microsoft.UI.Xaml.Controls.TextBlock
                {
                    Text = GetString("AdminWarningMessage"),
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                    MaxWidth = 520
                };

                var dialog = new ContentDialog
                {
                    Title = GetString("AdminWarningTitle"),
                    Content = textBlock,
                    PrimaryButtonText = GetString("AdminWarningIgnoreButton"),
                    SecondaryButtonText = GetString("AdminWarningrexitButton"),
                    XamlRoot = m_window.Content.XamlRoot,
                    DefaultButton = ContentDialogButton.Primary
                };

                var result = await dialog.ShowAsync();
                
                if (result == ContentDialogResult.Secondary)
                {
                    Log("ShowAdminWarningDialogAsync: User chose to exit");
                    m_window?.Close();
                    Exit();
                }
                else
                {
                    Log("ShowAdminWarningDialogAsync: User chose to continue as administrator");
                }
            }
            catch (Exception ex)
            {
                Log($"ShowAdminWarningDialogAsync ERROR: {ex}");
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log($"App_UnhandledException: {e.Exception}");
            Log($"App_UnhandledException StackTrace: {e.Exception.StackTrace}");
            if (e.Exception.InnerException != null)
            {
                Log($"App_UnhandledException InnerException: {e.Exception.InnerException}");
            }
            
            // Show notification to user
            try
            {
                NotificationService.Instance.ShowError(
                    GetString("UnhandledErrorTitle"),
                    GetString("UnhandledErrorMessage", e.Exception.Message));
            }
            catch
            {
                // If notification fails, at least we logged it
            }
            
            e.Handled = false; // Let it crash so we can see the error
        }

        private Window? m_window;
        
        public static MainWindow? MainWindowInstance { get; private set; }

        private static void Log(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(LogFile, $"[{timestamp}] {message}\n");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        private static string GetString(string key) =>
            Services.LocalizationService.Instance.GetLocalizedString(key);

        private static string GetString(string key, params object[] args) =>
            Services.LocalizationService.Instance.GetLocalizedString(key, args);
    }
}

