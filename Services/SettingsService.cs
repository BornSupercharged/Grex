using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Grex.Models;

namespace Grex.Services
{
    public enum ThemePreference
    {
        System,
        Light,
        Dark,
        BlackKnight,
        Paranoid,
        Diamond,
        Subspace,
        RedVelvet,
        Dreams,
        Tiefling,
        Vibes
    }

    public class DefaultSettings
    {
        public bool IsRegexSearch { get; set; } = false;
        public bool IsFilesSearch { get; set; } = false;
        public bool RespectGitignore { get; set; } = false;
        public bool SearchCaseSensitive { get; set; } = false;
        public bool IncludeSystemFiles { get; set; } = false;
        public bool IncludeSubfolders { get; set; } = true;
        public bool IncludeHiddenItems { get; set; } = false;
        public bool IncludeBinaryFiles { get; set; } = false;
        public bool IncludeSymbolicLinks { get; set; } = false;
        public bool UseWindowsSearchIndex { get; set; } = false;
        public bool EnableDockerSearch { get; set; } = false;
        public Models.SizeUnit SizeUnit { get; set; } = Models.SizeUnit.KB;
        public ThemePreference ThemePreference { get; set; } = ThemePreference.System;
        public string UILanguage { get; set; } = "en-US"; // Default to English (United States)
        
        // Culture-aware string comparison settings
        public Models.StringComparisonMode StringComparisonMode { get; set; } = Models.StringComparisonMode.Ordinal;
        public Models.UnicodeNormalizationMode UnicodeNormalizationMode { get; set; } = Models.UnicodeNormalizationMode.None;
        public bool DiacriticSensitive { get; set; } = true;
        public string Culture { get; set; } = CultureInfo.CurrentCulture.Name;
        
        // Default filter values
        public string DefaultMatchFiles { get; set; } = string.Empty;
        public string DefaultExcludeDirs { get; set; } = string.Empty;
        
        // Content table column visibility
        public bool ContentLineColumnVisible { get; set; } = true;
        public bool ContentColumnColumnVisible { get; set; } = true;
        public bool ContentPathColumnVisible { get; set; } = true;
        
        // Files table column visibility
        public bool FilesSizeColumnVisible { get; set; } = true;
        public bool FilesMatchesColumnVisible { get; set; } = true;
        public bool FilesPathColumnVisible { get; set; } = true;
        public bool FilesExtColumnVisible { get; set; } = true;
        public bool FilesEncodingColumnVisible { get; set; } = true;
        public bool FilesDateModifiedColumnVisible { get; set; } = true;
        
        // Window position and size
        public int? WindowX { get; set; } = null;
        public int? WindowY { get; set; } = null;
        public int? WindowWidth { get; set; } = 1100;
	public int? WindowHeight { get; set; } = 700;
    }

    public static class SettingsService
    {
        public static event EventHandler<bool>? DockerSearchEnabledChanged;
        
        private static readonly string DefaultSettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Grex",
            "settings.json");

        private static string? _settingsFilePathOverride;

        private static DefaultSettings? _cachedSettings;
        private static readonly object _lock = new object();

        private static string GetSettingsFilePath()
        {
            if (!string.IsNullOrWhiteSpace(_settingsFilePathOverride))
            {
                return _settingsFilePathOverride!;
            }
            return DefaultSettingsFilePath;
        }

        private static DefaultSettings LoadSettings()
        {
            lock (_lock)
            {
                if (_cachedSettings != null)
                    return _cachedSettings;

                try
                {
                    var settingsFilePath = GetSettingsFilePath();
                    if (File.Exists(settingsFilePath))
                    {
                        var json = File.ReadAllText(settingsFilePath);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                            AllowTrailingCommas = true
                        };
                        _cachedSettings = JsonSerializer.Deserialize<DefaultSettings>(json, options) ?? new DefaultSettings();
                    }
                    else
                    {
                        _cachedSettings = new DefaultSettings();
                    }
                }
                catch
                {
                    _cachedSettings = new DefaultSettings();
                }

                return _cachedSettings;
            }
        }

        private static void SaveSettings(DefaultSettings settings)
        {
            lock (_lock)
            {
                try
                {
                    var settingsFilePath = GetSettingsFilePath();
                    var directory = Path.GetDirectoryName(settingsFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(settingsFilePath, json);
                    _cachedSettings = settings;
                }
                catch
                {
                    // Ignore save errors
                }
            }
        }

        public static DefaultSettings GetDefaultSettings()
        {
            return LoadSettings();
        }

        public static void SetDefaultIsRegexSearch(bool value)
        {
            var settings = LoadSettings();
            settings.IsRegexSearch = value;
            SaveSettings(settings);
        }

        public static void SetDefaultIsFilesSearch(bool value)
        {
            var settings = LoadSettings();
            settings.IsFilesSearch = value;
            SaveSettings(settings);
        }

        public static void SetDefaultRespectGitignore(bool value)
        {
            var settings = LoadSettings();
            settings.RespectGitignore = value;
            SaveSettings(settings);
        }

        public static void SetDefaultSearchCaseSensitive(bool value)
        {
            var settings = LoadSettings();
            settings.SearchCaseSensitive = value;
            SaveSettings(settings);
        }

        public static void SetDefaultIncludeSystemFiles(bool value)
        {
            var settings = LoadSettings();
            settings.IncludeSystemFiles = value;
            SaveSettings(settings);
        }

        public static void SetDefaultIncludeSubfolders(bool value)
        {
            var settings = LoadSettings();
            settings.IncludeSubfolders = value;
            SaveSettings(settings);
        }

        public static void SetDefaultIncludeHiddenItems(bool value)
        {
            var settings = LoadSettings();
            settings.IncludeHiddenItems = value;
            SaveSettings(settings);
        }

        public static void SetDefaultIncludeBinaryFiles(bool value)
        {
            var settings = LoadSettings();
            settings.IncludeBinaryFiles = value;
            SaveSettings(settings);
        }

        public static void SetDefaultIncludeSymbolicLinks(bool value)
        {
            var settings = LoadSettings();
            settings.IncludeSymbolicLinks = value;
            SaveSettings(settings);
        }

        public static void SetDefaultUseWindowsSearchIndex(bool value)
        {
            var settings = LoadSettings();
            settings.UseWindowsSearchIndex = value;
            SaveSettings(settings);
        }

        public static bool GetEnableDockerSearch()
        {
            var settings = LoadSettings();
            return settings.EnableDockerSearch;
        }

        public static void SetEnableDockerSearch(bool value)
        {
            var settings = LoadSettings();
            if (settings.EnableDockerSearch == value)
                return;

            settings.EnableDockerSearch = value;
            SaveSettings(settings);
            DockerSearchEnabledChanged?.Invoke(null, value);
        }

        public static void SetDefaultSizeUnit(Models.SizeUnit value)
        {
            var settings = LoadSettings();
            settings.SizeUnit = value;
            SaveSettings(settings);
        }

        public static void SetDefaultContentLineColumnVisible(bool value)
        {
            var settings = LoadSettings();
            settings.ContentLineColumnVisible = value;
            SaveSettings(settings);
        }

        public static void SetDefaultContentColumnColumnVisible(bool value)
        {
            var settings = LoadSettings();
            settings.ContentColumnColumnVisible = value;
            SaveSettings(settings);
        }

        public static void SetDefaultContentPathColumnVisible(bool value)
        {
            var settings = LoadSettings();
            settings.ContentPathColumnVisible = value;
            SaveSettings(settings);
        }

        public static void SetDefaultFilesSizeColumnVisible(bool value)
        {
            var settings = LoadSettings();
            settings.FilesSizeColumnVisible = value;
            SaveSettings(settings);
        }

        public static void SetDefaultFilesMatchesColumnVisible(bool value)
        {
            var settings = LoadSettings();
            settings.FilesMatchesColumnVisible = value;
            SaveSettings(settings);
        }

        public static void SetDefaultFilesPathColumnVisible(bool value)
        {
            var settings = LoadSettings();
            settings.FilesPathColumnVisible = value;
            SaveSettings(settings);
        }

        public static void SetDefaultFilesExtColumnVisible(bool value)
        {
            var settings = LoadSettings();
            settings.FilesExtColumnVisible = value;
            SaveSettings(settings);
        }

        public static void SetDefaultFilesEncodingColumnVisible(bool value)
        {
            var settings = LoadSettings();
            settings.FilesEncodingColumnVisible = value;
            SaveSettings(settings);
        }

        public static void SetDefaultFilesDateModifiedColumnVisible(bool value)
        {
            var settings = LoadSettings();
            settings.FilesDateModifiedColumnVisible = value;
            SaveSettings(settings);
        }

        public static void SetWindowPosition(int x, int y, int width, int height)
        {
            var settings = LoadSettings();
            settings.WindowX = x;
            settings.WindowY = y;
            settings.WindowWidth = width;
            settings.WindowHeight = height;
            SaveSettings(settings);
        }

        public static (int? x, int? y, int? width, int? height) GetWindowPosition()
        {
            var settings = LoadSettings();
            return (settings.WindowX, settings.WindowY, settings.WindowWidth, settings.WindowHeight);
        }

        public static ThemePreference GetThemePreference()
        {
            var settings = LoadSettings();
            return settings.ThemePreference;
        }

        public static void SetThemePreference(ThemePreference value)
        {
            var settings = LoadSettings();
            settings.ThemePreference = value;
            SaveSettings(settings);
        }

        public static void SetDefaultStringComparisonMode(Models.StringComparisonMode value)
        {
            var settings = LoadSettings();
            settings.StringComparisonMode = value;
            SaveSettings(settings);
        }

        public static void SetDefaultUnicodeNormalizationMode(Models.UnicodeNormalizationMode value)
        {
            var settings = LoadSettings();
            settings.UnicodeNormalizationMode = value;
            SaveSettings(settings);
        }

        public static void SetDefaultDiacriticSensitive(bool value)
        {
            var settings = LoadSettings();
            settings.DiacriticSensitive = value;
            SaveSettings(settings);
        }

        public static void SetDefaultCulture(string value)
        {
            var settings = LoadSettings();
            settings.Culture = value;
            SaveSettings(settings);
        }

        public static void SetDefaultMatchFiles(string value)
        {
            var settings = LoadSettings();
            settings.DefaultMatchFiles = value ?? string.Empty;
            SaveSettings(settings);
        }

        public static void SetDefaultExcludeDirs(string value)
        {
            var settings = LoadSettings();
            settings.DefaultExcludeDirs = value ?? string.Empty;
            SaveSettings(settings);
        }

        public static string GetUILanguage()
        {
            var settings = LoadSettings();
            return settings.UILanguage ?? string.Empty;
        }

        public static void SetUILanguage(string value)
        {
            var settings = LoadSettings();
            settings.UILanguage = value ?? string.Empty;
            SaveSettings(settings);
        }

        public static void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedSettings = null;
            }
        }

        public static void SetSettingsFilePathOverride(string? customPath)
        {
            lock (_lock)
            {
                _settingsFilePathOverride = customPath;
                _cachedSettings = null;
            }
        }

        /// <summary>
        /// Export current settings as a JSON string.
        /// </summary>
        public static string ExportSettingsAsJson()
        {
            var settings = LoadSettings();
            return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Import settings from a JSON string, merging with existing settings.
        /// Returns (success, error message if failed).
        /// </summary>
        public static (bool Success, string? ErrorMessage) ImportSettingsFromJson(string json)
        {
            lock (_lock)
            {
                try
                {
                    // First, try to parse the JSON to validate it
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };

                    var importedSettings = JsonSerializer.Deserialize<DefaultSettings>(json, options);
                    if (importedSettings == null)
                    {
                        return (false, "Invalid settings file format.");
                    }

                    // Load current settings (or create new if none exist)
                    var currentSettings = LoadSettings();
                    
                    // Merge the imported settings with the current settings
                    // Only copy properties that have valid values
                    currentSettings.IsRegexSearch = importedSettings.IsRegexSearch;
                    currentSettings.IsFilesSearch = importedSettings.IsFilesSearch;
                    currentSettings.RespectGitignore = importedSettings.RespectGitignore;
                    currentSettings.SearchCaseSensitive = importedSettings.SearchCaseSensitive;
                    currentSettings.IncludeSystemFiles = importedSettings.IncludeSystemFiles;
                    currentSettings.IncludeSubfolders = importedSettings.IncludeSubfolders;
                    currentSettings.IncludeHiddenItems = importedSettings.IncludeHiddenItems;
                    currentSettings.IncludeBinaryFiles = importedSettings.IncludeBinaryFiles;
                    currentSettings.IncludeSymbolicLinks = importedSettings.IncludeSymbolicLinks;
                    currentSettings.UseWindowsSearchIndex = importedSettings.UseWindowsSearchIndex;
                    currentSettings.SizeUnit = importedSettings.SizeUnit;
                    currentSettings.ThemePreference = importedSettings.ThemePreference;
                    
                    if (!string.IsNullOrEmpty(importedSettings.UILanguage))
                    {
                        currentSettings.UILanguage = importedSettings.UILanguage;
                    }
                    
                    currentSettings.StringComparisonMode = importedSettings.StringComparisonMode;
                    currentSettings.UnicodeNormalizationMode = importedSettings.UnicodeNormalizationMode;
                    currentSettings.DiacriticSensitive = importedSettings.DiacriticSensitive;
                    
                    if (!string.IsNullOrEmpty(importedSettings.Culture))
                    {
                        currentSettings.Culture = importedSettings.Culture;
                    }
                    
                    // Content table column visibility
                    currentSettings.ContentLineColumnVisible = importedSettings.ContentLineColumnVisible;
                    currentSettings.ContentColumnColumnVisible = importedSettings.ContentColumnColumnVisible;
                    currentSettings.ContentPathColumnVisible = importedSettings.ContentPathColumnVisible;
                    
                    // Files table column visibility
                    currentSettings.FilesSizeColumnVisible = importedSettings.FilesSizeColumnVisible;
                    currentSettings.FilesMatchesColumnVisible = importedSettings.FilesMatchesColumnVisible;
                    currentSettings.FilesPathColumnVisible = importedSettings.FilesPathColumnVisible;
                    currentSettings.FilesExtColumnVisible = importedSettings.FilesExtColumnVisible;
                    currentSettings.FilesEncodingColumnVisible = importedSettings.FilesEncodingColumnVisible;
                    currentSettings.FilesDateModifiedColumnVisible = importedSettings.FilesDateModifiedColumnVisible;
                    
                    // Note: We intentionally do NOT import window position/size
                    // as this is machine-specific and may not work well on other displays

                    // Save the merged settings
                    SaveSettings(currentSettings);
                    _cachedSettings = null; // Invalidate cache

                    return (true, null);
                }
                catch (JsonException ex)
                {
                    return (false, $"Invalid JSON format: {ex.Message}");
                }
                catch (Exception ex)
                {
                    return (false, $"Error importing settings: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Delete the settings file to restore defaults.
        /// </summary>
        public static void DeleteSettingsFile()
        {
            lock (_lock)
            {
                try
                {
                    var settingsFilePath = GetSettingsFilePath();
                    if (File.Exists(settingsFilePath))
                    {
                        File.Delete(settingsFilePath);
                    }
                    _cachedSettings = null;
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }
    }
}

