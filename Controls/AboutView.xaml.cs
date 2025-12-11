using System;
using System.IO;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Grex.Services;

namespace Grex.Controls
{
    public sealed partial class AboutView : UserControl
    {
        public AboutView()
        {
            this.InitializeComponent();
            LoadAppLogo();
            LoadVersionInfo();
            RefreshLocalization();
            this.Loaded += AboutView_Loaded;
            this.Unloaded += AboutView_Unloaded;
        }
        
        private void AboutView_Loaded(object sender, RoutedEventArgs e)
        {
            // Subscribe to theme changes and apply initial theme
            MainWindow.ThemeChanged += OnThemeChanged;
            
            // Delay theme application to ensure visual tree is fully populated
            DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                ApplyCurrentThemeColors();
            });
        }
        
        private void AboutView_Unloaded(object sender, RoutedEventArgs e)
        {
            MainWindow.ThemeChanged -= OnThemeChanged;
        }
        
        private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            try
            {
                if (DispatcherQueue == null || !DispatcherQueue.TryEnqueue(() => ApplyThemeColors(e)))
                {
                    ApplyThemeColors(e);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnThemeChanged ERROR: {ex}");
            }
        }
        
        private void ApplyCurrentThemeColors()
        {
            try
            {
                var currentTheme = MainWindow.CurrentTheme;
                if (!IsHighContrastTheme(currentTheme))
                {
                    ClearHighContrastColors();
                    return;
                }
                
                var colors = MainWindow.GetCurrentThemeColors();
                ApplyThemeColors(new ThemeChangedEventArgs(currentTheme, colors.background, colors.secondary, colors.tertiary, colors.text, colors.accent));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyCurrentThemeColors ERROR: {ex}");
            }
        }
        
        public void ApplyThemeFromHost(ThemeChangedEventArgs e)
        {
            ApplyThemeColors(e);
        }
        
        private static bool IsHighContrastTheme(Services.ThemePreference preference)
        {
            return preference == Services.ThemePreference.BlackKnight ||
                   preference == Services.ThemePreference.Paranoid ||
                   preference == Services.ThemePreference.Diamond ||
                   preference == Services.ThemePreference.Subspace ||
                   preference == Services.ThemePreference.RedVelvet ||
                   preference == Services.ThemePreference.Dreams ||
                   preference == Services.ThemePreference.Tiefling ||
                   preference == Services.ThemePreference.Vibes;
        }
        
        private void ApplyThemeColors(ThemeChangedEventArgs e)
        {
            try
            {
                if (!IsHighContrastTheme(e.Theme))
                {
                    ClearHighContrastColors();
                    return;
                }
                
                // Apply text color to all TextBlocks
                ApplyForegroundToAllTextBlocks(this, e.TextBrush, e.AccentBrush);
                
                // Apply background
                this.Background = e.BackgroundBrush;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyThemeColors ERROR: {ex}");
            }
        }
        
        private void ApplyForegroundToAllTextBlocks(DependencyObject parent, Microsoft.UI.Xaml.Media.SolidColorBrush foreground, Microsoft.UI.Xaml.Media.SolidColorBrush accent)
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
                    
                    // Clear existing resources and set new ones for all states
                    checkBox.Resources?.Clear();
                    if (checkBox.Resources == null)
                        checkBox.Resources = new Microsoft.UI.Xaml.ResourceDictionary();
                    checkBox.Resources["CheckBoxForeground"] = foreground;
                    checkBox.Resources["CheckBoxForegroundPointerOver"] = foreground;
                    checkBox.Resources["CheckBoxForegroundPressed"] = foreground;
                    checkBox.Resources["CheckBoxForegroundDisabled"] = foreground;
                    checkBox.Resources["CheckBoxCheckGlyphForegroundChecked"] = foreground;
                    checkBox.Resources["CheckBoxCheckGlyphForegroundCheckedPointerOver"] = foreground;
                    checkBox.Resources["CheckBoxCheckGlyphForegroundCheckedPressed"] = foreground;
                    checkBox.Resources["CheckBoxCheckBackgroundFillChecked"] = accent;
                    checkBox.Resources["CheckBoxCheckBackgroundFillCheckedPointerOver"] = accent;
                    checkBox.Resources["CheckBoxCheckBackgroundFillCheckedPressed"] = accent;
                }
                else if (child is Button button)
                {
                    button.Foreground = foreground;
                }
                
                // Recurse into children
                ApplyForegroundToAllTextBlocks(child, foreground, accent);
            }
        }
        
        private void ClearHighContrastColors()
        {
            try
            {
                this.ClearValue(BackgroundProperty);
                
                // Clear resources
                this.Resources?.Clear();
                
                // Reset foreground on all controls
                ClearForegroundFromVisualTree(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearHighContrastColors ERROR: {ex}");
            }
        }
        
        private void ClearForegroundFromVisualTree(DependencyObject parent)
        {
            try
            {
                var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < count; i++)
                {
                    var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                    
                    if (child is TextBlock textBlock)
                    {
                        textBlock.ClearValue(TextBlock.ForegroundProperty);
                    }
                    else if (child is ContentPresenter contentPresenter)
                    {
                        contentPresenter.ClearValue(ContentPresenter.ForegroundProperty);
                    }
                    else if (child is CheckBox checkBox)
                    {
                        checkBox.ClearValue(CheckBox.ForegroundProperty);
                        checkBox.ClearValue(CheckBox.BackgroundProperty);
                        checkBox.Resources?.Clear();
                    }
                    else if (child is Button button)
                    {
                        button.ClearValue(Button.ForegroundProperty);
                        button.ClearValue(Button.BackgroundProperty);
                    }
                    else if (child is ComboBox comboBox)
                    {
                        comboBox.ClearValue(ComboBox.ForegroundProperty);
                        comboBox.ClearValue(ComboBox.BackgroundProperty);
                    }
                    
                    // Recurse into children
                    ClearForegroundFromVisualTree(child);
                }
            }
            catch
            {
                // Ignore errors during visual tree traversal
            }
        }

        private void LoadAppLogo()
        {
            try
            {
                var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Grex.png");
                if (File.Exists(logoPath))
                {
                    AppLogoImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(logoPath));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AboutView: Failed to load logo: {ex.Message}");
            }
        }

        private void LoadVersionInfo()
        {
            try
            {
                var locService = LocalizationService.Instance;
                var versionLabel = locService.GetLocalizedString("AboutVersionLabel.Text");
                
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                if (version != null)
                {
                    VersionTextBlock.Text = $"{versionLabel} {version.Major}.{version.Minor}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AboutView: Failed to load version: {ex.Message}");
                VersionTextBlock.Text = "Version 1.3";
            }
        }

        public void RefreshLocalization()
        {
            try
            {
                var locService = LocalizationService.Instance;
                
                if (CreatedByTextBlock != null)
                {
                    CreatedByTextBlock.Text = locService.GetLocalizedString("AboutCreatedByTextBlock.Text");
                }
                
                if (LicenseTextBlock != null)
                {
                    LicenseTextBlock.Text = locService.GetLocalizedString("AboutLicenseTextBlock.Text");
                }
                
                if (GitHubLinkButton != null)
                {
                    GitHubLinkButton.Content = locService.GetLocalizedString("AboutGitHubLinkButton.Content");
                }
                
                if (KeyboardShortcutTextBlock != null)
                {
                    KeyboardShortcutTextBlock.Text = locService.GetLocalizedString("AboutKeyboardShortcutTextBlock.Text");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AboutView: RefreshLocalization error: {ex.Message}");
            }
        }

        private void HyperlinkButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement element)
            {
                try
                {
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
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

        private void HyperlinkButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement element)
            {
                try
                {
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
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
    }
}

