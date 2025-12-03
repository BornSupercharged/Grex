using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Grex.Models;
using Grex.Services;
using Windows.UI.ViewManagement;
using Windows.Storage;

namespace Grex.Controls
{
    public sealed partial class SettingsView : UserControl
    {
        private bool _isLoadingSettings = false;
        private bool _areToolTipsRegistered;
        private bool _isUILanguageDropDownOpen = false;
        private bool _isCultureDropDownOpen = false;

        public SettingsView()
        {
            // Set flag BEFORE InitializeComponent to prevent XAML default values from triggering events
            _isLoadingSettings = true;
            this.InitializeComponent();
            RegisterLocalizedToolTips();
            InitializeCultureComboBox();
            InitializeUILanguageComboBox();
            LoadSettings();
            this.Loaded += SettingsView_Loaded;
            
            // Subscribe to localization service culture changes to refresh UI
            LocalizationService.Instance.PropertyChanged += LocalizationService_PropertyChanged;
            
            // Subscribe to dropdown open/close events to allow keyboard navigation within dropdowns
            // This prevents SelectionChanged from triggering full UI refresh while navigating with keyboard
            UILanguageComboBox.DropDownOpened += UILanguageComboBox_DropDownOpened;
            UILanguageComboBox.DropDownClosed += UILanguageComboBox_DropDownClosed;
            CultureComboBox.DropDownOpened += CultureComboBox_DropDownOpened;
            CultureComboBox.DropDownClosed += CultureComboBox_DropDownClosed;
        }

        private void InitializeCultureComboBox()
        {
            // Get all available cultures by checking ResourceManager
            var supportedCultures = GetAvailableCultures();

            CultureComboBox.Items.Clear();
            // DisplayName will automatically use the current thread's UI culture (Bug 1 fix)
            // Since SetCulture updates Thread.CurrentThread.CurrentUICulture, DisplayName will show in the correct language
            
            // Build list of (displayName, cultureCode) for sorting by display name
            var cultureItems = new List<(string DisplayName, string CultureCode)>();
            foreach (var culture in supportedCultures)
            {
                try
                {
                    var cultureInfo = new CultureInfo(culture);
                    // DisplayName respects the current thread's CurrentUICulture, which is set by LocalizationService
                    var displayName = cultureInfo.DisplayName;
                    cultureItems.Add((displayName, culture));
                }
                catch
                {
                    // If culture is invalid, skip it
                }
            }
            
            // Sort alphabetically by display name
            cultureItems.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));
            
            // Add sorted items to ComboBox
            foreach (var (displayName, cultureCode) in cultureItems)
            {
                var item = new ComboBoxItem
                {
                    Content = displayName,
                    Tag = cultureCode
                };
                CultureComboBox.Items.Add(item);
            }
        }
        
        private static List<string> GetAvailableCultures()
        {
            var cultures = new List<string>();
            
            // Try to discover from file system first (for development/debugging)
            var stringsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Strings");
            if (!Directory.Exists(stringsDirectory))
            {
                // Try relative path
                stringsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Strings");
            }
            
            if (Directory.Exists(stringsDirectory))
            {
                var cultureDirs = Directory.GetDirectories(stringsDirectory);
                foreach (var dir in cultureDirs)
                {
                    var dirName = Path.GetFileName(dir);
                    var reswFile = Path.Combine(dir, "Resources.resw");
                    if (File.Exists(reswFile))
                    {
                        try
                        {
                            // Validate it's a valid culture code
                            var cultureInfo = new CultureInfo(dirName);
                            cultures.Add(dirName);
                        }
                        catch
                        {
                            // Skip invalid culture codes
                        }
                    }
                }
            }
            
            // If no cultures found from file system, use comprehensive fallback list
            if (cultures.Count == 0)
            {
                cultures.AddRange(new[]
                {
                    "af-ZA", "am-ET", "ar-SA", "as-IN", "az-AZ", "be-BY", "bg-BG", "bn-BD", "bo-CN",
                    "bs-BA", "ca-ES", "ceb-PH", "cs-CZ", "cy-GB", "da-DK", "de-DE", "el-GR", "en-US",
                    "es-ES", "et-EE", "eu-ES", "fa-IR", "fi-FI", "fil-PH", "fj-FJ", "fr-FR", "ga-IE",
                    "gl-ES", "gu-IN", "ha-NG", "haw-US", "he-IL", "hi-IN", "hr-HR", "hu-HU", "hy-AM",
                    "id-ID", "ig-NG", "is-IS", "it-IT", "ja-JP", "jv-ID", "ka-GE", "kk-KZ", "km-KH",
                    "kn-IN", "ko-KR", "ky-KG", "lb-LU", "lo-LA", "lt-LT", "lv-LV", "mg-MG", "mi-NZ",
                    "mk-MK", "ml-IN", "mn-MN", "mr-IN", "ms-MY", "mt-MT", "my-MM", "ne-NP", "nl-NL",
                    "no-NO", "nr-ZA", "nso-ZA", "or-IN", "pa-IN", "pl-PL", "pt-BR", "pt-PT", "ro-RO",
                    "ru-RU", "rw-RW", "si-LK", "sk-SK", "sl-SI", "sm-WS", "sn-ZW", "so-SO", "sq-AL",
                    "sr-RS", "ss-ZA", "st-ZA", "su-ID", "sv-SE", "sw-KE", "ta-IN", "te-IN", "tg-TJ",
                    "th-TH", "tk-TM", "tn-ZA", "to-TO", "tr-TR", "ts-ZA", "ty-PF", "ug-CN", "uk-UA",
                    "ur-PK", "uz-UZ", "ve-ZA", "vi-VN", "xh-ZA", "yo-NG", "zh-CN", "zh-TW", "zu-ZA"
                });
            }
            
            // Sort cultures with en-US first, then alphabetically
            cultures.Sort((a, b) => {
                if (a == "en-US") return -1;
                if (b == "en-US") return 1;
                return string.Compare(a, b, StringComparison.Ordinal);
            });
            return cultures;
        }

        private void InitializeUILanguageComboBox()
        {
            // Get all available languages (same as cultures)
            var supportedLanguages = GetAvailableCultures();

            UILanguageComboBox.Items.Clear();
            
            // Build list of (displayName, languageCode) for sorting by display name
            var languageItems = new List<(string DisplayName, string LanguageCode)>();
            foreach (var language in supportedLanguages)
            {
                try
                {
                    var cultureInfo = new CultureInfo(language);
                    var displayName = cultureInfo.DisplayName;
                    languageItems.Add((displayName, language));
                }
                catch
                {
                    // If language is invalid, skip it
                }
            }
            
            // Sort alphabetically by display name
            languageItems.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));
            
            // Add sorted items to ComboBox
            foreach (var (displayName, languageCode) in languageItems)
            {
                var item = new ComboBoxItem
                {
                    Content = displayName,
                    Tag = languageCode
                };
                UILanguageComboBox.Items.Add(item);
            }
        }

        private void LocalizationService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LocalizationService.CurrentCulture))
            {
                // Refresh the UI when culture changes
                RefreshUI();
            }
        }

        private void RefreshUI()
        {
            // Use dispatcher to ensure we're on the UI thread
            this.DispatcherQueue?.TryEnqueue(async () =>
            {
                try
                {
                    var locService = LocalizationService.Instance;
                    
                    // Small delay to ensure culture change has propagated
                    await Task.Delay(100);
                    
                    // Reload settings to update all localized text
                    _isLoadingSettings = true;
                    try
                    {
                        LoadSettings();
                    }
                    finally
                    {
                        _isLoadingSettings = false;
                    }
                    
                    // Manually update SettingsView elements that use x:Uid
                    // Since x:Uid resources don't refresh automatically, we need to manually update them
                    UpdateSettingsViewElements(locService);
                    
                    // Explicitly refresh all registered tooltips to ensure they update with the new language
                    LocalizedToolTipRegistry.RefreshRegisteredToolTips();
                    
                    // Force a layout update
                    this.InvalidateArrange();
                    this.InvalidateMeasure();
                    this.UpdateLayout();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"RefreshUI error: {ex.Message}");
                }
            });
        }

        internal void HostRefreshLocalization()
        {
            RefreshUI();
        }

        private void RegisterLocalizedToolTips()
        {
            if (_areToolTipsRegistered)
            {
                return;
            }

            _areToolTipsRegistered = true;

            LocalizedToolTipRegistry.Register(ThemePreferenceComboBox, "Controls.SettingsView.ThemePreferenceComboBox.ToolTip");
            LocalizedToolTipRegistry.Register(UILanguageComboBox, "Controls.SettingsView.UILanguageComboBox.ToolTip");
            LocalizedToolTipRegistry.Register(DefaultSearchResultsComboBox, "Controls.SettingsView.DefaultSearchResultsComboBox.ToolTip");
            LocalizedToolTipRegistry.Register(DefaultSearchTypeComboBox, "Controls.SettingsView.DefaultSearchTypeComboBox.ToolTip");
            LocalizedToolTipRegistry.Register(DefaultRespectGitignoreCheckBox, "Controls.SettingsView.DefaultRespectGitignoreCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(DefaultIncludeSystemFilesCheckBox, "Controls.SettingsView.DefaultIncludeSystemFilesCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(DefaultIncludeSubfoldersCheckBox, "Controls.SettingsView.DefaultIncludeSubfoldersCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(DefaultIncludeHiddenItemsCheckBox, "Controls.SettingsView.DefaultIncludeHiddenItemsCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(DefaultIncludeBinaryFilesCheckBox, "Controls.SettingsView.DefaultIncludeBinaryFilesCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(DefaultIncludeSymbolicLinksCheckBox, "Controls.SettingsView.DefaultIncludeSymbolicLinksCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(DefaultSearchCaseSensitiveCheckBox, "Controls.SettingsView.DefaultSearchCaseSensitiveCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(DefaultUseWindowsSearchCheckBox, "DefaultUseWindowsSearchCheckBox.ToolTipService.ToolTip");
            LocalizedToolTipRegistry.Register(EnableDockerSearchToggleSwitch, "Controls.SettingsView.EnableDockerSearchToggleSwitch.ToolTip");
            LocalizedToolTipRegistry.Register(CultureComboBox, "CultureComboBoxToolTip.Content");
            LocalizedToolTipRegistry.Register(UnicodeNormalizationModeComboBox, "Controls.SettingsView.UnicodeNormalizationModeComboBox.ToolTip");
            LocalizedToolTipRegistry.Register(StringComparisonModeComboBox, "Controls.SettingsView.StringComparisonModeComboBox.ToolTip");
            LocalizedToolTipRegistry.Register(DiacriticSensitiveCheckBox, "DiacriticSensitiveCheckBox.ToolTipService.ToolTip");
            LocalizedToolTipRegistry.Register(TestNotificationButton, "Controls.SettingsView.TestNotificationButton.ToolTip");
            LocalizedToolTipRegistry.Register(TestLocalizationButton, "Controls.SettingsView.TestLocalizationButton.ToolTip");
            LocalizedToolTipRegistry.Register(ExportSettingsButton, "Controls.SettingsView.ExportSettingsButton.ToolTip");
            LocalizedToolTipRegistry.Register(ImportSettingsButton, "Controls.SettingsView.ImportSettingsButton.ToolTip");
            LocalizedToolTipRegistry.Register(RestoreDefaultsButton, "Controls.SettingsView.RestoreDefaultsButton.ToolTip");
        }

        private void UpdateSettingsViewElements(LocalizationService locService)
        {
            try
            {
                // Manually update TextBlocks and other elements that use x:Uid
                // Since WinUI 3's x:Uid resources don't refresh automatically, we need to update them manually
                
                // Update header text blocks (now with x:Name attributes)
                if (SettingsTitleTextBlock != null)
                {
                    SettingsTitleTextBlock.Text = locService.GetLocalizedString("SettingsTitleTextBlock.Text");
                }
                
                if (SettingsDescriptionTextBlock != null)
                {
                    SettingsDescriptionTextBlock.Text = locService.GetLocalizedString("SettingsDescriptionTextBlock.Text");
                }
                
                if (ThemePreferenceTextBlock != null)
                {
                    ThemePreferenceTextBlock.Text = locService.GetLocalizedString("ThemePreferenceTextBlock.Text");
                }
                
                if (UILanguageHeaderTextBlock != null)
                {
                    UILanguageHeaderTextBlock.Text = locService.GetLocalizedString("UILanguageHeaderTextBlock.Text");
                }
                
                if (UILanguageLabelTextBlock != null)
                {
                    UILanguageLabelTextBlock.Text = locService.GetLocalizedString("UILanguageLabelTextBlock.Text");
                }
                
                if (FilterOptionsHeaderTextBlock != null)
                {
                    FilterOptionsHeaderTextBlock.Text = locService.GetLocalizedString("FilterOptionsHeaderTextBlock.Text");
                }
                
                if (StringComparisonHeaderTextBlock != null)
                {
                    StringComparisonHeaderTextBlock.Text = locService.GetLocalizedString("StringComparisonHeaderTextBlock.Text");
                }
                
                if (DebugHeaderTextBlock != null)
                {
                    DebugHeaderTextBlock.Text = locService.GetLocalizedString("DebugHeaderTextBlock.Text");
                }

                if (DockerSettingsHeaderTextBlock != null)
                {
                    DockerSettingsHeaderTextBlock.Text = locService.GetLocalizedString("DockerSettingsHeaderTextBlock.Text");
                }

                if (DockerSettingsDescriptionTextBlock != null)
                {
                    DockerSettingsDescriptionTextBlock.Text = locService.GetLocalizedString("DockerSettingsDescriptionTextBlock.Text");
                }

                if (EnableDockerSearchToggleSwitch != null)
                {
                    EnableDockerSearchToggleSwitch.Header = locService.GetLocalizedString("EnableDockerSearchToggleSwitch.Header");
                    EnableDockerSearchToggleSwitch.OnContent = locService.GetLocalizedString("EnableDockerSearchToggleSwitch.OnContent");
                    EnableDockerSearchToggleSwitch.OffContent = locService.GetLocalizedString("EnableDockerSearchToggleSwitch.OffContent");
                }
                
                // Update Culture label (Bug 3 fix - now uses x:Name so we can update it)
                if (CultureLabelTextBlock != null)
                {
                    CultureLabelTextBlock.Text = locService.GetLocalizedString("CultureLabelTextBlock.Text");
                }
                
                // Update Theme Preference ComboBox items
                if (ThemePreferenceComboBox != null)
                {
                    foreach (var item in ThemePreferenceComboBox.Items.OfType<ComboBoxItem>())
                    {
                        var tag = item.Tag?.ToString();
                        switch (tag)
                        {
                            case "System":
                                item.Content = locService.GetLocalizedString("SystemThemeComboBoxItem.Content");
                                break;
                            case "Light":
                                item.Content = locService.GetLocalizedString("LightThemeComboBoxItem.Content");
                                break;
                            case "Dark":
                                item.Content = locService.GetLocalizedString("DarkThemeComboBoxItem.Content");
                                break;
                            case "BlackKnight":
                                item.Content = locService.GetLocalizedString("BlackKnightThemeComboBoxItem.Content");
                                break;
                            case "Paranoid":
                                item.Content = locService.GetLocalizedString("ParanoidThemeComboBoxItem.Content");
                                break;
                            case "Diamond":
                                item.Content = locService.GetLocalizedString("DiamondThemeComboBoxItem.Content");
                                break;
                            case "Subspace":
                                item.Content = locService.GetLocalizedString("SubspaceThemeComboBoxItem.Content");
                                break;
                            case "RedVelvet":
                                item.Content = locService.GetLocalizedString("RedVelvetThemeComboBoxItem.Content");
                                break;
                            case "Dreams":
                                item.Content = locService.GetLocalizedString("DreamsThemeComboBoxItem.Content");
                                break;
                            case "Tiefling":
                                item.Content = locService.GetLocalizedString("TieflingThemeComboBoxItem.Content");
                                break;
                            case "Vibes":
                                item.Content = locService.GetLocalizedString("VibesThemeComboBoxItem.Content");
                                break;
                        }
                    }

                    RefreshComboBoxSelection(ThemePreferenceComboBox, ThemePreferenceComboBox_SelectionChanged);
                }
                
                // Update CultureComboBox with localized culture names (Bug 1 fix)
                RefreshCultureComboBox();
                
                // Update UILanguageComboBox with localized language names
                RefreshUILanguageComboBox();
                
                // Update Test Notification button and explanation text
                if (TestNotificationButton != null)
                {
                    TestNotificationButton.Content = locService.GetLocalizedString("TestNotificationButton.Content");
                }
                if (TestNotificationExplanationTextBlock != null)
                {
                    TestNotificationExplanationTextBlock.Text = locService.GetLocalizedString("TestNotificationExplanationTextBlock.Text");
                }
                if (TestLocalizationButton != null)
                {
                    TestLocalizationButton.Content = locService.GetLocalizedString("TestLocalizationButton.Content");
                }
                if (TestLocalizationExplanationTextBlock != null)
                {
                    TestLocalizationExplanationTextBlock.Text = locService.GetLocalizedString("TestLocalizationExplanationTextBlock.Text");
                }
                if (RestartApplicationButton != null)
                {
                    RestartApplicationButton.Content = locService.GetLocalizedString("RestartApplicationButton.Content");
                    ToolTipService.SetToolTip(RestartApplicationButton, locService.GetLocalizedString("Controls.SettingsView.RestartApplicationButton.ToolTip"));
                }
                
                // Update Filter Options labels
                if (SearchResultsLabelTextBlock != null)
                {
                    SearchResultsLabelTextBlock.Text = locService.GetLocalizedString("SearchResultsLabelTextBlock.Text");
                }
                if (SearchTypeLabelTextBlock != null)
                {
                    SearchTypeLabelTextBlock.Text = locService.GetLocalizedString("SearchTypeLabelTextBlock.Text");
                }
                
                // Update String Comparison labels
                if (StringComparisonModeLabelTextBlock != null)
                {
                    StringComparisonModeLabelTextBlock.Text = locService.GetLocalizedString("StringComparisonModeLabelTextBlock.Text");
                }
                if (UnicodeNormalizationLabelTextBlock != null)
                {
                    UnicodeNormalizationLabelTextBlock.Text = locService.GetLocalizedString("UnicodeNormalizationLabelTextBlock.Text");
                }
                if (DiacriticSensitiveCheckBox != null)
                {
                    DiacriticSensitiveCheckBox.Content = locService.GetLocalizedString("DiacriticSensitiveCheckBox.Content");
                }
                
                // Update String Comparison ComboBox items
                if (StringComparisonModeComboBox != null)
                {
                    foreach (var item in StringComparisonModeComboBox.Items.OfType<ComboBoxItem>())
                    {
                        var tag = item.Tag?.ToString();
                        if (tag == "Ordinal")
                        {
                            item.Content = locService.GetLocalizedString("OrdinalComboBoxItem.Content");
                        }
                        else if (tag == "CurrentCulture")
                        {
                            item.Content = locService.GetLocalizedString("CurrentCultureComboBoxItem.Content");
                        }
                        else if (tag == "InvariantCulture")
                        {
                            item.Content = locService.GetLocalizedString("InvariantCultureComboBoxItem.Content");
                        }
                    }

                    RefreshComboBoxSelection(StringComparisonModeComboBox, StringComparisonModeComboBox_SelectionChanged);
                }
                
                // Update Unicode Normalization ComboBox items
                if (UnicodeNormalizationModeComboBox != null)
                {
                    foreach (var item in UnicodeNormalizationModeComboBox.Items.OfType<ComboBoxItem>())
                    {
                        var tag = item.Tag?.ToString();
                        if (tag == "None")
                        {
                            item.Content = locService.GetLocalizedString("NoneNormalizationComboBoxItem.Content");
                        }
                        else if (tag == "FormC")
                        {
                            item.Content = locService.GetLocalizedString("FormCNormalizationComboBoxItem.Content");
                        }
                        else if (tag == "FormD")
                        {
                            item.Content = locService.GetLocalizedString("FormDNormalizationComboBoxItem.Content");
                        }
                        else if (tag == "FormKC")
                        {
                            item.Content = locService.GetLocalizedString("FormKCNormalizationComboBoxItem.Content");
                        }
                        else if (tag == "FormKD")
                        {
                            item.Content = locService.GetLocalizedString("FormKDNormalizationComboBoxItem.Content");
                        }
                    }

                    RefreshComboBoxSelection(UnicodeNormalizationModeComboBox, UnicodeNormalizationModeComboBox_SelectionChanged);
                }
                
                // Update Search Type ComboBox items
                if (DefaultSearchTypeComboBox != null)
                {
                    foreach (var item in DefaultSearchTypeComboBox.Items.OfType<ComboBoxItem>())
                    {
                        var tag = item.Tag?.ToString();
                        if (tag == "Text")
                        {
                            item.Content = locService.GetLocalizedString("TextSearchComboBoxItem.Content");
                        }
                        else if (tag == "Regex")
                        {
                            item.Content = locService.GetLocalizedString("RegexSearchComboBoxItem.Content");
                        }
                    }

                    RefreshComboBoxSelection(DefaultSearchTypeComboBox, DefaultSearchTypeComboBox_SelectionChanged);
                }
                
                // Update Search Results ComboBox items
                if (DefaultSearchResultsComboBox != null)
                {
                    foreach (var item in DefaultSearchResultsComboBox.Items.OfType<ComboBoxItem>())
                    {
                        var tag = item.Tag?.ToString();
                        if (tag == "Content")
                        {
                            item.Content = locService.GetLocalizedString("ContentComboBoxItem.Content");
                        }
                        else if (tag == "Files")
                        {
                            item.Content = locService.GetLocalizedString("FilesComboBoxItem.Content");
                        }
                    }

                    RefreshComboBoxSelection(DefaultSearchResultsComboBox, DefaultSearchResultsComboBox_SelectionChanged);
                }
                
                // Update Filter Options checkboxes
                if (DefaultRespectGitignoreCheckBox != null)
                {
                    DefaultRespectGitignoreCheckBox.Content = locService.GetLocalizedString("DefaultRespectGitignoreCheckBox.Content");
                }
                if (DefaultSearchCaseSensitiveCheckBox != null)
                {
                    DefaultSearchCaseSensitiveCheckBox.Content = locService.GetLocalizedString("DefaultSearchCaseSensitiveCheckBox.Content");
                }
                if (DefaultIncludeSystemFilesCheckBox != null)
                {
                    DefaultIncludeSystemFilesCheckBox.Content = locService.GetLocalizedString("DefaultIncludeSystemFilesCheckBox.Content");
                }
                if (DefaultIncludeSubfoldersCheckBox != null)
                {
                    DefaultIncludeSubfoldersCheckBox.Content = locService.GetLocalizedString("DefaultIncludeSubfoldersCheckBox.Content");
                }
                if (DefaultIncludeHiddenItemsCheckBox != null)
                {
                    DefaultIncludeHiddenItemsCheckBox.Content = locService.GetLocalizedString("DefaultIncludeHiddenItemsCheckBox.Content");
                }
                if (DefaultIncludeBinaryFilesCheckBox != null)
                {
                    DefaultIncludeBinaryFilesCheckBox.Content = locService.GetLocalizedString("DefaultIncludeBinaryFilesCheckBox.Content");
                }
                if (DefaultIncludeSymbolicLinksCheckBox != null)
                {
                    DefaultIncludeSymbolicLinksCheckBox.Content = locService.GetLocalizedString("DefaultIncludeSymbolicLinksCheckBox.Content");
                }
                if (DefaultUseWindowsSearchCheckBox != null)
                {
                    DefaultUseWindowsSearchCheckBox.Content = locService.GetLocalizedString("DefaultUseWindowsSearchCheckBox.Content");
                }
                
                // Update Backup & Restore section
                if (BackupRestoreHeaderTextBlock != null)
                {
                    BackupRestoreHeaderTextBlock.Text = locService.GetLocalizedString("BackupRestoreHeaderTextBlock.Text");
                }
                if (BackupRestoreExplanationTextBlock != null)
                {
                    BackupRestoreExplanationTextBlock.Text = locService.GetLocalizedString("BackupRestoreExplanationTextBlock.Text");
                }
                if (ExportSettingsButton != null)
                {
                    ExportSettingsButton.Content = locService.GetLocalizedString("ExportSettingsButton.Content");
                }
                if (ImportSettingsButton != null)
                {
                    ImportSettingsButton.Content = locService.GetLocalizedString("ImportSettingsButton.Content");
                }
                if (RestoreDefaultsButton != null)
                {
                    RestoreDefaultsButton.Content = locService.GetLocalizedString("RestoreDefaultsButton.Content");
                }
                
                // Note: Most other elements are updated via LoadSettings() or are ComboBox items
                // which are updated when we reload the settings
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSettingsViewElements error: {ex.Message}");
            }
        }

        private void RefreshCultureComboBox()
        {
            try
            {
                // Check if ComboBox is loaded and available
                if (CultureComboBox == null || !CultureComboBox.IsLoaded)
                {
                    return;
                }

                // Save the currently selected culture
                var selectedCulture = string.Empty;
                if (CultureComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string tag)
                {
                    selectedCulture = tag;
                }
                
                // Bug 3 fix: When UI language changes, update String Culture to match
                var newUILanguage = LocalizationService.Instance.CurrentCulture;
                if (string.IsNullOrEmpty(selectedCulture) || selectedCulture != newUILanguage)
                {
                    // Update to match the new UI language
                    selectedCulture = newUILanguage;
                }
                
                // Temporarily detach the event handler to prevent recursive calls
                CultureComboBox.SelectionChanged -= CultureComboBox_SelectionChanged;
                
                try
                {
                    // Repopulate with localized culture names
                    InitializeCultureComboBox();
                    
                    // Restore or update the selection
                    ComboBoxItem? cultureItemToSelect = null;
                    if (!string.IsNullOrEmpty(selectedCulture))
                    {
                        cultureItemToSelect = CultureComboBox.Items.Cast<ComboBoxItem>()
                            .FirstOrDefault(item => item.Tag?.ToString() == selectedCulture);
                    }
                    
                    if (cultureItemToSelect != null)
                    {
                        // Temporarily disable the loading flag to allow the event to fire
                        var wasLoading = _isLoadingSettings;
                        _isLoadingSettings = false;
                        
                        CultureComboBox.SelectedItem = cultureItemToSelect;
                        
                        // Bug 3 fix: Fire the selection changed event to trigger any handlers
                        // Manually call the handler since programmatic selection doesn't fire the event
                        var culture = cultureItemToSelect.Tag?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(culture))
                        {
                            SettingsService.SetDefaultCulture(culture);
                        }
                        
                        _isLoadingSettings = wasLoading;
                    }
                    else if (CultureComboBox.Items.Count > 0)
                    {
                        var wasLoading = _isLoadingSettings;
                        _isLoadingSettings = false;
                        CultureComboBox.SelectedIndex = 0;
                        _isLoadingSettings = wasLoading;
                    }
                }
                finally
                {
                    // Reattach the event handler
                    CultureComboBox.SelectionChanged += CultureComboBox_SelectionChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshCultureComboBox error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"RefreshCultureComboBox StackTrace: {ex.StackTrace}");
            }
        }

        private void RefreshUILanguageComboBox()
        {
            try
            {
                // Check if ComboBox is loaded and available
                if (UILanguageComboBox == null || !UILanguageComboBox.IsLoaded)
                {
                    return;
                }

                // Save the currently selected language
                var selectedLanguage = string.Empty;
                if (UILanguageComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string tag)
                {
                    selectedLanguage = tag;
                }
                
                // Temporarily detach the event handler to prevent recursive calls
                UILanguageComboBox.SelectionChanged -= UILanguageComboBox_SelectionChanged;
                
                try
                {
                    // Repopulate with localized language names (DisplayName will use the new culture)
                    InitializeUILanguageComboBox();
                    
                    // Restore the selection
                    ComboBoxItem? languageItemToSelect = null;
                    if (!string.IsNullOrEmpty(selectedLanguage))
                    {
                        languageItemToSelect = UILanguageComboBox.Items.Cast<ComboBoxItem>()
                            .FirstOrDefault(item => item.Tag?.ToString() == selectedLanguage);
                    }
                    
                    if (languageItemToSelect != null)
                    {
                        // Temporarily disable the loading flag to prevent event firing
                        var wasLoading = _isLoadingSettings;
                        _isLoadingSettings = true;
                        
                        UILanguageComboBox.SelectedItem = languageItemToSelect;
                        
                        _isLoadingSettings = wasLoading;
                    }
                    else if (UILanguageComboBox.Items.Count > 0)
                    {
                        var wasLoading = _isLoadingSettings;
                        _isLoadingSettings = true;
                        UILanguageComboBox.SelectedIndex = 0;
                        _isLoadingSettings = wasLoading;
                    }
                }
                finally
                {
                    // Reattach the event handler
                    UILanguageComboBox.SelectionChanged += UILanguageComboBox_SelectionChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshUILanguageComboBox error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"RefreshUILanguageComboBox StackTrace: {ex.StackTrace}");
            }
        }

        private void RefreshComboBoxSelection(ComboBox comboBox, SelectionChangedEventHandler handler)
        {
            if (comboBox == null)
            {
                return;
            }

            var selectedIndex = comboBox.SelectedIndex;
            comboBox.SelectionChanged -= handler;
            var previousLoading = _isLoadingSettings;
            _isLoadingSettings = true;
            try
            {
                if (selectedIndex >= 0)
                {
                    comboBox.SelectedIndex = -1;
                    comboBox.UpdateLayout();
                    comboBox.SelectedIndex = selectedIndex;
                }
                else
                {
                    comboBox.SelectedIndex = -1;
                }

                comboBox.UpdateLayout();
            }
            finally
            {
                _isLoadingSettings = previousLoading;
                comboBox.SelectionChanged += handler;
            }
        }


        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Reload settings when the view becomes visible to ensure we have the latest values
            LoadSettings();
            // Update ComboBox items and other elements to ensure proper localization
            UpdateSettingsViewElements(LocalizationService.Instance);
            
            // Subscribe to theme changes and apply initial theme
            MainWindow.ThemeChanged += OnThemeChanged;
            this.Unloaded += SettingsView_Unloaded;
            
            // Delay theme application to ensure visual tree is fully populated
            DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                ApplyCurrentThemeColors();
            });
        }
        
        private void SettingsView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from theme changes
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
                    // Reset to default theme
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
        
        private static bool IsHighContrastTheme(ThemePreference preference)
        {
            return preference == ThemePreference.BlackKnight ||
                   preference == ThemePreference.Paranoid ||
                   preference == ThemePreference.Diamond ||
                   preference == ThemePreference.Subspace ||
                   preference == ThemePreference.RedVelvet ||
                   preference == ThemePreference.Dreams ||
                   preference == ThemePreference.Tiefling ||
                   preference == ThemePreference.Vibes;
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
                
                // Clear first to ensure clean state when switching between high-contrast themes
                this.Resources?.Clear();
                
                // Apply text color to all TextBlocks
                ApplyForegroundToAllTextBlocks(this, e.TextBrush, e.AccentBrush);
                
                // Apply accent color to specific accent text
                ApplyAccentColors(e.AccentBrush);
                
                // Apply background to control containers
                this.Background = e.BackgroundBrush;
                
                // Apply theme resources for better control styling
                if (this.Resources == null)
                    this.Resources = new Microsoft.UI.Xaml.ResourceDictionary();
                
                // Button resources
                this.Resources["ButtonForeground"] = e.TextBrush;
                this.Resources["ButtonForegroundPointerOver"] = e.TextBrush;
                this.Resources["ButtonForegroundPressed"] = e.TextBrush;
                this.Resources["ButtonBackground"] = e.SecondaryBrush;
                this.Resources["ButtonBackgroundPointerOver"] = e.TertiaryBrush;
                this.Resources["ButtonBackgroundPressed"] = e.TertiaryBrush;
                
                // CheckBox resources
                this.Resources["CheckBoxForeground"] = e.TextBrush;
                this.Resources["CheckBoxForegroundPointerOver"] = e.TextBrush;
                this.Resources["CheckBoxForegroundPressed"] = e.TextBrush;
                this.Resources["CheckBoxCheckGlyphForegroundChecked"] = e.TextBrush;
                this.Resources["CheckBoxCheckBackgroundFillChecked"] = e.AccentBrush;
                this.Resources["CheckBoxCheckBackgroundFillCheckedPointerOver"] = e.AccentBrush;
                
                // ComboBox resources
                this.Resources["ComboBoxForeground"] = e.TextBrush;
                this.Resources["ComboBoxForegroundPointerOver"] = e.TextBrush;
                this.Resources["ComboBoxBackground"] = e.SecondaryBrush;
                this.Resources["ComboBoxBackgroundPointerOver"] = e.TertiaryBrush;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyThemeColors ERROR: {ex}");
            }
        }
        
        private void ApplyForegroundToAllTextBlocks(DependencyObject parent, SolidColorBrush foreground, SolidColorBrush accent)
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

                    // Force visual state refresh to apply new resources
                    // We must toggle to a different state and back to force the VSM to re-apply setters
                    // useTransitions: false ensures immediate update without animation delays
                    if (checkBox.IsEnabled)
                    {
                        Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "PointerOver", false);
                        Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Normal", false);
                    }
                    else
                    {
                        Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Normal", false);
                        Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Disabled", false);
                    }
                    
                    if (checkBox.IsChecked == true)
                    {
                        Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Unchecked", false);
                        Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Checked", false);
                    }
                    else if (checkBox.IsChecked == false)
                    {
                        Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Checked", false);
                        Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Unchecked", false);
                    }
                    else
                    {
                         Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Checked", false);
                         Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Indeterminate", false);
                    }
                }
                else if (child is Button button)
                {
                    button.Foreground = foreground;
                }
                
                // Recurse into children
                ApplyForegroundToAllTextBlocks(child, foreground, accent);
            }
        }
        
        private void ApplyAccentColors(SolidColorBrush accent)
        {
            // Apply accent color to description/subtitle text if needed
            // These are typically the lighter colored descriptive text
        }
        
        private void ClearHighContrastColors()
        {
            try
            {
                // Reset to default - let theme resources handle it
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
                        
                        // Force visual state refresh to reset to default theme resources
                        if (checkBox.IsEnabled)
                        {
                            Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "PointerOver", false);
                            Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Normal", false);
                        }
                        else
                        {
                            Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Normal", false);
                            Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Disabled", false);
                        }
                        
                        if (checkBox.IsChecked == true)
                        {
                            Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Unchecked", false);
                            Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Checked", false);
                        }
                        else if (checkBox.IsChecked == false)
                        {
                            Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Checked", false);
                            Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Unchecked", false);
                        }
                        else
                        {
                             Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Checked", false);
                             Microsoft.UI.Xaml.VisualStateManager.GoToState(checkBox, "Indeterminate", false);
                        }
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

        private void LoadSettings()
        {
            // Flag should already be set in constructor, but ensure it's set
            _isLoadingSettings = true;
            
            try
            {
                // Invalidate cache to ensure we get fresh settings
                SettingsService.InvalidateCache();
                var settings = SettingsService.GetDefaultSettings();
                
                // Theme preference
                var themePreference = settings.ThemePreference;
                ThemePreferenceComboBox.SelectedIndex = GetThemePreferenceIndex(themePreference);
                
                DefaultSearchTypeComboBox.SelectedIndex = settings.IsRegexSearch ? 1 : 0;
                DefaultSearchResultsComboBox.SelectedIndex = settings.IsFilesSearch ? 1 : 0;
                DefaultMatchFilesTextBox.Text = settings.DefaultMatchFiles ?? string.Empty;
                DefaultExcludeDirsTextBox.Text = settings.DefaultExcludeDirs ?? string.Empty;
                DefaultRespectGitignoreCheckBox.IsChecked = settings.RespectGitignore;
                DefaultSearchCaseSensitiveCheckBox.IsChecked = settings.SearchCaseSensitive;
                DefaultIncludeSystemFilesCheckBox.IsChecked = settings.IncludeSystemFiles;
                DefaultIncludeSubfoldersCheckBox.IsChecked = settings.IncludeSubfolders;
                DefaultIncludeHiddenItemsCheckBox.IsChecked = settings.IncludeHiddenItems;
                DefaultIncludeBinaryFilesCheckBox.IsChecked = settings.IncludeBinaryFiles;
                DefaultIncludeSymbolicLinksCheckBox.IsChecked = settings.IncludeSymbolicLinks;
                DefaultUseWindowsSearchCheckBox.IsChecked = settings.UseWindowsSearchIndex;
                
                // String comparison mode
                var stringComparisonMode = settings.StringComparisonMode;
                StringComparisonModeComboBox.SelectedIndex = GetComparisonModeIndex(stringComparisonMode);
                
                // Unicode normalization mode
                var unicodeNormalizationMode = settings.UnicodeNormalizationMode;
                UnicodeNormalizationModeComboBox.SelectedIndex = GetUnicodeNormalizationModeIndex(unicodeNormalizationMode);
                
                // Diacritic sensitivity
                DiacriticSensitiveCheckBox.IsChecked = settings.DiacriticSensitive;

                if (EnableDockerSearchToggleSwitch != null)
                {
                    EnableDockerSearchToggleSwitch.IsOn = settings.EnableDockerSearch;
                }
                
                // Culture (for string comparison)
                var culture = settings.Culture;
                if (!string.IsNullOrEmpty(culture))
                {
                    // Try to find the culture in the combo box
                    var cultureItem = CultureComboBox.Items.Cast<ComboBoxItem>()
                        .FirstOrDefault(item => item.Tag?.ToString() == culture);
                    if (cultureItem != null)
                    {
                        CultureComboBox.SelectedItem = cultureItem;
                    }
                    else
                    {
                        // If not found, select the first item and add the culture as a custom item
                        if (CultureComboBox.Items.Count > 0)
                        {
                            CultureComboBox.SelectedIndex = 0;
                        }
                        
                        // Check if culture already exists
                        var existingCustomCultures = CultureComboBox.Items.Cast<ComboBoxItem>()
                            .Where(item => item.Tag?.ToString() == culture)
                            .ToList();
                        
                        if (!existingCustomCultures.Any())
                        {
                            var customItem = new ComboBoxItem { Content = culture, Tag = culture };
                            CultureComboBox.Items.Add(customItem);
                            CultureComboBox.SelectedItem = customItem;
                        }
                    }
                }
                else
                {
                    // If no culture is set, select the first item
                    if (CultureComboBox.Items.Count > 0)
                    {
                        CultureComboBox.SelectedIndex = 0;
                    }
                }
                
                // UI Language (for application localization) - Bug 2 fix: load from settings
                var savedUILanguage = settings.UILanguage;
                var uiLanguageToUse = !string.IsNullOrEmpty(savedUILanguage) 
                    ? savedUILanguage 
                    : LocalizationService.Instance.CurrentCulture;
                
                var uiLanguageItem = UILanguageComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(item => item.Tag?.ToString() == uiLanguageToUse);
                if (uiLanguageItem != null)
                {
                    UILanguageComboBox.SelectedItem = uiLanguageItem;
                }
                else
                {
                    // If not found, select the first item (shouldn't happen, but handle gracefully)
                    if (UILanguageComboBox.Items.Count > 0)
                    {
                        UILanguageComboBox.SelectedIndex = 0;
                    }
                }
            }
            finally
            {
                // Always reset the flag, even if an exception occurs
                _isLoadingSettings = false;
            }
        }

        private void DefaultSearchTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var isRegex = selectedItem.Tag?.ToString() == "Regex";
                SettingsService.SetDefaultIsRegexSearch(isRegex);
            }
        }

        private void DefaultSearchResultsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var isFilesSearch = selectedItem.Tag?.ToString() == "Files";
                SettingsService.SetDefaultIsFilesSearch(isFilesSearch);
            }
        }

        private void DefaultRespectGitignore_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is CheckBox checkBox)
            {
                SettingsService.SetDefaultRespectGitignore(checkBox.IsChecked == true);
            }
        }

        private void DefaultSearchCaseSensitive_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is CheckBox checkBox)
            {
                SettingsService.SetDefaultSearchCaseSensitive(checkBox.IsChecked == true);
            }
        }

        private void DefaultIncludeSystemFiles_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is CheckBox checkBox)
            {
                SettingsService.SetDefaultIncludeSystemFiles(checkBox.IsChecked == true);
            }
        }

        private void DefaultIncludeSubfolders_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is CheckBox checkBox)
            {
                SettingsService.SetDefaultIncludeSubfolders(checkBox.IsChecked == true);
            }
        }

        private void DefaultIncludeHiddenItems_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is CheckBox checkBox)
            {
                SettingsService.SetDefaultIncludeHiddenItems(checkBox.IsChecked == true);
            }
        }

        private void DefaultIncludeBinaryFiles_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is CheckBox checkBox)
            {
                SettingsService.SetDefaultIncludeBinaryFiles(checkBox.IsChecked == true);
            }
        }

        private void DefaultIncludeSymbolicLinks_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is CheckBox checkBox)
            {
                SettingsService.SetDefaultIncludeSymbolicLinks(checkBox.IsChecked == true);
            }
        }

        private void DefaultUseWindowsSearch_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;

            if (sender is CheckBox checkBox)
            {
                SettingsService.SetDefaultUseWindowsSearchIndex(checkBox.IsChecked == true);
            }
        }

        private void DefaultMatchFilesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingSettings) return;

            if (sender is TextBox textBox)
            {
                SettingsService.SetDefaultMatchFiles(textBox.Text);
            }
        }

        private void DefaultExcludeDirsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingSettings) return;

            if (sender is TextBox textBox)
            {
                SettingsService.SetDefaultExcludeDirs(textBox.Text);
            }
        }

        private void EnableDockerSearchToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;

            if (sender is ToggleSwitch toggleSwitch)
            {
                SettingsService.SetEnableDockerSearch(toggleSwitch.IsOn);
            }
        }

        private void StringComparisonModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var mode = (StringComparisonMode)selectedItem.Tag;
                SettingsService.SetDefaultStringComparisonMode(mode);
            }
        }

        private void UnicodeNormalizationModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var mode = (UnicodeNormalizationMode)selectedItem.Tag;
                SettingsService.SetDefaultUnicodeNormalizationMode(mode);
            }
        }

        private void DiacriticSensitiveCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is CheckBox checkBox)
            {
                SettingsService.SetDefaultDiacriticSensitive(checkBox.IsChecked == true);
            }
        }

        private void CultureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            // Don't process selection changes while dropdown is open (keyboard navigation)
            // The actual change will be processed when the dropdown closes
            if (_isCultureDropDownOpen) return;
            
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var culture = selectedItem.Tag?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(culture))
                {
                    SettingsService.SetDefaultCulture(culture);
                }
            }
        }

        private void CultureComboBox_DropDownOpened(object? sender, object e)
        {
            _isCultureDropDownOpen = true;
        }

        private void CultureComboBox_DropDownClosed(object? sender, object e)
        {
            _isCultureDropDownOpen = false;
            
            // Process the selection now that the dropdown is closed (user committed their choice)
            if (_isLoadingSettings) return;
            
            if (CultureComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var culture = selectedItem.Tag?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(culture))
                {
                    SettingsService.SetDefaultCulture(culture);
                }
            }
        }

        private void UILanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            // Don't process selection changes while dropdown is open (keyboard navigation)
            // The actual change will be processed when the dropdown closes
            if (_isUILanguageDropDownOpen) return;
            
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var language = selectedItem.Tag?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(language))
                {
                    // Save UI language to settings (Bug 2 fix)
                    SettingsService.SetUILanguage(language);
                    
                    // Set the UI language via LocalizationService
                    // This will trigger PropertyChanged which calls RefreshUI()
                    LocalizationService.Instance.SetCulture(language);
                    
                    // Also immediately refresh to ensure UI updates
                    // (PropertyChanged might fire asynchronously)
                    RefreshUI();
                }
            }
        }

        private void UILanguageComboBox_DropDownOpened(object? sender, object e)
        {
            _isUILanguageDropDownOpen = true;
        }

        private void UILanguageComboBox_DropDownClosed(object? sender, object e)
        {
            _isUILanguageDropDownOpen = false;
            
            // Process the selection now that the dropdown is closed (user committed their choice)
            if (_isLoadingSettings) return;
            
            if (UILanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var language = selectedItem.Tag?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(language))
                {
                    // Save UI language to settings (Bug 2 fix)
                    SettingsService.SetUILanguage(language);
                    
                    // Set the UI language via LocalizationService
                    // This will trigger PropertyChanged which calls RefreshUI()
                    LocalizationService.Instance.SetCulture(language);
                    
                    // Also immediately refresh to ensure UI updates
                    // (PropertyChanged might fire asynchronously)
                    RefreshUI();
                }
            }
        }

        private int GetComparisonModeIndex(StringComparisonMode mode)
        {
            for (int i = 0; i < StringComparisonModeComboBox.Items.Count; i++)
            {
                if (StringComparisonModeComboBox.Items[i] is ComboBoxItem item &&
                    item.Tag is StringComparisonMode itemMode &&
                    itemMode == mode)
                {
                    return i;
                }
            }
            return 0; // Default to first item
        }

        private int GetUnicodeNormalizationModeIndex(UnicodeNormalizationMode mode)
        {
            for (int i = 0; i < UnicodeNormalizationModeComboBox.Items.Count; i++)
            {
                if (UnicodeNormalizationModeComboBox.Items[i] is ComboBoxItem item &&
                    item.Tag is UnicodeNormalizationMode itemMode &&
                    itemMode == mode)
                {
                    return i;
                }
            }
            return 0; // Default to first item
        }

        private async void ThemePreferenceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var tag = selectedItem.Tag?.ToString();
                Services.ThemePreference preference = tag switch
                {
                    "System" => Services.ThemePreference.System,
                    "Light" => Services.ThemePreference.Light,
                    "Dark" => Services.ThemePreference.Dark,
                    "BlackKnight" => Services.ThemePreference.BlackKnight,
                    "Paranoid" => Services.ThemePreference.Paranoid,
                    "Diamond" => Services.ThemePreference.Diamond,
                    "Subspace" => Services.ThemePreference.Subspace,
                    "RedVelvet" => Services.ThemePreference.RedVelvet,
                    "Dreams" => Services.ThemePreference.Dreams,
                    "Tiefling" => Services.ThemePreference.Tiefling,
                    "Vibes" => Services.ThemePreference.Vibes,
                    _ => Services.ThemePreference.System
                };
                
                SettingsService.SetThemePreference(preference);
                
                // Show confirmation dialog to restart application
                var dialog = new ContentDialog
                {
                    Title = GetString("ApplyThemePromptTitle"),
                    Content = GetString("ApplyThemePromptMessage"),
                    PrimaryButtonText = GetString("LaterButton"),
                    SecondaryButtonText = GetString("RestartApplicationButton.Content"),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };
                
                // Add hand cursor to dialog buttons
                dialog.Opened += async (s, args) =>
                {
                    await Task.Delay(50);
                    var buttons = FindAllDialogButtons(dialog);
                    foreach (var button in buttons)
                    {
                        button.PointerEntered += Button_PointerEntered;
                        button.PointerExited += Button_PointerExited;
                    }
                };
                
                var result = await dialog.ShowAsync();
                
                if (result == ContentDialogResult.Secondary)
                {
                    // Save window position before restart
                    App.MainWindowInstance?.SaveWindowPosition();
                    // Restart Application
                    Microsoft.Windows.AppLifecycle.AppInstance.Restart("/settings");
                }
                else
                {
                    // Apply immediately (Later)
                    ApplyTheme(preference);
                }
            }
        }

        private int GetThemePreferenceIndex(Services.ThemePreference preference)
        {
            return preference switch
            {
                Services.ThemePreference.System => 0,
                Services.ThemePreference.Light => 1,
                Services.ThemePreference.Dark => 2,
                Services.ThemePreference.BlackKnight => 3,
                Services.ThemePreference.Paranoid => 4,
                Services.ThemePreference.Diamond => 5,
                Services.ThemePreference.Subspace => 6,
                Services.ThemePreference.RedVelvet => 7,
                Services.ThemePreference.Dreams => 8,
                Services.ThemePreference.Tiefling => 9,
                Services.ThemePreference.Vibes => 10,
                _ => 0
            };
        }

        private void ApplyTheme(Services.ThemePreference preference)
        {
            // Get MainWindow from App's static property
            var mainWindow = App.MainWindowInstance;
            
            if (mainWindow != null)
            {
                mainWindow.ApplyThemeAndBackdrop(preference);
            }
        }

        private void RadioButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Set cursor to hand for RadioButton
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
                    this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                }
            }
        }

        private void RadioButton_PointerExited(object sender, PointerRoutedEventArgs e)
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
                    this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
                }
            }
        }

        private void ComboBox_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Set cursor to hand for ComboBox
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
                    this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                }
            }
        }

        private void ComboBox_PointerExited(object sender, PointerRoutedEventArgs e)
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
                    this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
                }
            }
        }

        private void ComboBoxItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Set cursor to hand for ComboBoxItem
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
                    this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                }
            }
        }

        private void ComboBoxItem_PointerExited(object sender, PointerRoutedEventArgs e)
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
                    this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
                }
            }
        }

        private void CheckBox_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Set cursor to hand for CheckBox
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
                    this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                }
            }
        }

        private void CheckBox_PointerExited(object sender, PointerRoutedEventArgs e)
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
                    this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
                }
            }
        }

        private void Button_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Set cursor to hand for Button
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
                    this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                }
            }
        }

        private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
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
                    this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
                }
            }
        }

        private void TextBox_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Set cursor to IBeam for TextBox
            if (sender is UIElement element)
            {
                try
                {
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && element != null)
                    {
                        var cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.IBeam);
                        prop.SetValue(element, cursor);
                    }
                }
                catch
                {
                    this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.IBeam);
                }
            }
        }

        private void TextBox_PointerExited(object sender, PointerRoutedEventArgs e)
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
                    this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
                }
            }
        }

        private async void TestNotificationButton_Click(object sender, RoutedEventArgs e)
        {
            await RunNotificationDiagnosticsAsync();
        }

        private async void TestLocalizationButton_Click(object sender, RoutedEventArgs e)
        {
            await TestLocalizationAsync();
        }

        private async Task RunNotificationDiagnosticsAsync()
        {
            try
            {
                LogToFile("TestNotificationButton_Click: Starting notification diagnostics");
                
                var notificationService = Services.NotificationService.Instance;
                LogToFile("TestNotificationButton_Click: NotificationService instance obtained");

                // Check registration first - this will attempt registration even if IsSupported() returns false
                // (which can happen with unpackaged apps even when notifications work)
                var registrationOk = notificationService.CheckRegistration();
                var supportOk = notificationService.CheckSupport();
                
                if (!registrationOk)
                {
                    LogToFile("TestNotificationButton_Click: Notification registration failed");
                    // If both support and registration failed, show support error (more comprehensive)
                    if (!supportOk)
                    {
                        LogToFile("TestNotificationButton_Click: Both support check and registration failed");
                        await ShowNotificationDialogAsync(
                            "Notifications Not Supported",
                            BuildSupportErrorMessage(notificationService));
                    }
                    else
                    {
                        await ShowNotificationDialogAsync(
                            "Notification Registration Failed",
                            BuildRegistrationErrorMessage(notificationService));
                    }
                    return;
                }
                
                // Registration succeeded - proceed with test notification
                // Note: If support check failed but registration succeeded, that's OK for unpackaged apps
                if (!supportOk)
                {
                    LogToFile("TestNotificationButton_Click: Registration succeeded despite IsSupported() returning false (common for unpackaged apps)");
                }
                
                notificationService.ShowError(
                    GetString("NotificationTestTitle"),
                    GetString("NotificationTestMessage"));
                LogToFile("TestNotificationButton_Click: ShowError called successfully");

                await ShowNotificationDialogAsync(
                    GetString("NotificationTestSentTitle"),
                    GetString("NotificationTestSentMessage", notificationService.NotificationLogFilePath, GetLocalNotificationLogPath()));
            }
            catch (Exception ex)
            {
                LogToFile($"TestNotificationButton_Click: Exception occurred - {ex.GetType().Name}: {ex.Message}");
                LogToFile($"TestNotificationButton_Click: StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    LogToFile($"TestNotificationButton_Click: InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }

                await ShowNotificationDialogAsync(
                    GetString("NotificationTestErrorTitle"),
                    GetString("NotificationTestErrorMessage", ex.Message, GetLocalNotificationLogPath()));
            }
        }

        private async Task ShowNotificationDialogAsync(string title, string message)
        {
            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 520
            };

            var dialog = new ContentDialog
            {
                Title = title,
                Content = textBlock,
                PrimaryButtonText = GetString("OKButton"),
                XamlRoot = this.XamlRoot
            };

            // Set up cursor handling for the OK button
            dialog.Opened += async (sender, e) =>
            {
                // Wait a bit for the dialog to fully render
                await Task.Delay(50);
                
                // Find the primary button in the dialog's visual tree
                var primaryButton = FindPrimaryButton(dialog);
                if (primaryButton != null)
                {
                    primaryButton.PointerEntered += Button_PointerEntered;
                    primaryButton.PointerExited += Button_PointerExited;
                }
            };

            await dialog.ShowAsync();
        }

        private static Button? FindPrimaryButton(DependencyObject parent)
        {
            // Search through the visual tree to find the primary button
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is Button button)
                {
                    // Check if this is the primary button by checking its content
                    var content = button.Content?.ToString();
                    var okButtonText = GetString("OKButton");
                    if (content == okButtonText)
                    {
                        return button;
                    }
                }
                
                // Recursively search children
                var result = FindPrimaryButton(child);
                if (result != null)
                {
                    return result;
                }
            }
            
            return null;
        }

        private static List<Button> FindAllDialogButtons(DependencyObject parent)
        {
            var buttons = new List<Button>();
            FindAllDialogButtonsRecursive(parent, buttons);
            return buttons;
        }

        private static void FindAllDialogButtonsRecursive(DependencyObject parent, List<Button> buttons)
        {
            // Search through the visual tree to find all buttons
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is Button button)
                {
                    buttons.Add(button);
                }
                
                // Recursively search children
                FindAllDialogButtonsRecursive(child, buttons);
            }
        }

        private static string BuildSupportErrorMessage(NotificationService service)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetString("NotificationSupportUnavailable"));
            sb.AppendLine();
            sb.AppendLine(GetString("NotificationSupportCommonCauses"));
            sb.AppendLine();
            sb.AppendLine(GetString("NotificationSupportTroubleshooting"));
            if (!string.IsNullOrEmpty(service.SupportFailureDetails))
            {
                sb.AppendLine();
                sb.AppendLine(GetString("NotificationDetailsPrefix", service.SupportFailureDetails));
            }
            sb.AppendLine();
            sb.AppendLine(GetString("NotificationLogFilePrefix", service.NotificationLogFilePath));
            return sb.ToString();
        }

        private static string BuildRegistrationErrorMessage(NotificationService service)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetString("NotificationRegistrationFailure"));
            sb.AppendLine();
            sb.AppendLine(GetString("NotificationRegistrationCommonCauses"));
            sb.AppendLine();
            sb.AppendLine(GetString("NotificationRegistrationTroubleshooting"));
            if (!string.IsNullOrEmpty(service.RegistrationFailureDetails))
            {
                sb.AppendLine();
                sb.AppendLine(GetString("NotificationDetailsPrefix", service.RegistrationFailureDetails));
            }
            sb.AppendLine();
            sb.AppendLine(GetString("NotificationLogFilePrefix", service.NotificationLogFilePath));
            return sb.ToString();
        }

        private static void LogToFile(string message)
        {
            try
            {
                var logFile = GetLocalNotificationLogPath();
                var logDir = Path.GetDirectoryName(logFile);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(logFile, $"[{timestamp}] {message}\n");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        private static string GetString(string key) =>
            LocalizationService.Instance.GetLocalizedString(key);

        private static string GetString(string key, params object[] args) =>
            LocalizationService.Instance.GetLocalizedString(key, args);

        private static string GetLocalNotificationLogPath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Grex", "notification_test.log");

        private async Task TestLocalizationAsync()
        {
            try
            {
                var localizationService = LocalizationService.Instance;
                var originalCulture = localizationService.CurrentCulture;
                var notificationService = NotificationService.Instance;

                // Get all English keys - comprehensive list of all localization keys
                var allKeys = GetAllLocalizationKeys();

                // Test all supported languages
                var languages = new[] { "en-US", "de-DE", "es-ES", "fr-FR" };
                var allLanguagesValid = true;
                var languageResults = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();

                foreach (var language in languages)
                {
                    try
                    {
                        // Set the language
                        localizationService.SetCulture(language);
                        await Task.Delay(100); // Wait for culture change to propagate

                        // Verify culture was set
                        if (localizationService.CurrentCulture != language)
                        {
                            allLanguagesValid = false;
                            if (!languageResults.ContainsKey(language))
                            {
                                languageResults[language] = new System.Collections.Generic.List<string>();
                            }
                            languageResults[language].Add($"Failed to set culture to {language}");
                            continue;
                        }

                        // Test all keys for this language
                        var missingKeys = new System.Collections.Generic.List<string>();
                        foreach (var key in allKeys)
                        {
                            var localizedString = localizationService.GetLocalizedString(key);
                            // A key is valid if it returns a non-empty string that's different from the key itself
                            // (meaning it was found in the resource file)
                            if (string.IsNullOrEmpty(localizedString) || localizedString == key)
                            {
                                missingKeys.Add(key);
                            }
                        }

                        if (missingKeys.Count > 0)
                        {
                            allLanguagesValid = false;
                            languageResults[language] = missingKeys;
                        }
                    }
                    catch (Exception ex)
                    {
                        allLanguagesValid = false;
                        if (!languageResults.ContainsKey(language))
                        {
                            languageResults[language] = new System.Collections.Generic.List<string>();
                        }
                        languageResults[language].Add($"Exception: {ex.Message}");
                    }
                }

                // Restore original culture
                localizationService.SetCulture(originalCulture);

                // Report results via toast notification
                if (allLanguagesValid)
                {
                    notificationService.ShowSuccess(
                        GetString("LocalizationTestSuccessTitle"),
                        GetString("LocalizationTestSuccessMessage", allKeys.Length, languages.Length));
                }
                else
                {
                    var errorMessage = BuildLocalizationErrorMessage(languageResults, allKeys.Length);
                    notificationService.ShowError(
                        GetString("LocalizationTestFailureTitle"),
                        errorMessage);
                }
            }
            catch (Exception ex)
            {
                NotificationService.Instance.ShowError(
                    GetString("LocalizationTestErrorTitle"),
                    GetString("LocalizationTestErrorMessage", ex.Message));
            }
        }

        private static string[] GetAllLocalizationKeys()
        {
            // Comprehensive list of all localization keys in the application
            // This should include all keys from all resource files
            return new[]
            {
                // Regex Builder keys
                "EnterValidPatternMessage",
                "EnterSampleTextMessage",
                "RegexBreakdownNoMatchesFound",
                "RegexBreakdownFoundMatches",
                "RegexBreakdownNoMatchFound",
                "RegexBreakdownFoundOneMatch",
                "RegexBreakdownErrorMessage",
                "RegexBreakdownEnterPatternMessage",
                "RegexBreakdownInvalidPatternMessage",
                "RegexBreakdownTypeCharacterClass",
                "RegexBreakdownTypeNonCapturingGroup",
                "RegexBreakdownTypeCapturingGroup",
                "RegexBreakdownTypeQuantifier",
                "RegexBreakdownTypeAnchor",
                "RegexBreakdownTypeEscapeSequence",
                "RegexBreakdownTypeLiteral",
                "RegexBreakdownDescCharacterClass",
                "RegexBreakdownDescNonCapturingGroup",
                "RegexBreakdownDescCapturingGroup",
                "RegexBreakdownDescQuantifierRange",
                "RegexBreakdownDescZeroOrMore",
                "RegexBreakdownDescOneOrMore",
                "RegexBreakdownDescZeroOrOne",
                "RegexBreakdownDescAnchorStart",
                "RegexBreakdownDescAnchorEnd",
                "RegexBreakdownDescDigit",
                "RegexBreakdownDescNonDigit",
                "RegexBreakdownDescWordChar",
                "RegexBreakdownDescNonWordChar",
                "RegexBreakdownDescWhitespace",
                "RegexBreakdownDescNonWhitespace",
                "RegexBreakdownDescNewline",
                "RegexBreakdownDescTab",
                "RegexBreakdownDescCarriageReturn",
                "RegexBreakdownDescLiteralChar",
                "RegexBreakdownOverwritePatternTitle",
                "RegexBreakdownOverwritePatternMessage",
                "ProceedButton",
                "CancelButton",
                "SampleTextTextBlock.Text",
                "RegexPatternTextBlock.Text",
                "LiveMatchResultsTextBlock.Text",
                "VisualRegexBreakdownTextBlock.Text",
                "PresetsTextBlock.Text",
                "OptionsTextBlock.Text",
                "SampleTextTextBox.PlaceholderText",
                "RegexPatternTextBox.PlaceholderText",
                "EmailPresetButton.Content",
                "PhonePresetButton.Content",
                "DatePresetButton.Content",
                "DigitsPresetButton.Content",
                "URLPresetButton.Content",
                "CaseInsensitiveCheckBox.Content",
                "MultilineCheckBox.Content",
                "GlobalMatchCheckBox.Content",
                "Controls.RegexBuilderView.SampleTextTextBox.ToolTip",
                "Controls.RegexBuilderView.RegexPatternTextBox.ToolTip",
                "Controls.RegexBuilderView.CaseInsensitiveCheckBox.ToolTip",
                "Controls.RegexBuilderView.MultilineCheckBox.ToolTip",
                "Controls.RegexBuilderView.GlobalMatchCheckBox.ToolTip",
                
                // Settings keys
                "SettingsTitleTextBlock.Text",
                "SettingsDescriptionTextBlock.Text",
                "ThemePreferenceTextBlock.Text",
                "SystemThemeRadio.Content",
                "LightThemeRadio.Content",
                "DarkThemeRadio.Content",
                "UILanguageHeaderTextBlock.Text",
                "UILanguageLabelTextBlock.Text",
                "FilterOptionsHeaderTextBlock.Text",
                "SearchResultsLabelTextBlock.Text",
                "SearchTypeLabelTextBlock.Text",
                "StringComparisonHeaderTextBlock.Text",
                "CultureLabelTextBlock.Text",
                "DebugHeaderTextBlock.Text",
                "TestNotificationButton.Content",
                "TestNotificationExplanationTextBlock.Text",
                "TestLocalizationButton.Content",
                "TestLocalizationExplanationTextBlock.Text",
                
                // Common keys
                "OKButton"
            };
        }

        private static string BuildLocalizationErrorMessage(
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> languageResults,
            int totalKeys)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetString("LocalizationTestFailureSummary"));
            sb.AppendLine();

            foreach (var kvp in languageResults)
            {
                var language = kvp.Key;
                var missingKeys = kvp.Value;
                sb.AppendLine(GetString("LocalizationTestLanguageFailure", language, missingKeys.Count));
                
                // Show first 5 missing keys as examples
                var keysToShow = missingKeys.Take(5).ToList();
                foreach (var key in keysToShow)
                {
                    sb.AppendLine($"  • {key}");
                }
                
                if (missingKeys.Count > 5)
                {
                    sb.AppendLine(GetString("LocalizationTestMoreKeys", missingKeys.Count - 5));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void RestartApplicationButton_Click(object sender, RoutedEventArgs e)
        {
            // Save window position before restart
            App.MainWindowInstance?.SaveWindowPosition();
            // Restart the application and pass "settings" as an argument to open the Settings page
            Microsoft.Windows.AppLifecycle.AppInstance.Restart("/settings");
        }

        private async void ExportSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                
                // Initialize the picker with the window handle
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
                
                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("JSON Files", new List<string>() { ".json" });
                
                // Generate timestamp-based filename
                var timestamp = DateTime.Now.ToString("yyyy_MM_dd_H_mm_ss");
                savePicker.SuggestedFileName = $"settings_{timestamp}";
                
                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    // Export the current settings
                    var settingsJson = SettingsService.ExportSettingsAsJson();
                    await Windows.Storage.FileIO.WriteTextAsync(file, settingsJson);
                    
                    await ShowDialogAsync(
                        GetString("SettingsExportedSuccessTitle"),
                        GetString("SettingsExportedSuccessMessage", file.Path));
                }
            }
            catch (Exception ex)
            {
                await ShowDialogAsync(
                    GetString("SettingsExportErrorTitle"),
                    GetString("SettingsExportErrorMessage", ex.Message));
            }
        }

        private async void ImportSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openPicker = new Windows.Storage.Pickers.FileOpenPicker();
                
                // Initialize the picker with the window handle
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
                
                openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                openPicker.FileTypeFilter.Add(".json");
                
                var file = await openPicker.PickSingleFileAsync();
                if (file != null)
                {
                    var json = await Windows.Storage.FileIO.ReadTextAsync(file);
                    
                    // Try to import the settings
                    var (success, message) = SettingsService.ImportSettingsFromJson(json);
                    
                    if (success)
                    {
                        await ShowDialogAsync(
                            GetString("SettingsImportedSuccessTitle"),
                            GetString("SettingsImportedSuccessMessage"));
                        
                        // Save window position before restart
                        App.MainWindowInstance?.SaveWindowPosition();
                        // Restart the application to apply imported settings
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
                    }
                    else
                    {
                        await ShowDialogAsync(
                            GetString("SettingsImportErrorTitle"),
                            message ?? GetString("SettingsImportInvalidFileMessage"));
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowDialogAsync(
                    GetString("SettingsImportErrorTitle"),
                    GetString("SettingsImportErrorMessage", ex.Message));
            }
        }

        private async void RestoreDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            // Show confirmation dialog
            var dialog = new ContentDialog
            {
                Title = GetString("RestoreDefaultsConfirmTitle"),
                Content = GetString("RestoreDefaultsConfirmMessage"),
                PrimaryButtonText = GetString("YesButton"),
                CloseButtonText = GetString("NoButton"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            // Add hand cursor to dialog buttons
            dialog.Opened += async (s, args) =>
            {
                await Task.Delay(50);
                var buttons = FindAllDialogButtons(dialog);
                foreach (var button in buttons)
                {
                    button.PointerEntered += Button_PointerEntered;
                    button.PointerExited += Button_PointerExited;
                }
            };

            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    // Delete settings file and restart the application
                    SettingsService.DeleteSettingsFile();
                    
                    // Restart the application
                    Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
                }
                catch (Exception ex)
                {
                    await ShowDialogAsync(
                        GetString("SettingsExportErrorTitle"),
                        ex.Message);
                }
            }
        }

        private async Task ShowDialogAsync(string title, string message)
        {
            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 520
            };

            var dialog = new ContentDialog
            {
                Title = title,
                Content = textBlock,
                PrimaryButtonText = GetString("OKButton"),
                XamlRoot = this.XamlRoot
            };

            // Add hand cursor to dialog button
            dialog.Opened += async (s, args) =>
            {
                await Task.Delay(50);
                var primaryButton = FindPrimaryButton(dialog);
                if (primaryButton != null)
                {
                    primaryButton.PointerEntered += Button_PointerEntered;
                    primaryButton.PointerExited += Button_PointerExited;
                }
            };

            await dialog.ShowAsync();
        }
    }
}

