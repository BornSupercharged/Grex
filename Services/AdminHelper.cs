using System;
using System.IO;
using System.Security.Principal;

namespace Grex.Services
{
    /// <summary>
    /// Helper class for detecting administrator privileges.
    /// </summary>
    public static class AdminHelper
    {
        /// <summary>
        /// Checks if the current process is running with administrator privileges.
        /// </summary>
        public static bool IsRunAsAdmin()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        private static void LogToFile(string message)
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
    }
}

