using System;
using System.IO;
using System.Xml;
using Microsoft.Windows.AppNotifications;

namespace Grex.Services
{
    /// <summary>
    /// Service for displaying toast notifications using WinUI 3's AppNotification functionality.
    /// </summary>
    public class NotificationService
    {
        private static NotificationService? _instance;
        private static readonly object _lock = new object();
        private static readonly object _registrationLock = new object();
        private static readonly string LogFilePath = Path.Combine(Path.GetTempPath(), "Grex.log");

        private bool? _isSupported;
        private string? _supportFailureDetails;
        private bool _registrationAttempted;
        private bool _isRegistered;
        private string? _registrationFailureDetails;

        /// <summary>
        /// Gets the singleton instance of the NotificationService.
        /// </summary>
        public static NotificationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new NotificationService();
                        }
                    }
                }
                return _instance;
            }
        }

        private NotificationService()
        {
            // Private constructor for singleton pattern
        }

        /// <summary>
        /// Shows an error notification to the user.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The error message to display.</param>
        public void ShowError(string title, string message)
        {
            if (!EnsureSupport())
            {
                LogToFile("ShowError: App notifications are not supported on this system. Install/repair the Windows App SDK runtime (Singleton package).");
                return;
            }

            EnsureRegistration();
            if (!_isRegistered)
            {
                LogToFile($"ShowError: App notifications could not be registered. Details: {_registrationFailureDetails ?? "Unknown error"}");
                return;
            }

            LogToFile($"ShowError called: title='{title}', message='{message}'");
            try
            {
                LogToFile("ShowError: Escaping XML");
                var escapedTitle = EscapeXml(title);
                var escapedMessage = EscapeXml(message);
                LogToFile($"ShowError: Escaped title='{escapedTitle}', escaped message='{escapedMessage}'");
                
                var xmlPayload = $@"<toast>
    <visual>
        <binding template=""ToastGeneric"">
            <text>{escapedTitle}</text>
            <text>{escapedMessage}</text>
        </binding>
    </visual>
</toast>";

                LogToFile($"ShowError: XML payload created, length={xmlPayload.Length}");
                var previewLength = xmlPayload.Length > 200 ? 200 : xmlPayload.Length;
                LogToFile($"ShowError: XML payload preview: {xmlPayload.Substring(0, previewLength)}...");

                LogToFile("ShowError: Creating AppNotification object");
                var notification = new AppNotification(xmlPayload);
                LogToFile("ShowError: AppNotification object created successfully");

                LogToFile("ShowError: Getting AppNotificationManager.Default");
                var manager = AppNotificationManager.Default;
                LogToFile("ShowError: AppNotificationManager.Default obtained");

                LogToFile("ShowError: Calling Show(notification)");
                manager.Show(notification);
                LogToFile("ShowError: Show(notification) called successfully - notification should be displayed");
            }
            catch (Exception ex)
            {
                // If notification fails, log to file as fallback
                LogToFile($"NotificationService.ShowError failed: {ex.GetType().Name}: {ex.Message}");
                LogToFile($"NotificationService.ShowError StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    LogToFile($"NotificationService.ShowError InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                LogToFile($"Original error: {title} - {message}");
            }
        }

        /// <summary>
        /// Shows an error notification with exception details.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="exception">The exception that occurred.</param>
        public void ShowError(string title, Exception exception)
        {
            if (exception == null)
            {
                ShowError(title, "An unknown error occurred.");
                return;
            }

            // Truncate long exception messages for display
            var message = exception.Message;
            if (message.Length > 200)
            {
                message = message.Substring(0, 197) + "...";
            }

            ShowError(title, message);
            
            // Always log full exception details to file
            LogToFile($"Error: {title}");
            LogToFile($"Exception: {exception}");
            if (exception.InnerException != null)
            {
                LogToFile($"Inner Exception: {exception.InnerException}");
            }
            LogToFile($"Stack Trace: {exception.StackTrace}");
        }

        /// <summary>
        /// Shows an informational notification to the user.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The message to display.</param>
        public void ShowInfo(string title, string message)
        {
            try
            {
                if (!EnsureSupport())
                    return;

                EnsureRegistration();
                if (!_isRegistered)
                {
                    LogToFile("ShowInfo: Registration unavailable; skipping notification.");
                    return;
                }

                var xmlPayload = $@"<toast>
    <visual>
        <binding template=""ToastGeneric"">
            <text>{EscapeXml(title)}</text>
            <text>{EscapeXml(message)}</text>
        </binding>
    </visual>
</toast>";

                var notification = new AppNotification(xmlPayload);
                AppNotificationManager.Default.Show(notification);
            }
            catch (Exception ex)
            {
                // If notification fails, log to file as fallback
                LogToFile($"NotificationService.ShowInfo failed: {ex.Message}");
                LogToFile($"Original message: {title} - {message}");
            }
        }

        /// <summary>
        /// Shows a warning notification to the user.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The warning message to display.</param>
        public void ShowWarning(string title, string message)
        {
            try
            {
                if (!EnsureSupport())
                    return;

                EnsureRegistration();
                if (!_isRegistered)
                {
                    LogToFile("ShowWarning: Registration unavailable; skipping notification.");
                    return;
                }

                var xmlPayload = $@"<toast>
    <visual>
        <binding template=""ToastGeneric"">
            <text>{EscapeXml(title)}</text>
            <text>{EscapeXml(message)}</text>
        </binding>
    </visual>
</toast>";

                var notification = new AppNotification(xmlPayload);
                AppNotificationManager.Default.Show(notification);
            }
            catch (Exception ex)
            {
                // If notification fails, log to file as fallback
                LogToFile($"NotificationService.ShowWarning failed: {ex.Message}");
                LogToFile($"Original warning: {title} - {message}");
            }
        }

        /// <summary>
        /// Shows a success notification to the user.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The success message to display.</param>
        public void ShowSuccess(string title, string message)
        {
            try
            {
                if (!EnsureSupport())
                    return;

                EnsureRegistration();
                if (!_isRegistered)
                {
                    LogToFile("ShowSuccess: Registration unavailable; skipping notification.");
                    return;
                }

                var xmlPayload = $@"<toast>
    <visual>
        <binding template=""ToastGeneric"">
            <text>{EscapeXml(title)}</text>
            <text>{EscapeXml(message)}</text>
        </binding>
    </visual>
</toast>";

                var notification = new AppNotification(xmlPayload);
                AppNotificationManager.Default.Show(notification);
            }
            catch (Exception ex)
            {
                // If notification fails, log to file as fallback
                LogToFile($"NotificationService.ShowSuccess failed: {ex.Message}");
                LogToFile($"Original message: {title} - {message}");
            }
        }

        /// <summary>
        /// Escapes XML special characters in a string.
        /// </summary>
        private static string EscapeXml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private static void LogToFile(string message)
        {
            try
            {
                var logFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Grex.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                System.IO.File.AppendAllText(logFile, $"[{timestamp}] {message}\n");
            }
            catch
            {
                // Ignore logging errors to prevent infinite loops
            }
        }

        /// <summary>
        /// Initializes notification support/registration (call during app startup).
        /// </summary>
        public void Initialize()
        {
            if (!EnsureSupport())
                return;

            EnsureRegistration();
        }

        /// <summary>
        /// Returns true if the Windows notification infrastructure is available.
        /// Note: For unpackaged apps, this may return false even when notifications work.
        /// Use CheckRegistration() to verify if notifications are actually available.
        /// </summary>
        public bool CheckSupport() => EnsureSupport();

        /// <summary>
        /// Returns true if registration succeeded (will attempt registration if needed).
        /// For unpackaged apps, this will attempt registration even if IsSupported() returns false.
        /// </summary>
        public bool CheckRegistration()
        {
            EnsureRegistration();
            return _isRegistered;
        }

        public string NotificationLogFilePath => LogFilePath;
        public string? SupportFailureDetails => _supportFailureDetails;
        public string? RegistrationFailureDetails => _registrationFailureDetails;

        private bool EnsureSupport()
        {
            if (_isSupported.HasValue)
                return _isSupported.Value;

            try
            {
                var supported = AppNotificationManager.IsSupported();
                _isSupported = supported;
                _supportFailureDetails = supported
                    ? null
                    : "App notifications are not supported. Install or repair the Windows App SDK runtime (Singleton package) or run the Windows App Runtime installer.";
                LogToFile($"NotificationService.EnsureSupport: AppNotificationManager.IsSupported returned {supported}");
                return supported;
            }
            catch (Exception ex)
            {
                _isSupported = false;
                _supportFailureDetails = $"{ex.GetType().Name}: {ex.Message}";
                LogToFile($"NotificationService.EnsureSupport: Exception {ex}");
                return false;
            }
        }

        private void EnsureRegistration()
        {
            if (_registrationAttempted)
                return;

            lock (_registrationLock)
            {
                if (_registrationAttempted)
                    return;

                _registrationAttempted = true;

                // For unpackaged apps, IsSupported() can return false even when notifications work.
                // We'll attempt registration anyway and only fail if registration throws an exception.
                var supportCheck = EnsureSupport();
                if (!supportCheck)
                {
                    LogToFile("NotificationService.EnsureRegistration: IsSupported() returned false, but attempting registration anyway (unpackaged apps may report false incorrectly).");
                }

                try
                {
                    AppNotificationManager.Default.Register();
                    _isRegistered = true;
                    _registrationFailureDetails = null;
                    // If registration succeeded but IsSupported() was false, update support status
                    if (!supportCheck)
                    {
                        _isSupported = true;
                        _supportFailureDetails = null;
                        LogToFile("NotificationService.EnsureRegistration: Registration succeeded despite IsSupported() returning false (common for unpackaged apps).");
                    }
                    else
                    {
                        LogToFile("NotificationService.EnsureRegistration: AppNotificationManager.Register succeeded.");
                    }
                }
                catch (Exception ex)
                {
                    _isRegistered = false;
                    _registrationFailureDetails = $"{ex.GetType().Name}: {ex.Message}";
                    LogToFile($"NotificationService.EnsureRegistration: Register failed - {_registrationFailureDetails}");
                    
                    // If registration failed and IsSupported() was false, provide more detailed error
                    if (!supportCheck)
                    {
                        _registrationFailureDetails = $"Registration failed: {_registrationFailureDetails}. Note: IsSupported() also returned false. Ensure Windows App SDK 1.8 runtime is installed and the app is not running with elevated privileges.";
                    }
                }
            }
        }
    }
}

