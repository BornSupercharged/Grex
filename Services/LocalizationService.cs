using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace Grex.Services
{
    /// <summary>
    /// Provides localization services for Grex
    /// </summary>
    public class LocalizationService : ILocalizationService
    {
        private static LocalizationService? _instance;
        private readonly ResourceManager? _resourceManager;
        private readonly ResourceMap? _resourceMap;
        private readonly Dictionary<string, ResourceContext> _resourceContexts;
        private readonly object _resourceContextLock = new();
        private string _currentCulture = DefaultCulture;
        private const string DefaultCulture = "en-US";

        /// <summary>
        /// Gets the singleton instance of the LocalizationService
        /// </summary>
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        /// <summary>
        /// Gets the current culture
        /// </summary>
        public string CurrentCulture
        {
            get => _currentCulture;
            private set
            {
                if (_currentCulture != value)
                {
                    _currentCulture = value;
                    OnPropertyChanged(nameof(CurrentCulture));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private LocalizationService()
        {
            try
            {
                try
                {
                    _resourceManager = new ResourceManager();
                    _resourceMap = _resourceManager.MainResourceMap?.TryGetSubtree("Resources") ?? _resourceManager.MainResourceMap;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"LocalizationService: ResourceManager unavailable: {ex.Message}");
                    _resourceManager = null;
                    _resourceMap = null;
                }
                _resourceContexts = new Dictionary<string, ResourceContext>(StringComparer.OrdinalIgnoreCase);
                
                // Initialize with system culture or default to en-US
                // Wrap in try-catch to handle any culture-related issues
                try
                {
                    var systemCulture = CultureInfo.CurrentUICulture.Name;
                    CurrentCulture = IsValidCulture(systemCulture) ? systemCulture : DefaultCulture;
                }
                catch
                {
                    // If culture detection fails, use default
                    CurrentCulture = DefaultCulture;
                }
                
                // Don't preload resources in constructor - load them on-demand
                // This prevents crashes during app startup if resources aren't available yet
            }
            catch
            {
                // If anything fails in constructor, initialize with safe defaults
                try
                {
                    _resourceManager = new ResourceManager();
                    _resourceMap = _resourceManager.MainResourceMap?.TryGetSubtree("Resources") ?? _resourceManager.MainResourceMap;
                }
                catch
                {
                    _resourceManager = null;
                    _resourceMap = null;
                }
                _resourceContexts = new Dictionary<string, ResourceContext>(StringComparer.OrdinalIgnoreCase);
                CurrentCulture = DefaultCulture;
            }
        }

        /// <summary>
        /// Initializes the localization service
        /// </summary>
        public void Initialize()
        {
            // Initialization is done in constructor
        }

        /// <summary>
        /// Gets a localized string by key
        /// </summary>
        /// <param name="key">The resource key</param>
        /// <returns>The localized string or the key if not found</returns>
        public string GetLocalizedString(string key)
        {
            // Return immediately if key is invalid - don't even try to access resources
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            // Wrap everything in try-catch to prevent any crashes
            try
            {
                var localizedString = GetStringForCulture(key, CurrentCulture);
                
                // If not found, try the default culture
                if (string.IsNullOrEmpty(localizedString) && CurrentCulture != DefaultCulture)
                {
                    localizedString = GetStringForCulture(key, DefaultCulture);
                }
                
                // If still not found, return the key as fallback
                return string.IsNullOrEmpty(localizedString) ? key : localizedString;
            }
            catch
            {
                // Return the key as fallback - don't let any exception propagate
                return key;
            }
        }

        /// <summary>
        /// Gets a localized string by key with string formatting
        /// </summary>
        /// <param name="key">The resource key</param>
        /// <param name="args">Format arguments</param>
        /// <returns>The formatted localized string or the key if not found</returns>
        public string GetLocalizedString(string key, params object[] args)
        {
            var format = GetLocalizedString(key);
            try
            {
                return args?.Length > 0 ? string.Format(format, args) : format;
            }
            catch (FormatException)
            {
                // Return the unformatted string if formatting fails
                return format;
            }
        }

        /// <summary>
        /// Sets the current culture
        /// </summary>
        /// <param name="culture">The culture code (e.g., "en-US")</param>
        public void SetCulture(string culture)
        {
            if (string.IsNullOrEmpty(culture))
                return;

            if (!IsValidCulture(culture))
            {
                // Fall back to default culture
                culture = DefaultCulture;
            }

            if (CurrentCulture != culture)
            {
                CurrentCulture = culture;
                
                // Update the current thread's culture
                var cultureInfo = new CultureInfo(culture);
                Thread.CurrentThread.CurrentCulture = cultureInfo;
                Thread.CurrentThread.CurrentUICulture = cultureInfo;
                CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
                CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

                try
                {
                    ApplicationLanguages.PrimaryLanguageOverride = culture;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"LocalizationService: Failed to set PrimaryLanguageOverride: {ex.Message}");
                }
                
                // Clear resource context cache to force reload with new culture
                ClearResourceContextCache();
            }
        }

        /// <summary>
        /// Clears the resource context cache to force reload with new culture
        /// </summary>
        private void ClearResourceContextCache()
        {
            lock (_resourceContextLock)
            {
                _resourceContexts.Clear();
            }
        }

        /// <summary>
        /// Updates the default resource context for the application
        /// </summary>
        private void UpdateDefaultResourceContext(string culture)
        {
            try
            {
                if (_resourceManager == null)
                    return;

                // Get or create the default resource context
                var defaultContext = _resourceManager.CreateResourceContext();
                defaultContext.QualifierValues["Language"] = culture;
                
                // Note: WinUI 3 doesn't have a direct way to set a "default" context
                // that affects x:Uid resources. The resources are resolved at XAML load time.
                // We need to manually update UI elements or reload the XAML.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateDefaultResourceContext error: {ex.Message}");
            }
        }

        private static bool IsValidCulture(string culture)
        {
            try
            {
                return !string.IsNullOrEmpty(culture) && 
                       CultureInfo.GetCultureInfo(culture) != null;
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string? GetStringForCulture(string key, string culture)
        {
            try
            {
                if (_resourceMap == null || _resourceManager == null)
                    return null;

                var context = GetResourceContext(culture);
                var keyVariants = BuildKeyVariants(key);

                foreach (var variant in keyVariants)
                {
                    var candidate = TryGetCandidate(_resourceMap, variant, context);
                    if (candidate != null)
                        return candidate.ValueAsString;

                    candidate = TryGetCandidate(_resourceManager.MainResourceMap, $"Resources/{variant}", context);
                    if (candidate != null)
                        return candidate.ValueAsString;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalizationService: Failed to get resource '{key}' for culture '{culture}': {ex.Message}");
                return null;
            }
        }

        private static IEnumerable<string> BuildKeyVariants(string key)
        {
            if (string.IsNullOrEmpty(key))
                yield break;

            yield return key;

            if (key.Contains('.'))
            {
                yield return key.Replace('.', '/');
                yield return key.Replace('.', '_');
            }
        }

        private static ResourceCandidate? TryGetCandidate(ResourceMap? map, string key, ResourceContext context)
        {
            if (map == null || string.IsNullOrEmpty(key))
                return null;

            try
            {
                return map.GetValue(key, context);
            }
            catch
            {
                return null;
            }
        }

        private ResourceContext GetResourceContext(string culture)
        {
            if (string.IsNullOrEmpty(culture) || !IsValidCulture(culture))
            {
                culture = DefaultCulture;
            }

            lock (_resourceContextLock)
            {
            if (_resourceManager == null)
                throw new InvalidOperationException("ResourceManager is not available in this context.");

            if (_resourceContexts.TryGetValue(culture, out var context))
                    return context;

                var newContext = _resourceManager.CreateResourceContext();
                newContext.QualifierValues["Language"] = culture;
                _resourceContexts[culture] = newContext;
                return newContext;
            }
        }
    }
}