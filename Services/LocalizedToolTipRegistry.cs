using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Grex.Services
{
    /// <summary>
    /// Central registry that applies localized tooltips to framework elements and keeps them updated
    /// whenever the application language changes.
    /// </summary>
    public static class LocalizedToolTipRegistry
    {
        private sealed class ToolTipRegistration
        {
            public ToolTipRegistration(FrameworkElement element, string resourceKey)
            {
                Element = new WeakReference<FrameworkElement>(element);
                ResourceKey = resourceKey;
            }

            public WeakReference<FrameworkElement> Element { get; }

            public string ResourceKey { get; set; }
        }

        private static readonly object _syncRoot = new();
        private static readonly List<ToolTipRegistration> _registrations = new();
        private static bool _subscribedToLocalization;

        /// <summary>
        /// Registers a framework element so its tooltip text is sourced from the specified localization key.
        /// </summary>
        /// <param name="element">The UI element to decorate.</param>
        /// <param name="resourceKey">The resource key to resolve via <see cref="LocalizationService"/>.</param>
        public static void Register(FrameworkElement? element, string resourceKey)
        {
            if (element == null || string.IsNullOrWhiteSpace(resourceKey))
            {
                return;
            }

            lock (_syncRoot)
            {
                EnsureSubscribed();

                var existing = _registrations
                    .FirstOrDefault(registration =>
                        registration.Element.TryGetTarget(out var target) && ReferenceEquals(target, element));

                if (existing != null)
                {
                    existing.ResourceKey = resourceKey;
                }
                else
                {
                    _registrations.Add(new ToolTipRegistration(element, resourceKey));
                }
            }

            ApplyToolTip(element, resourceKey);
        }

        /// <summary>
        /// Forces all registered tooltips to refresh using the current culture.
        /// </summary>
        public static void RefreshRegisteredToolTips()
        {
            List<ToolTipRegistration> snapshot;

            lock (_syncRoot)
            {
                CleanupDeadRegistrationsLocked();
                snapshot = _registrations.ToList();
            }

            foreach (var registration in snapshot)
            {
                if (registration.Element.TryGetTarget(out var element))
                {
                    ApplyToolTip(element, registration.ResourceKey);
                }
            }
        }

        private static void EnsureSubscribed()
        {
            if (_subscribedToLocalization)
            {
                return;
            }

            _subscribedToLocalization = true;
            LocalizationService.Instance.PropertyChanged += LocalizationServiceOnPropertyChanged;
        }

        private static void LocalizationServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LocalizationService.CurrentCulture))
            {
                RefreshRegisteredToolTips();
            }
        }

        private static void ApplyToolTip(FrameworkElement element, string resourceKey)
        {
            try
            {
                var locService = LocalizationService.Instance;
                var localizedText = locService.GetLocalizedString(resourceKey);

                if (string.IsNullOrWhiteSpace(localizedText) || localizedText == resourceKey)
                {
                    localizedText = resourceKey;
                }

                SetToolTipText(element, localizedText);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalizedToolTipRegistry.ApplyToolTip error: {ex}");
            }
        }

        private static void SetToolTipText(FrameworkElement element, string text)
        {
            if (element.DispatcherQueue != null && !element.DispatcherQueue.HasThreadAccess)
            {
                element.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => ApplyToolTipValues(element, text));
            }
            else
            {
                ApplyToolTipValues(element, text);
            }
        }

        private static void ApplyToolTipValues(FrameworkElement element, string text)
        {
            try
            {
                ToolTipService.SetToolTip(element, text);
                AutomationProperties.SetHelpText(element, text);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalizedToolTipRegistry.ApplyToolTipValues error: {ex}");
            }
        }

        private static void CleanupDeadRegistrationsLocked()
        {
            for (var i = _registrations.Count - 1; i >= 0; i--)
            {
                if (!_registrations[i].Element.TryGetTarget(out _))
                {
                    _registrations.RemoveAt(i);
                }
            }
        }
    }
}

