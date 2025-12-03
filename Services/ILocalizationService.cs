using System;
using System.ComponentModel;

namespace Grex.Services
{
    /// <summary>
    /// Interface for providing localization services in Grex
    /// </summary>
    public interface ILocalizationService : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets the current culture
        /// </summary>
        string CurrentCulture { get; }

        /// <summary>
        /// Gets a localized string by key
        /// </summary>
        /// <param name="key">The resource key</param>
        /// <returns>The localized string or the key if not found</returns>
        string GetLocalizedString(string key);

        /// <summary>
        /// Gets a localized string by key with string formatting
        /// </summary>
        /// <param name="key">The resource key</param>
        /// <param name="args">Format arguments</param>
        /// <returns>The formatted localized string or the key if not found</returns>
        string GetLocalizedString(string key, params object[] args);

        /// <summary>
        /// Sets the current culture
        /// </summary>
        /// <param name="culture">The culture code (e.g., "en-US")</param>
        void SetCulture(string culture);
    }
}