using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.UI.Text;
using Grex.ViewModels;
using Grex.Services;
using Grex.Models;
using Windows.ApplicationModel.DataTransfer;
using WinForms = System.Windows.Forms;

namespace Grex.Controls
{
    public sealed partial class SearchTabContent : UserControl
    {
        private bool _isUpdatingDataContext = false;
        private readonly ILocalizationService _localizationService = LocalizationService.Instance;
        private bool _isLocalizationSubscribed;
        private bool _areToolTipsRegistered;
        private readonly ContextMenuService _contextMenuService = new ContextMenuService();
        private bool _isCurrentOperationReplace = false; // Tracks whether the current operation is a Replace
        private string WindowsSearchEnabledTooltip => _localizationService.GetLocalizedString("UseWindowsSearchCheckBox.ToolTipService.ToolTip");
        private string WindowsSearchDisabledTooltip => _localizationService.GetLocalizedString("WindowsSearchDisabledTooltip");

        public GridLength NameColumnWidth
        {
            get => (GridLength)GetValue(NameColumnWidthProperty);
            set
            {
                SetValue(NameColumnWidthProperty, value);
                ApplyHeaderWidth(NameColumnDefinition, value);
                UpdateAllRowColumnWidths();
            }
        }

        public static readonly DependencyProperty NameColumnWidthProperty =
            DependencyProperty.Register(nameof(NameColumnWidth), typeof(GridLength), typeof(SearchTabContent), new PropertyMetadata(new GridLength(220)));

        public GridLength LineColumnWidth
        {
            get => (GridLength)GetValue(LineColumnWidthProperty);
            set
            {
                SetValue(LineColumnWidthProperty, value);
                ApplyHeaderWidth(LineColumnDefinition, value);
                UpdateAllRowColumnWidths();
            }
        }

        public static readonly DependencyProperty LineColumnWidthProperty =
            DependencyProperty.Register(nameof(LineColumnWidth), typeof(GridLength), typeof(SearchTabContent), new PropertyMetadata(new GridLength(80)));

        public GridLength ColumnColumnWidth
        {
            get => (GridLength)GetValue(ColumnColumnWidthProperty);
            set
            {
                SetValue(ColumnColumnWidthProperty, value);
                ApplyHeaderWidth(ColumnColumnDefinition, value);
                UpdateAllRowColumnWidths();
            }
        }

        public static readonly DependencyProperty ColumnColumnWidthProperty =
            DependencyProperty.Register(nameof(ColumnColumnWidth), typeof(GridLength), typeof(SearchTabContent), new PropertyMetadata(new GridLength(90)));

        public GridLength TextColumnWidth
        {
            get => (GridLength)GetValue(TextColumnWidthProperty);
            set
            {
                SetValue(TextColumnWidthProperty, value);
                ApplyHeaderWidth(TextColumnDefinition, value);
                UpdateAllRowColumnWidths();
            }
        }

        public static readonly DependencyProperty TextColumnWidthProperty =
            DependencyProperty.Register(nameof(TextColumnWidth), typeof(GridLength), typeof(SearchTabContent), new PropertyMetadata(new GridLength(2, GridUnitType.Star)));

        public GridLength PathColumnWidth
        {
            get => (GridLength)GetValue(PathColumnWidthProperty);
            set
            {
                SetValue(PathColumnWidthProperty, value);
                ApplyHeaderWidth(PathColumnDefinition, value);
                UpdateAllRowColumnWidths();
            }
        }

        public static readonly DependencyProperty PathColumnWidthProperty =
            DependencyProperty.Register(nameof(PathColumnWidth), typeof(GridLength), typeof(SearchTabContent), new PropertyMetadata(new GridLength(2, GridUnitType.Star)));

        // Files mode column widths
        public GridLength FilesNameColumnWidth
        {
            get => (GridLength)GetValue(FilesNameColumnWidthProperty);
            set
            {
                SetValue(FilesNameColumnWidthProperty, value);
                ApplyHeaderWidth(FilesNameColumnDefinition, value);
                UpdateAllFilesRowColumnWidths();
            }
        }

        public static readonly DependencyProperty FilesNameColumnWidthProperty =
            DependencyProperty.Register(nameof(FilesNameColumnWidth), typeof(GridLength), typeof(SearchTabContent), new PropertyMetadata(new GridLength(220)));

        public GridLength FilesSizeColumnWidth
        {
            get => (GridLength)GetValue(FilesSizeColumnWidthProperty);
            set
            {
                SetValue(FilesSizeColumnWidthProperty, value);
                ApplyHeaderWidth(FilesSizeColumnDefinition, value);
                UpdateAllFilesRowColumnWidths();
            }
        }

        public static readonly DependencyProperty FilesSizeColumnWidthProperty =
            DependencyProperty.Register(nameof(FilesSizeColumnWidth), typeof(GridLength), typeof(SearchTabContent), new PropertyMetadata(new GridLength(100)));

        public GridLength FilesMatchesColumnWidth
        {
            get => (GridLength)GetValue(FilesMatchesColumnWidthProperty);
            set
            {
                SetValue(FilesMatchesColumnWidthProperty, value);
                ApplyHeaderWidth(FilesMatchesColumnDefinition, value);
                UpdateAllFilesRowColumnWidths();
            }
        }

        public static readonly DependencyProperty FilesMatchesColumnWidthProperty =
            DependencyProperty.Register(nameof(FilesMatchesColumnWidth), typeof(GridLength), typeof(SearchTabContent), new PropertyMetadata(new GridLength(80)));

        public GridLength FilesPathColumnWidth
        {
            get => (GridLength)GetValue(FilesPathColumnWidthProperty);
            set
            {
                SetValue(FilesPathColumnWidthProperty, value);
                ApplyHeaderWidth(FilesPathColumnDefinition, value);
                UpdateAllFilesRowColumnWidths();
            }
        }

        public static readonly DependencyProperty FilesPathColumnWidthProperty =
            DependencyProperty.Register(nameof(FilesPathColumnWidth), typeof(GridLength), typeof(SearchTabContent), new PropertyMetadata(new GridLength(2, GridUnitType.Star)));

        public GridLength FilesExtColumnWidth
        {
            get => (GridLength)GetValue(FilesExtColumnWidthProperty);
            set
            {
                SetValue(FilesExtColumnWidthProperty, value);
                ApplyHeaderWidth(FilesExtColumnDefinition, value);
                UpdateAllFilesRowColumnWidths();
            }
        }

        public static readonly DependencyProperty FilesExtColumnWidthProperty =
            DependencyProperty.Register(nameof(FilesExtColumnWidth), typeof(GridLength), typeof(SearchTabContent), new PropertyMetadata(new GridLength(80)));

        public GridLength FilesEncodingColumnWidth
        {
            get => (GridLength)GetValue(FilesEncodingColumnWidthProperty);
            set
            {
                SetValue(FilesEncodingColumnWidthProperty, value);
                ApplyHeaderWidth(FilesEncodingColumnDefinition, value);
                UpdateAllFilesRowColumnWidths();
            }
        }

        public static readonly DependencyProperty FilesEncodingColumnWidthProperty =
            DependencyProperty.Register(nameof(FilesEncodingColumnWidth), typeof(GridLength), typeof(SearchTabContent), new PropertyMetadata(new GridLength(100)));

        public GridLength FilesDateColumnWidth
        {
            get => (GridLength)GetValue(FilesDateColumnWidthProperty);
            set
            {
                SetValue(FilesDateColumnWidthProperty, value);
                ApplyHeaderWidth(FilesDateColumnDefinition, value);
                UpdateAllFilesRowColumnWidths();
            }
        }

        public static readonly DependencyProperty FilesDateColumnWidthProperty =
            DependencyProperty.Register(nameof(FilesDateColumnWidth), typeof(GridLength), typeof(SearchTabContent), new PropertyMetadata(new GridLength(150)));

        public TabViewModel? ViewModel
        {
            get => DataContext as TabViewModel;
            set
            {
                // Only set if different to avoid triggering DataContextChanged unnecessarily
                if (DataContext != value)
                {
                    _isUpdatingDataContext = true;
                    try
                    {
                        DataContext = value;
                    }
                    finally
                    {
                        _isUpdatingDataContext = false;
                    }
                }
            }
        }

        public SearchTabContent()
        {
            try
            {
                Log("SearchTabContent constructor: Starting");
                this.InitializeComponent();
                Log("SearchTabContent constructor: InitializeComponent completed");
                RegisterLocalizedToolTips();
                this.Loaded += SearchTabContent_Loaded;
                this.Unloaded += SearchTabContent_Unloaded;
                this.DataContextChanged += SearchTabContent_DataContextChanged;
                Log("SearchTabContent constructor: Completed");
            }
            catch (Exception ex)
            {
                Log($"SearchTabContent constructor ERROR: {ex}");
                throw;
            }
        }

        private void RegisterLocalizedToolTips()
        {
            if (_areToolTipsRegistered)
            {
                return;
            }

            _areToolTipsRegistered = true;

            LocalizedToolTipRegistry.Register(PathAutoSuggestBox, "Controls.SearchTabContent.PathAutoSuggestBox.ToolTip");
            LocalizedToolTipRegistry.Register(BrowseButton, "Controls.SearchTabContent.BrowseButton.ToolTip");
            LocalizedToolTipRegistry.Register(SearchTextBox, "Controls.SearchTabContent.SearchTextBox.ToolTip");
            LocalizedToolTipRegistry.Register(ReplaceCheckBox, "Controls.SearchTabContent.ReplaceCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(ReplaceWithTextBox, "Controls.SearchTabContent.ReplaceWithTextBox.ToolTip");
            LocalizedToolTipRegistry.Register(MatchFileNamesTextBox, "Controls.SearchTabContent.MatchFileNamesTextBox.ToolTip");
            LocalizedToolTipRegistry.Register(ExcludeDirsTextBox, "Controls.SearchTabContent.ExcludeDirsTextBox.ToolTip");
            LocalizedToolTipRegistry.Register(SearchTypeComboBox, "Controls.SearchTabContent.SearchTypeComboBox.ToolTip");
            LocalizedToolTipRegistry.Register(SearchResultsComboBox, "Controls.SearchTabContent.SearchResultsComboBox.ToolTip");
            LocalizedToolTipRegistry.Register(SizeLimitComboBox, "Controls.SearchTabContent.SizeLimitComboBox.ToolTip");
            LocalizedToolTipRegistry.Register(SizeLimitNumberBox, "Controls.SearchTabContent.SizeLimitNumberBox.ToolTip");
            LocalizedToolTipRegistry.Register(SizeUnitComboBox, "Controls.SearchTabContent.SizeUnitComboBox.ToolTip");
            LocalizedToolTipRegistry.Register(RespectGitignoreCheckBox, "Controls.SearchTabContent.RespectGitignoreCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(SearchCaseSensitiveCheckBox, "Controls.SearchTabContent.SearchCaseSensitiveCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(IncludeSystemFilesCheckBox, "Controls.SearchTabContent.IncludeSystemFilesCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(IncludeSubfoldersCheckBox, "Controls.SearchTabContent.IncludeSubfoldersCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(IncludeHiddenItemsCheckBox, "Controls.SearchTabContent.IncludeHiddenItemsCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(IncludeBinaryFilesCheckBox, "Controls.SearchTabContent.IncludeBinaryFilesCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(IncludeSymbolicLinksCheckBox, "Controls.SearchTabContent.IncludeSymbolicLinksCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(UseWindowsSearchCheckBox, "UseWindowsSearchCheckBox.ToolTipService.ToolTip");
            LocalizedToolTipRegistry.Register(DockerRefreshButton, "DockerRefreshButton.ToolTipService.ToolTip");
        }

        private void SearchTabContent_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("SearchTabContent_Loaded: Starting");
                if (ViewModel != null)
                {
                    BindViewModel();
                    Log("SearchTabContent_Loaded: ViewModel bound");
                }
                InitializeColumnWidths();
                // Pre-load recent paths on initialization
                RefreshSuggestions();
                // Ensure result headers remain hidden until data is available
                UpdateResultsHeaderVisibility();
                
                Log("SearchTabContent_Loaded: Completed");

                SubscribeToLocalizationChanges();
                RefreshLocalization();
                
                // Subscribe to theme changes and apply initial theme
                MainWindow.ThemeChanged += OnThemeChanged;
                
                // Delay theme application to ensure visual tree is fully populated
                DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    ApplyCurrentThemeColors();
                });
            }
            catch (Exception ex)
            {
                Log($"SearchTabContent_Loaded ERROR: {ex}");
                Log($"SearchTabContent_Loaded ERROR StackTrace: {ex.StackTrace}");
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

        private void SearchTabContent_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            // Prevent recursive calls
            if (_isUpdatingDataContext)
            {
                Log("SearchTabContent_DataContextChanged: Ignoring recursive call");
                return;
            }

            try
            {
                Log($"SearchTabContent_DataContextChanged: NewValue is {args.NewValue?.GetType().Name ?? "null"}, Current ViewModel is {ViewModel?.GetType().Name ?? "null"}");
                
                // Prevent setting the same ViewModel again (which would cause a loop)
                if (args.NewValue == ViewModel)
                {
                    Log("SearchTabContent_DataContextChanged: NewValue is same as current ViewModel, skipping");
                    return;
                }
                
                if (args.NewValue is TabViewModel newViewModel)
                {
                    Log("SearchTabContent_DataContextChanged: Unbinding old ViewModel");
                    UnbindViewModel();
                    
                    // Update internal reference without triggering DataContextChanged again
                    _isUpdatingDataContext = true;
                    try
                    {
                        this.DataContext = newViewModel;
                    }
                    finally
                    {
                        _isUpdatingDataContext = false;
                    }
                    Log("SearchTabContent_DataContextChanged: DataContext set");
                    
                    // Set ComboBox selection only if control is loaded and in visual tree
                    // This prevents COM exceptions when controls aren't ready yet
                    if (IsLoaded && SearchTypeComboBox != null)
                    {
                        try
                        {
                            SearchTypeComboBox.SelectedIndex = newViewModel.IsRegexSearch ? 1 : 0;
                            Log("SearchTabContent_DataContextChanged: Search type ComboBox set immediately");
                        }
                        catch (Exception ex)
                        {
                            Log($"SearchTabContent_DataContextChanged: Error setting SearchTypeComboBox: {ex.Message}");
                        }
                    }
                    if (IsLoaded && SearchResultsComboBox != null)
                    {
                        try
                        {
                            SearchResultsComboBox.SelectedIndex = newViewModel.IsFilesSearch ? 1 : 0;
                            Log("SearchTabContent_DataContextChanged: Search results ComboBox set immediately");
                        }
                        catch (Exception ex)
                        {
                            Log($"SearchTabContent_DataContextChanged: Error setting SearchResultsComboBox: {ex.Message}");
                        }
                    }
                    
                    // Don't call BindViewModel here - wait for Loaded event
                    if (IsLoaded)
                    {
                        BindViewModel();
                        Log("SearchTabContent_DataContextChanged: ViewModel bound (already loaded)");
                    }
                    else
                    {
                        Log("SearchTabContent_DataContextChanged: Control not loaded yet, will bind on Loaded event");
                    }
                }
                else if (args.NewValue == null && ViewModel != null)
                {
                    Log("SearchTabContent_DataContextChanged: DataContext is being cleared");
                    UnbindViewModel();
                    _isUpdatingDataContext = true;
                    try
                    {
                        this.DataContext = null;
                    }
                    finally
                    {
                        _isUpdatingDataContext = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"SearchTabContent_DataContextChanged ERROR: {ex}");
                Log($"SearchTabContent_DataContextChanged ERROR StackTrace: {ex.StackTrace}");
            }
        }

        private void SubscribeToLocalizationChanges()
        {
            if (_isLocalizationSubscribed)
            {
                return;
            }

            _localizationService.PropertyChanged += LocalizationService_PropertyChanged;
            _isLocalizationSubscribed = true;
        }

        private void SearchTabContent_Unloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from theme changes
            MainWindow.ThemeChanged -= OnThemeChanged;
            
            if (!_isLocalizationSubscribed)
            {
                return;
            }

            _localizationService.PropertyChanged -= LocalizationService_PropertyChanged;
            _isLocalizationSubscribed = false;
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
                Log($"OnThemeChanged ERROR: {ex}");
            }
        }
        
        private void ApplyCurrentThemeColors()
        {
            try
            {
                var currentTheme = MainWindow.CurrentTheme;
                if (!IsHighContrastTheme(currentTheme))
                {
                    // Reset to default theme resources
                    ClearHighContrastColors();
                    return;
                }
                
                var colors = MainWindow.GetCurrentThemeColors();
                ApplyThemeColors(new ThemeChangedEventArgs(currentTheme, colors.background, colors.secondary, colors.tertiary, colors.text, colors.accent));
            }
            catch (Exception ex)
            {
                Log($"ApplyCurrentThemeColors ERROR: {ex}");
            }
        }
        
        public void ApplyThemeFromHost(ThemeChangedEventArgs e)
        {
            ApplyThemeColors(e);
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
        
        private void ApplyThemeColors(ThemeChangedEventArgs e)
        {
            try
            {
                if (!IsHighContrastTheme(e.Theme))
                {
                    ClearHighContrastColors();
                    return;
                }
                
                Log($"ApplyThemeColors: Applying {e.Theme} theme");
                
                // Clear first to ensure clean state when switching between high-contrast themes
                this.Resources?.Clear();
                ResultsListView?.Resources?.Clear();
                FilesResultsListView?.Resources?.Clear();
                
                // Apply to results ListView
                if (ResultsListView != null)
                {
                    ResultsListView.Background = e.BackgroundBrush;
                    if (ResultsListView.Resources == null)
                        ResultsListView.Resources = new Microsoft.UI.Xaml.ResourceDictionary();
                    ResultsListView.Resources["ListViewItemBackground"] = e.BackgroundBrush;
                    ResultsListView.Resources["ListViewItemBackgroundPointerOver"] = e.SecondaryBrush;
                    ResultsListView.Resources["ListViewItemBackgroundSelected"] = e.TertiaryBrush;
                    ResultsListView.Resources["ListViewItemForeground"] = e.TextBrush;
                }
                
                // Apply to files results ListView
                if (FilesResultsListView != null)
                {
                    FilesResultsListView.Background = e.BackgroundBrush;
                    if (FilesResultsListView.Resources == null)
                        FilesResultsListView.Resources = new Microsoft.UI.Xaml.ResourceDictionary();
                    FilesResultsListView.Resources["ListViewItemBackground"] = e.BackgroundBrush;
                    FilesResultsListView.Resources["ListViewItemBackgroundPointerOver"] = e.SecondaryBrush;
                    FilesResultsListView.Resources["ListViewItemBackgroundSelected"] = e.TertiaryBrush;
                    FilesResultsListView.Resources["ListViewItemForeground"] = e.TextBrush;
                }
                
                // Apply to result grids and containers
                ApplyThemeToResultContainers(e);
                
                // Apply foreground colors to all text elements in this control
                ApplyForegroundToAllTextBlocks(this, e.TextBrush, e.AccentBrush, e.TertiaryBrush);
                
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
                this.Resources["CheckBoxCheckBackgroundFillChecked"] = e.TertiaryBrush;
                this.Resources["CheckBoxCheckBackgroundFillCheckedPointerOver"] = e.AccentBrush;
                
                // ComboBox resources
                this.Resources["ComboBoxForeground"] = e.TextBrush;
                this.Resources["ComboBoxForegroundPointerOver"] = e.TextBrush;
                this.Resources["ComboBoxBackground"] = e.SecondaryBrush;
                this.Resources["ComboBoxBackgroundPointerOver"] = e.TertiaryBrush;
                
                // TextBox resources
                this.Resources["TextBoxForeground"] = e.TextBrush;
                this.Resources["TextControlForeground"] = e.TextBrush;
                
                var accentDefaultBrush = e.SecondaryBrush;
                var accentHoverBrush = e.TertiaryBrush;

                // AccentButton resources (Browse button, Filter Options toggle)
                this.Resources["AccentButtonForeground"] = e.TextBrush;
                this.Resources["AccentButtonForegroundPointerOver"] = e.TextBrush;
                this.Resources["AccentButtonForegroundPressed"] = e.TextBrush;
                this.Resources["AccentButtonBackground"] = accentDefaultBrush;
                this.Resources["AccentButtonBackgroundPointerOver"] = accentHoverBrush;
                this.Resources["AccentButtonBackgroundPressed"] = accentHoverBrush;
                
                // Directly style AccentButton controls (ThemeResource doesn't update dynamically)
                if (DockerRefreshButton != null)
                {
                    DockerRefreshButton.Foreground = e.TextBrush;
                    DockerRefreshButton.Background = accentDefaultBrush;
                    
                    if (DockerRefreshButton.Resources == null)
                        DockerRefreshButton.Resources = new Microsoft.UI.Xaml.ResourceDictionary();
                    DockerRefreshButton.Resources["AccentButtonBackground"] = accentDefaultBrush;
                    DockerRefreshButton.Resources["AccentButtonBackgroundPointerOver"] = accentHoverBrush;
                    DockerRefreshButton.Resources["AccentButtonBackgroundPressed"] = accentHoverBrush;
                    DockerRefreshButton.Resources["AccentButtonForeground"] = e.TextBrush;
                    DockerRefreshButton.Resources["AccentButtonForegroundPointerOver"] = e.TextBrush;
                    DockerRefreshButton.Resources["AccentButtonForegroundPressed"] = e.TextBrush;
                }

                if (BrowseButton != null)
                {
                    BrowseButton.Foreground = e.TextBrush;
                    BrowseButton.Background = accentDefaultBrush;
                    
                    // Set hover/pressed state resources
                    if (BrowseButton.Resources == null)
                        BrowseButton.Resources = new Microsoft.UI.Xaml.ResourceDictionary();
                    BrowseButton.Resources["AccentButtonBackground"] = accentDefaultBrush;
                    BrowseButton.Resources["AccentButtonBackgroundPointerOver"] = accentHoverBrush;
                    BrowseButton.Resources["AccentButtonBackgroundPressed"] = accentHoverBrush;
                    BrowseButton.Resources["AccentButtonForeground"] = e.TextBrush;
                    BrowseButton.Resources["AccentButtonForegroundPointerOver"] = e.TextBrush;
                    BrowseButton.Resources["AccentButtonForegroundPressed"] = e.TextBrush;
                }
                
                var appBarDefaultBrush = e.SecondaryBrush;
                var appBarHoverBrush = e.TertiaryBrush;

                if (FilterOptionsToggleButton != null)
                {
                    // Clear any hardcoded values first
                    FilterOptionsToggleButton.ClearValue(AppBarToggleButton.ForegroundProperty);
                    FilterOptionsToggleButton.ClearValue(AppBarToggleButton.BackgroundProperty);
                    
                    // Set properties directly
                    FilterOptionsToggleButton.Foreground = e.TextBrush;
                    FilterOptionsToggleButton.Background = appBarDefaultBrush;
                    
                    // Set hover/pressed state resources
                    if (FilterOptionsToggleButton.Resources == null)
                        FilterOptionsToggleButton.Resources = new Microsoft.UI.Xaml.ResourceDictionary();
                    
                    // Set all AppBarToggleButton resources
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonBackground"] = appBarDefaultBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonBackgroundPointerOver"] = appBarHoverBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonBackgroundPressed"] = appBarHoverBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonBackgroundChecked"] = appBarDefaultBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonBackgroundCheckedPointerOver"] = appBarHoverBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonBackgroundCheckedPressed"] = appBarHoverBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonRevealBackground"] = appBarDefaultBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonRevealBackgroundPointerOver"] = appBarHoverBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonRevealBackgroundPressed"] = appBarHoverBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonRevealBackgroundChecked"] = appBarDefaultBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonRevealBackgroundCheckedPointerOver"] = appBarHoverBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonRevealBackgroundCheckedPressed"] = appBarHoverBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonForeground"] = e.TextBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonForegroundPointerOver"] = e.TextBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonForegroundPressed"] = e.TextBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonForegroundChecked"] = e.TextBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonForegroundCheckedPointerOver"] = e.TextBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonForegroundCheckedPressed"] = e.TextBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonRevealForeground"] = e.TextBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonRevealForegroundPointerOver"] = e.TextBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonRevealForegroundPressed"] = e.TextBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonRevealForegroundChecked"] = e.TextBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonRevealForegroundCheckedPointerOver"] = e.TextBrush;
                    FilterOptionsToggleButton.Resources["AppBarToggleButtonRevealForegroundCheckedPressed"] = e.TextBrush;
                    
                    // Also set CommandBar-specific resources that might be used
                    FilterOptionsToggleButton.Resources["CommandBarBackground"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    FilterOptionsToggleButton.Resources["CommandBarOverflowPresenterBackground"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    
                    // Apply to parent CommandBar resources if available
                    var commandBar = FilterOptionsToggleButton.Parent as Microsoft.UI.Xaml.Controls.CommandBar;
                    if (commandBar != null && commandBar.Resources == null)
                        commandBar.Resources = new Microsoft.UI.Xaml.ResourceDictionary();
                    if (commandBar?.Resources != null)
                    {
                        commandBar.Resources["AppBarToggleButtonBackground"] = appBarDefaultBrush;
                        commandBar.Resources["AppBarToggleButtonBackgroundChecked"] = appBarDefaultBrush;
                        commandBar.Resources["AppBarToggleButtonRevealBackground"] = appBarDefaultBrush;
                        commandBar.Resources["AppBarToggleButtonRevealBackgroundChecked"] = appBarDefaultBrush;
                    }

                    // Force visual state refresh for FilterOptionsToggleButton
                    if (FilterOptionsToggleButton.IsChecked == true)
                    {
                        Microsoft.UI.Xaml.VisualStateManager.GoToState(FilterOptionsToggleButton, "Unchecked", false);
                        Microsoft.UI.Xaml.VisualStateManager.GoToState(FilterOptionsToggleButton, "Checked", false);
                    }
                    else
                    {
                        Microsoft.UI.Xaml.VisualStateManager.GoToState(FilterOptionsToggleButton, "Checked", false);
                        Microsoft.UI.Xaml.VisualStateManager.GoToState(FilterOptionsToggleButton, "Unchecked", false);
                    }
                    
                    // Force a layout update
                    FilterOptionsToggleButton.InvalidateArrange();
                    FilterOptionsToggleButton.InvalidateMeasure();
                    FilterOptionsToggleButton.UpdateLayout();
                }
                
                Log($"ApplyThemeColors: Completed for {e.Theme}");
            }
            catch (Exception ex)
            {
                Log($"ApplyThemeColors ERROR: {ex}");
            }
        }
        
        private void ApplyForegroundToAllTextBlocks(DependencyObject parent, Microsoft.UI.Xaml.Media.SolidColorBrush foreground, Microsoft.UI.Xaml.Media.SolidColorBrush accent, Microsoft.UI.Xaml.Media.SolidColorBrush tertiary)
        {
            var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is TextBlock textBlock)
                {
                    // Use accent color for file names (they have AccentTextFillColorPrimaryBrush)
                    if (textBlock.Name?.Contains("FileName") == true || 
                        (textBlock.Foreground is Microsoft.UI.Xaml.Media.SolidColorBrush brush && 
                         brush.Color.ToString().Contains("0078D4")))
                    {
                        textBlock.Foreground = accent;
                    }
                    else
                    {
                        textBlock.Foreground = foreground;
                    }
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
                    checkBox.Resources["CheckBoxCheckBackgroundFillChecked"] = tertiary;
                    checkBox.Resources["CheckBoxCheckBackgroundFillCheckedPointerOver"] = accent;
                    checkBox.Resources["CheckBoxCheckBackgroundFillCheckedPressed"] = tertiary;

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
                else if (child is AppBarToggleButton toggleButton)
                {
                    toggleButton.Foreground = foreground;
                }
                else if (child is ComboBox comboBox)
                {
                    comboBox.Foreground = foreground;
                }
                
                // Recurse into children
                ApplyForegroundToAllTextBlocks(child, foreground, accent, tertiary);
            }
        }
        
        private void ApplyThemeToResultContainers(ThemeChangedEventArgs e)
        {
            try
            {
                // Find and style result container borders
                if (ContentResultsGrid != null)
                {
                    // Find the Border element that wraps the results
                    var resultsBorder = FindChildByName<Microsoft.UI.Xaml.Controls.Border>(ContentResultsGrid, null);
                    if (resultsBorder != null)
                    {
                        resultsBorder.Background = e.SecondaryBrush;
                    }
                    
                    // Apply foreground to header buttons
                    ApplyForegroundToAllTextBlocks(ContentResultsGrid, e.TextBrush, e.AccentBrush, e.TertiaryBrush);
                }
                
                if (FilesResultsGrid != null)
                {
                    var filesBorder = FindChildByName<Microsoft.UI.Xaml.Controls.Border>(FilesResultsGrid, null);
                    if (filesBorder != null)
                    {
                        filesBorder.Background = e.SecondaryBrush;
                    }
                    
                    // Apply foreground to header buttons
                    ApplyForegroundToAllTextBlocks(FilesResultsGrid, e.TextBrush, e.AccentBrush, e.TertiaryBrush);
                }
            }
            catch (Exception ex)
            {
                Log($"ApplyThemeToResultContainers ERROR: {ex}");
            }
        }
        
        private static T? FindChildByName<T>(DependencyObject parent, string? name) where T : FrameworkElement
        {
            var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T element && (name == null || element.Name == name))
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
        
        private void ClearHighContrastColors()
        {
            try
            {
                Log("ClearHighContrastColors: Clearing theme colors");
                
                // Reset to transparent to allow theme resources to work
                if (ResultsListView != null)
                {
                    ResultsListView.ClearValue(ListView.BackgroundProperty);
                    ResultsListView.Resources?.Clear();
                }
                
                if (FilesResultsListView != null)
                {
                    FilesResultsListView.ClearValue(ListView.BackgroundProperty);
                    FilesResultsListView.Resources?.Clear();
                }
                
                // Clear AccentButton styles
                if (BrowseButton != null)
                {
                    BrowseButton.ClearValue(Button.ForegroundProperty);
                    BrowseButton.ClearValue(Button.BackgroundProperty);
                    BrowseButton.Resources?.Clear();
                }
                
                if (FilterOptionsToggleButton != null)
                {
                    FilterOptionsToggleButton.ClearValue(AppBarToggleButton.ForegroundProperty);
                    FilterOptionsToggleButton.ClearValue(AppBarToggleButton.BackgroundProperty);
                    FilterOptionsToggleButton.Resources?.Clear();
                }
                
                // Clear resources
                this.Resources?.Clear();
                
                // Reset foreground on all controls
                ClearForegroundFromVisualTree(this);
                
                Log("ClearHighContrastColors: Cleared all theme colors");
            }
            catch (Exception ex)
            {
                Log($"ClearHighContrastColors ERROR: {ex}");
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
                    else if (child is AppBarToggleButton toggleButton)
                    {
                        toggleButton.ClearValue(AppBarToggleButton.ForegroundProperty);
                        toggleButton.ClearValue(AppBarToggleButton.BackgroundProperty);
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

        private void LocalizationService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(LocalizationService.CurrentCulture))
            {
                return;
            }

            if (DispatcherQueue == null || !DispatcherQueue.TryEnqueue(RefreshLocalization))
            {
                RefreshLocalization();
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
                comboBox.SelectionChanged += handler;
            }
        }

        private void UpdateSearchButtonState()
        {
            if (AppBarSearchButton != null)
            {
                var canSearchOrStop = ViewModel?.CanSearchOrStop ?? false;
                AppBarSearchButton.IsEnabled = canSearchOrStop;
            }

            if (AppBarReplaceButton != null)
            {
                var isReplaceRunning = ViewModel?.IsSearching == true && _isCurrentOperationReplace;
                var inputsFilled = !string.IsNullOrWhiteSpace(SearchTextBox?.Text) &&
                                   !string.IsNullOrWhiteSpace(ReplaceWithTextBox?.Text);
                var canStartReplace = (ViewModel?.CanReplace ?? false) && inputsFilled;
                AppBarReplaceButton.IsEnabled = isReplaceRunning || canStartReplace;
            }
        }

        private void UpdateResultsHeaderVisibility()
        {
            if (ViewModel == null)
            {
                // Hide headers if no ViewModel
                if (ContentResultsGrid != null)
                    ContentResultsGrid.Visibility = Visibility.Collapsed;
                if (FilesResultsGrid != null)
                    FilesResultsGrid.Visibility = Visibility.Collapsed;
                return;
            }

            bool hasResults = false;
            if (ViewModel.IsFilesSearch)
            {
                hasResults = ViewModel.FileSearchResults != null && ViewModel.FileSearchResults.Count > 0;
            }
            else
            {
                hasResults = ViewModel.SearchResults != null && ViewModel.SearchResults.Count > 0;
            }

            // Show/hide the appropriate results grid based on whether there are results
            if (hasResults)
            {
                if (ViewModel.IsFilesSearch)
                {
                    if (ContentResultsGrid != null)
                        ContentResultsGrid.Visibility = Visibility.Collapsed;
                    if (FilesResultsGrid != null)
                        FilesResultsGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    if (ContentResultsGrid != null)
                        ContentResultsGrid.Visibility = Visibility.Visible;
                    if (FilesResultsGrid != null)
                        FilesResultsGrid.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Hide both grids when there are no results
                if (ContentResultsGrid != null)
                    ContentResultsGrid.Visibility = Visibility.Collapsed;
                if (FilesResultsGrid != null)
                    FilesResultsGrid.Visibility = Visibility.Collapsed;
            }
        }

        private static string FormatPathForDisplay(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            try
            {
                var trimmedPath = path.TrimEnd('\\', '/');
                
                // For very short paths (like "D:\" or "D:\files"), show the whole path
                if (trimmedPath.Length <= 30)
                {
                    return trimmedPath;
                }

                // Split path into parts
                var parts = new List<string>();
                
                // Handle UNC paths (\\server\share\...)
                if (trimmedPath.StartsWith(@"\\"))
                {
                    var uncParts = trimmedPath.Substring(2).Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (uncParts.Length > 0)
                    {
                        // Add the server name as first part
                        parts.Add(@"\\" + uncParts[0]);
                        // Add remaining parts
                        for (int i = 1; i < uncParts.Length; i++)
                        {
                            parts.Add(uncParts[i]);
                        }
                    }
                }
                else
                {
                    // Handle regular paths (C:\folder\...)
                    parts = trimmedPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                // If we have 2 or fewer parts, show the whole path
                if (parts.Count <= 2)
                {
                    return trimmedPath;
                }

                // For longer paths, show first part + ... + last part
                var firstPart = parts[0];
                var lastPart = parts[parts.Count - 1];
                return $"{firstPart}\\...\\{lastPart}";
            }
            catch
            {
                // If formatting fails, return original path
                return path;
            }
        }

        private void UpdateWindowsSearchCheckboxState()
        {
            if (UseWindowsSearchCheckBox == null || ViewModel == null)
                return;

            var isEnabled = ViewModel.IsWindowsSearchOptionEnabled;
            UseWindowsSearchCheckBox.IsEnabled = isEnabled;

            var tooltip = isEnabled ? WindowsSearchEnabledTooltip : WindowsSearchDisabledTooltip;
            ToolTipService.SetToolTip(UseWindowsSearchCheckBox, tooltip);

            if (!isEnabled)
            {
                if (UseWindowsSearchCheckBox.IsChecked == true)
                {
                    UseWindowsSearchCheckBox.IsChecked = false;
                }
            }
            else if (UseWindowsSearchCheckBox.IsChecked != ViewModel.UseWindowsSearchIndex)
            {
                UseWindowsSearchCheckBox.IsChecked = ViewModel.UseWindowsSearchIndex;
            }
        }

        private void PathAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                RefreshSuggestions();
            }

            if (ViewModel != null)
            {
                ViewModel.SearchPath = sender.Text;
                UpdateSearchButtonState();
                UpdateWindowsSearchCheckboxState();
            }
        }

        private void PathAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            // Extract full path from PathSuggestion
            string selectedPath;
            if (args.SelectedItem is PathSuggestion suggestion)
            {
                selectedPath = suggestion.FullPath;
            }
            else
            {
                selectedPath = args.SelectedItem?.ToString() ?? string.Empty;
            }
            
            sender.Text = selectedPath;
            if (ViewModel != null)
            {
                ViewModel.SearchPath = selectedPath;
            }
            UpdateSearchButtonState();
            UpdateWindowsSearchCheckboxState();
        }

        private void PathAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string selectedPath;
            
            if (args.ChosenSuggestion != null)
            {
                // User selected a suggestion
                if (args.ChosenSuggestion is PathSuggestion suggestion)
                {
                    selectedPath = suggestion.FullPath;
                }
                else
                {
                    selectedPath = args.ChosenSuggestion.ToString() ?? string.Empty;
                }
                sender.Text = selectedPath;
            }
            else
            {
                // User pressed Enter with typed text
                selectedPath = args.QueryText;
            }
            
            if (ViewModel != null)
            {
                ViewModel.SearchPath = selectedPath;
            }
            UpdateSearchButtonState();
            UpdateWindowsSearchCheckboxState();
        }

        private void PathAutoSuggestBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Show recent paths when focused, with formatted display text
            RefreshSuggestions();
        }

        private void RefreshSuggestions()
        {
            var searchText = PathAutoSuggestBox.Text ?? string.Empty;
            var filteredPaths = RecentPathsService.FilterPaths(searchText);
            
            var suggestions = filteredPaths.Select(path => 
                new PathSuggestion(path, FormatPathForDisplay(path))
            ).ToList();
            
            PathAutoSuggestBox.ItemsSource = suggestions;
        }

        private void RemoveButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Ensure we don't register multiple times
                button.AddHandler(UIElement.PointerPressedEvent,
                    new PointerEventHandler(RemoveButton_PointerPressed), true);
            }
        }

        private void RemoveButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(null);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;

            if (sender is Button button && button.DataContext is PathSuggestion suggestion)
            {
                RemoveRecentPath(suggestion);
            }
        }

        private void RemoveRecentPath(PathSuggestion suggestion)
        {
            try
            {
                var fullPath = suggestion.FullPath;
                Log($"RemoveRecentPath: Removing path: {fullPath}");

                RecentPathsService.RemoveRecentPath(fullPath);

                Log("RemoveRecentPath: Path removed, refreshing suggestions");

                var currentText = PathAutoSuggestBox.Text ?? string.Empty;

                RefreshSuggestions();

                DispatcherQueue.TryEnqueue(() =>
                {
                    PathAutoSuggestBox.Text = currentText;
                    PathAutoSuggestBox.Focus(FocusState.Programmatic);
                });
            }
            catch (Exception ex)
            {
                Log($"RemoveRecentPath ERROR: {ex.Message}");
                Log($"RemoveRecentPath ERROR StackTrace: {ex.StackTrace}");
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.SearchTerm = SearchTextBox.Text;
                UpdateSearchButtonState();
            }
        }

        private void ReplaceWithTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ReplaceWith = ReplaceWithTextBox.Text;
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                if (ViewModel != null && ViewModel.CanSearch)
                {
                    SearchButton_Click(sender, e);
                }
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Use WinForms FolderBrowserDialog directly - it's more reliable and doesn't have COM interop issues
                var dialog = new WinForms.FolderBrowserDialog
                {
                    Description = "Select a folder to search",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = false
                };

                var result = dialog.ShowDialog();
                if (result == WinForms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    var selectedPath = dialog.SelectedPath;
                    PathAutoSuggestBox.Text = selectedPath;
                    
                    // Save to recent paths
                    RecentPathsService.AddRecentPath(selectedPath);
                    
                    if (ViewModel != null)
                    {
                        ViewModel.SearchPath = selectedPath;
                    }
                    UpdateSearchButtonState();
                }
            }
            catch (Exception ex)
            {
                if (ViewModel != null)
                {
                    ViewModel.StatusText = $"Error: {ex.Message}";
                }
                Log($"BrowseButton_Click ERROR: {ex}");
            }
        }

        private async void DockerRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel != null)
                {
                    await ViewModel.RefreshDockerContainersAsync();
                }
            }
            catch (Exception ex)
            {
                Log($"DockerRefreshButton_Click ERROR: {ex}");
            }
        }


        private void BrowseButton_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Button button)
            {
                // If button width is less than 80 pixels, truncate to "..."
                // Otherwise show "Browse..."
                if (e.NewSize.Width < 80)
                {
                    button.Content = "...";
                }
                else
                {
                    button.Content = "Browse...";
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
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (prop != null && element != null)
                    {
                        var cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                        prop.SetValue(element, cursor);
                    }
                }
                catch
                {
                    // If reflection fails, set on this control as fallback
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
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (prop != null && element != null)
                    {
                        var cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                        prop.SetValue(element, cursor);
                    }
                }
                catch
                {
                    // If reflection fails, set on this control as fallback
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
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
                    var prop = typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

        private void SearchTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && ViewModel != null && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                ViewModel.IsRegexSearch = selectedItem.Tag?.ToString() == "Regex";
                UpdateWindowsSearchCheckboxState();
            }
        }

        private void SearchResultsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && ViewModel != null && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                ViewModel.IsFilesSearch = selectedItem.Tag?.ToString() == "Files";
            }
        }

        private void RespectGitignore_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && ViewModel != null)
            {
                ViewModel.RespectGitignore = checkBox.IsChecked == true;
            }
        }

        private void SearchCaseSensitive_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && ViewModel != null)
            {
                ViewModel.SearchCaseSensitive = checkBox.IsChecked == true;
            }
        }

        private void IncludeSystemFiles_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && ViewModel != null)
            {
                ViewModel.IncludeSystemFiles = checkBox.IsChecked == true;
            }
        }

        private void IncludeSubfolders_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && ViewModel != null)
            {
                ViewModel.IncludeSubfolders = checkBox.IsChecked == true;
            }
        }

        private void IncludeHiddenItems_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && ViewModel != null)
            {
                ViewModel.IncludeHiddenItems = checkBox.IsChecked == true;
            }
        }

        private void IncludeBinaryFiles_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && ViewModel != null)
            {
                ViewModel.IncludeBinaryFiles = checkBox.IsChecked == true;
            }
        }

        private void IncludeSymbolicLinks_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && ViewModel != null)
            {
                ViewModel.IncludeSymbolicLinks = checkBox.IsChecked == true;
            }
        }

        private void UseWindowsSearchIndex_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && ViewModel != null)
            {
                if (!ViewModel.IsWindowsSearchOptionEnabled)
                {
                    if (checkBox.IsChecked == true)
                    {
                        checkBox.IsChecked = false;
                    }
                    return;
                }

                ViewModel.UseWindowsSearchIndex = checkBox.IsChecked == true;
            }
        }

        private void SizeLimitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && ViewModel != null)
            {
                if (comboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string tag)
                {
                    var sizeLimitType = tag switch
                    {
                        "NoLimit" => Models.SizeLimitType.NoLimit,
                        "LessThan" => Models.SizeLimitType.LessThan,
                        "EqualTo" => Models.SizeLimitType.EqualTo,
                        "GreaterThan" => Models.SizeLimitType.GreaterThan,
                        _ => Models.SizeLimitType.NoLimit
                    };
                    ViewModel.SizeLimitType = sizeLimitType;
                    
                    // Show/hide the size input panel
                    SizeLimitInputPanel.Visibility = sizeLimitType == Models.SizeLimitType.NoLimit 
                        ? Visibility.Collapsed 
                        : Visibility.Visible;
                }
            }
        }

        private void SizeLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && ViewModel != null)
            {
                var text = textBox.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    ViewModel.SizeLimitKB = null;
                }
                else if (double.TryParse(text, out double value) && value > 0)
                {
                    // Round up to nearest integer KB to ensure "Less Than" works correctly
                    // For example, "3.57" becomes 4 KB, so files < 4 KB (including 3.57 KB) will match
                    // This ensures that when user enters "3.57", files smaller than 4 KB are included
                    ViewModel.SizeLimitKB = (long)Math.Ceiling(value);
                }
                // If parsing fails, keep the previous value (don't update ViewModel)
            }
        }

        private void SizeUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && ViewModel != null)
            {
                if (comboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string tag)
                {
                    var sizeUnit = tag switch
                    {
                        "KB" => Models.SizeUnit.KB,
                        "MB" => Models.SizeUnit.MB,
                        "GB" => Models.SizeUnit.GB,
                        _ => Models.SizeUnit.KB
                    };
                    ViewModel.SizeUnit = sizeUnit;
                }
            }
        }

        private static bool IsValidRegexPattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return true;

            // Check for specific invalid patterns first (before trying to construct regex)
            
            // Check for nested quantifiers: *, **, ???, +++, {{
            // These are always invalid regardless of context
            if (pattern.Contains("???") || pattern.Contains("++") || pattern.Contains("**") || pattern.Contains("{{"))
            {
                return false;
            }
            // Also check for ?? (two question marks - invalid nested quantifier)
            // But *? is valid (non-greedy), so we need to check context
            // Simple approach: check for ?? that's not preceded by *
            for (int i = 1; i < pattern.Length; i++)
            {
                if (pattern[i] == '?' && pattern[i - 1] == '?' && (i < 2 || pattern[i - 2] != '*'))
                {
                    return false;
                }
            }

            // Check for trailing unescaped backslash (odd number of backslashes at the end)
            int trailingBackslashes = 0;
            for (int i = pattern.Length - 1; i >= 0 && pattern[i] == '\\'; i--)
            {
                trailingBackslashes++;
            }
            if (trailingBackslashes % 2 == 1) // Odd number means unescaped trailing backslash
            {
                return false;
            }

            // Check for unclosed groups (count opening and closing parentheses and brackets)
            // Need to account for escaped characters
            int openParens = 0;
            int openBrackets = 0;
            bool inEscape = false;
            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];
                if (inEscape)
                {
                    inEscape = false;
                    continue;
                }
                if (c == '\\')
                {
                    inEscape = true;
                    continue;
                }
                if (c == '(')
                    openParens++;
                else if (c == ')')
                    openParens--;
                else if (c == '[')
                    openBrackets++;
                else if (c == ']')
                    openBrackets--;
            }
            if (openParens != 0 || openBrackets != 0)
            {
                return false;
            }

            // Try to create and use the regex to catch runtime errors
            // This will catch patterns like ^(**|resources)$ which have invalid quantifiers
            // The Regex constructor should throw ArgumentException for invalid patterns
            try
            {
                var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.None);
                // Also try to match an empty string to catch any runtime issues
                regex.IsMatch("");
                return true;
            }
            catch (ArgumentException)
            {
                // ArgumentException is thrown for invalid regex patterns
                return false;
            }
            catch (Exception)
            {
                // Catch any other exceptions (shouldn't happen, but be safe)
                return false;
            }
        }

        /// <summary>
        /// Checks if the path looks like a WSL home directory accessed through Windows
        /// (e.g., C:\home\user, D:\home\...) which would be slower than using native WSL paths.
        /// </summary>
        private static bool IsWindowsWslHomePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < 8)
                return false;

            // Check for pattern: [A-Z]:\home\ (any drive letter followed by \home\)
            if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
            {
                var remainder = path.Substring(2);
                if (remainder.StartsWith("\\home\\", StringComparison.OrdinalIgnoreCase) ||
                    remainder.StartsWith("/home/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Result of the WSL path warning dialog.
        /// </summary>
        private enum WslPathDialogResult
        {
            /// <summary>User chose to continue with slow Windows path search.</summary>
            Continue,
            /// <summary>User chose to try native WSL path for faster search.</summary>
            WslSearch,
            /// <summary>Dialog was closed without making a choice.</summary>
            Cancelled
        }

        /// <summary>
        /// Shows a warning dialog when user tries to search a WSL path through Windows,
        /// offering the choice between slow Windows search or faster native WSL search.
        /// </summary>
        /// <returns>The user's choice: Continue (slow), WslSearch (try native), or Cancelled.</returns>
        private async Task<WslPathDialogResult> ShowWslPathWarningDialogAsync()
        {
            var dialog = new ContentDialog
            {
                Title = GetString("WslPathWarningTitle"),
                Content = GetString("WslPathWarningMessage"),
                PrimaryButtonText = GetString("ContinueButton"),
                SecondaryButtonText = GetString("WslSearchButton"),
                DefaultButton = ContentDialogButton.Secondary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result switch
            {
                ContentDialogResult.Primary => WslPathDialogResult.Continue,
                ContentDialogResult.Secondary => WslPathDialogResult.WslSearch,
                _ => WslPathDialogResult.Cancelled
            };
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            // Mark this as a Search operation (not Replace)
            _isCurrentOperationReplace = false;

            try
            {
                var excludeDirs = ExcludeDirsTextBox?.Text ?? string.Empty;
                
                // Validate Exclude Dirs regex if it looks like a regex pattern
                if (!string.IsNullOrEmpty(excludeDirs)) {
                    if ( excludeDirs == "**" || excludeDirs == "*" ) {
                            Services.NotificationService.Instance.ShowError(
                                GetString("NoResultsPossibleTitle"), 
                                GetString("NoResultsPossibleMessage"));
                            return;
                    } else
                    if ( !excludeDirs.Contains(",") && (excludeDirs.Contains("^") || excludeDirs.Contains("$") || excludeDirs.Contains("|")) ) {
                        if (!IsValidRegexPattern(excludeDirs)) {
                            // Invalid regex - show error and cancel
                            Services.NotificationService.Instance.ShowError(
                                GetString("InvalidRegexPatternTitle"), 
                                GetString("InvalidRegexPatternMessage"));
                            return;
                        }
                    }
                }
                
                // Check if path looks like WSL accessed through Windows (e.g., C:\home\...)
                // Offer choice between slow Windows search or faster native WSL search
                // Only check if Search Target is set to "Local Disk" (or Docker search is disabled)
                var searchPath = PathAutoSuggestBox.Text;
                var isLocalDiskSelected = ViewModel?.SelectedDockerOption == null || ViewModel.SelectedDockerOption.IsLocal == true;
                if (isLocalDiskSelected && IsWindowsWslHomePath(searchPath))
                {
                    var dialogResult = await ShowWslPathWarningDialogAsync();
                    
                    switch (dialogResult)
                    {
                        case WslPathDialogResult.Cancelled:
                            return; // User closed dialog without choosing
                            
                        case WslPathDialogResult.WslSearch:
                            // User chose WSL Search - try to convert to native WSL path for faster search
                            var convertedPath = Services.WindowsSubsystemLinuxService.TryConvertToNativeWslPath(searchPath);
                            if (convertedPath != searchPath)
                            {
                                System.Diagnostics.Debug.WriteLine($"WSL Search: Converted path {searchPath} -> {convertedPath}");
                                searchPath = convertedPath;
                            }
                            else
                            {
                                // Conversion failed, silently fall back to original path
                                System.Diagnostics.Debug.WriteLine($"WSL Search: No matching WSL distribution found, using original path");
                            }
                            break;
                            
                        case WslPathDialogResult.Continue:
                            // User chose to continue with slow Windows path search - use original path as-is
                            System.Diagnostics.Debug.WriteLine($"Continue: Using slow Windows path search for {searchPath}");
                            break;
                    }
                }
                
                // Deselect Filter Options button and hide the pane when search starts
                if (FilterOptionsToggleButton != null && FilterOptionsToggleButton.IsChecked == true)
                {
                    FilterOptionsToggleButton.IsChecked = false;
                }
                if (FilterOptionsPane != null)
                {
                    FilterOptionsPane.Visibility = Visibility.Collapsed;
                }
                
                ResultsListView.ItemsSource = null;
                FilesResultsListView.ItemsSource = null;
                
                // ViewModel null check was done at method start; use ! to satisfy flow analysis after awaits
                ViewModel!.SearchPath = searchPath;
                ViewModel.SearchTerm = SearchTextBox.Text;
                ViewModel.MatchFileNames = MatchFileNamesTextBox?.Text ?? string.Empty;
                ViewModel.ExcludeDirs = excludeDirs;
                
                // Save path to recent paths
                if (!string.IsNullOrWhiteSpace(searchPath))
                {
                    RecentPathsService.AddRecentPath(searchPath);
                }
                
                // Reset column widths to defaults before search
                ResetColumnWidthsToDefaults();
                
                // Ensure column widths are initialized before setting ItemsSource
                InitializeColumnWidths();
                
                // Force header layout update to ensure widths are calculated
                if (ResultsHeaderGrid != null)
                {
                    ResultsHeaderGrid.UpdateLayout();
                }
                
                // ViewModel will update StatusText which triggers PropertyChanged
                // and shows/hides ProgressRing automatically
                await ViewModel.PerformSearchAsync();
                
                // Force another layout update after async operation
                if (ResultsHeaderGrid != null)
                {
                    ResultsHeaderGrid.UpdateLayout();
                }
                
                // Update header visibility based on search mode after search completes
                UpdateResultsHeader();
                
                // Set the appropriate items source based on search mode
                if (ViewModel.IsFilesSearch)
                {
                    if (FilesResultsListView != null)
                        FilesResultsListView.ItemsSource = ViewModel.FileSearchResults;
                }
                else
                {
                    if (ResultsListView != null)
                    {
                        ResultsListView.ItemsSource = ViewModel.SearchResults;
                        // Calculate and apply dynamic column widths based on content
                        AdjustColumnWidthsForContent();
                    }
                }
                
                // Update header visibility based on whether there are results
                UpdateResultsHeaderVisibility();
                
                // Update row widths after containers are created
                UpdateRowWidthsAfterItemsSourceSet();
            }
            catch (Exception ex)
            {
                // Handle errors that might occur outside PerformSearchAsync
                if (ViewModel != null)
                {
                    ViewModel.StatusText = $"Error: {ex.Message}";
                }
            }
            finally
            {
                UpdateSearchButtonState();
            }
        }

        private void AppBarSearchButton_Click(object sender, RoutedEventArgs e)
        {
            // If a search is in progress, cancel it instead of starting a new one
            if (ViewModel != null && ViewModel.IsSearching)
            {
                try
                {
                    ViewModel.CancelSearch();
                    UpdateSearchButtonLabel(false);
                }
                catch (Exception ex)
                {
                    Log($"AppBarSearchButton_Click (Cancel) ERROR: {ex}");
                }
                return;
            }
            
            SearchButton_Click(sender, e);
        }

        public bool CanExecuteSearchShortcut => AppBarSearchButton?.IsEnabled == true;

        public void ExecuteSearchShortcut()
        {
            if (!CanExecuteSearchShortcut)
            {
                return;
            }

            var sender = (object?)AppBarSearchButton ?? this;
            AppBarSearchButton_Click(sender!, new RoutedEventArgs());
        }

        public bool TryCancelActiveOperationFromShortcut()
        {
            if (ViewModel == null || !ViewModel.IsSearching)
            {
                return false;
            }

            if (_isCurrentOperationReplace)
            {
                var sender = (object?)AppBarReplaceButton ?? this;
                AppBarReplaceButton_Click(sender!, new RoutedEventArgs());
            }
            else
            {
                var sender = (object?)AppBarSearchButton ?? this;
                AppBarSearchButton_Click(sender!, new RoutedEventArgs());
            }

            return true;
        }

        public bool ClearSearchAndReplaceInputsFromShortcut()
        {
            bool cleared = false;

            if (SearchTextBox != null && !string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = string.Empty;
                cleared = true;
            }
            else if (ViewModel != null && !string.IsNullOrWhiteSpace(ViewModel.SearchTerm))
            {
                ViewModel.SearchTerm = string.Empty;
                cleared = true;
            }

            if (ReplaceWithTextBox != null && !string.IsNullOrWhiteSpace(ReplaceWithTextBox.Text))
            {
                ReplaceWithTextBox.Text = string.Empty;
                cleared = true;
            }
            else if (ViewModel != null && !string.IsNullOrWhiteSpace(ViewModel.ReplaceWith))
            {
                ViewModel.ReplaceWith = string.Empty;
                cleared = true;
            }

            if (cleared)
            {
                UpdateSearchButtonState();
            }

            return cleared;
        }
        
        /// <summary>
        /// Updates the search button label based on whether a search is in progress.
        /// Changes the label to "Stop" when searching, and back to "Search" when not.
        /// </summary>
        private void UpdateSearchButtonLabel(bool isSearching)
        {
            try
            {
                if (AppBarSearchButton == null)
                    return;
                    
                if (isSearching)
                {
                    // Change to "Stop" when searching
                    AppBarSearchButton.Label = _localizationService.GetLocalizedString("StopButton");
                    // Change icon to stop icon
                    if (AppBarSearchButton.Icon is FontIcon fontIcon)
                    {
                        fontIcon.Glyph = "\uE71A"; // Stop icon
                    }
                }
                else
                {
                    // Change back to "Search" when not searching
                    AppBarSearchButton.Label = _localizationService.GetLocalizedString("AppBarSearchButton.Label");
                    // Change icon back to search icon
                    if (AppBarSearchButton.Icon is FontIcon fontIcon)
                    {
                        fontIcon.Glyph = "\uE721"; // Search icon
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"UpdateSearchButtonLabel ERROR: {ex}");
            }
        }

        private void AppBarReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            // If a replace/search is in progress, cancel it instead of starting a new one
            if (ViewModel != null && ViewModel.IsSearching)
            {
                try
                {
                    ViewModel.CancelSearch();
                    UpdateReplaceButtonLabel(false);
                }
                catch (Exception ex)
                {
                    Log($"AppBarReplaceButton_Click (Cancel) ERROR: {ex}");
                }
                return;
            }
            
            ReplaceButton_Click(sender, e);
        }
        
        /// <summary>
        /// Updates the replace button label based on whether a replace is in progress.
        /// Changes the label to "Stop" when replacing, and back to "Replace" when not.
        /// </summary>
        private void UpdateReplaceButtonLabel(bool isSearching)
        {
            try
            {
                if (AppBarReplaceButton == null)
                    return;
                    
                if (isSearching)
                {
                    // Change to "Stop" when replacing
                    AppBarReplaceButton.Label = _localizationService.GetLocalizedString("StopButton");
                    // Change icon to stop icon
                    if (AppBarReplaceButton.Icon is FontIcon fontIcon)
                    {
                        fontIcon.Glyph = "\uE71A"; // Stop icon
                    }
                }
                else
                {
                    // Change back to "Replace" when not replacing
                    AppBarReplaceButton.Label = _localizationService.GetLocalizedString("AppBarReplaceButton.Label");
                    // Change icon back to replace icon
                    if (AppBarReplaceButton.Icon is FontIcon fontIcon)
                    {
                        fontIcon.Glyph = "\uE8AB"; // Replace icon
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"UpdateReplaceButtonLabel ERROR: {ex}");
            }
        }

        private void AppBarResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetButton_Click(sender, e);
        }

        private void FilterOptionsToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is AppBarToggleButton toggleButton)
            {
                FilterOptionsPane.Visibility = toggleButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            // Clear search term and results
            SearchTextBox.Text = "";
            ViewModel.SearchTerm = "";
            if (ReplaceWithTextBox != null)
            {
                ReplaceWithTextBox.Text = "";
            }
            if (ReplaceCheckBox != null)
            {
                ReplaceCheckBox.IsChecked = false;
                if (ReplaceWithTextBox != null)
                {
                    ReplaceWithTextBox.Visibility = Visibility.Collapsed;
                }
            }
            ViewModel.ReplaceWith = "";
            ViewModel.ClearResults();
            if (ResultsListView != null)
                ResultsListView.ItemsSource = null;
            if (FilesResultsListView != null)
                FilesResultsListView.ItemsSource = null;
            
            // Reset column widths to defaults
            ResetColumnWidthsToDefaults();
            
            // Reset all Filter Options to global settings from settings.json
            var defaultSettings = Services.SettingsService.GetDefaultSettings();
            ViewModel.IsRegexSearch = defaultSettings.IsRegexSearch;
            ViewModel.IsFilesSearch = defaultSettings.IsFilesSearch;
            ViewModel.RespectGitignore = defaultSettings.RespectGitignore;
            ViewModel.SearchCaseSensitive = defaultSettings.SearchCaseSensitive;
            ViewModel.IncludeSystemFiles = defaultSettings.IncludeSystemFiles;
            ViewModel.IncludeSubfolders = defaultSettings.IncludeSubfolders;
            ViewModel.IncludeHiddenItems = defaultSettings.IncludeHiddenItems;
            ViewModel.IncludeBinaryFiles = defaultSettings.IncludeBinaryFiles;
            ViewModel.IncludeSymbolicLinks = defaultSettings.IncludeSymbolicLinks;
            ViewModel.UseWindowsSearchIndex = defaultSettings.UseWindowsSearchIndex;
            ViewModel.MatchFileNames = defaultSettings.DefaultMatchFiles ?? string.Empty;
            ViewModel.ExcludeDirs = defaultSettings.DefaultExcludeDirs ?? string.Empty;
            ViewModel.SizeUnit = defaultSettings.SizeUnit;
            ViewModel.SizeLimitType = Models.SizeLimitType.NoLimit;
            ViewModel.SizeLimitKB = null;
            
            // Update UI controls to match ViewModel
            if (SearchTypeComboBox != null)
                SearchTypeComboBox.SelectedIndex = ViewModel.IsRegexSearch ? 1 : 0;
            if (SearchResultsComboBox != null)
                SearchResultsComboBox.SelectedIndex = ViewModel.IsFilesSearch ? 1 : 0;
            if (RespectGitignoreCheckBox != null)
                RespectGitignoreCheckBox.IsChecked = ViewModel.RespectGitignore;
            if (SearchCaseSensitiveCheckBox != null)
                SearchCaseSensitiveCheckBox.IsChecked = ViewModel.SearchCaseSensitive;
            if (IncludeSystemFilesCheckBox != null)
                IncludeSystemFilesCheckBox.IsChecked = ViewModel.IncludeSystemFiles;
            if (IncludeSubfoldersCheckBox != null)
                IncludeSubfoldersCheckBox.IsChecked = ViewModel.IncludeSubfolders;
            if (IncludeHiddenItemsCheckBox != null)
                IncludeHiddenItemsCheckBox.IsChecked = ViewModel.IncludeHiddenItems;
            if (IncludeBinaryFilesCheckBox != null)
                IncludeBinaryFilesCheckBox.IsChecked = ViewModel.IncludeBinaryFiles;
            if (IncludeSymbolicLinksCheckBox != null)
                IncludeSymbolicLinksCheckBox.IsChecked = ViewModel.IncludeSymbolicLinks;
            if (UseWindowsSearchCheckBox != null)
                UseWindowsSearchCheckBox.IsChecked = ViewModel.UseWindowsSearchIndex;
            if (MatchFileNamesTextBox != null)
                MatchFileNamesTextBox.Text = ViewModel.MatchFileNames;
            if (ExcludeDirsTextBox != null)
                ExcludeDirsTextBox.Text = ViewModel.ExcludeDirs;
            if (SizeLimitComboBox != null)
                SizeLimitComboBox.SelectedIndex = 0; // No Limit
            if (SizeLimitInputPanel != null)
                SizeLimitInputPanel.Visibility = Visibility.Collapsed;
            if (SizeLimitNumberBox != null)
                SizeLimitNumberBox.Text = "";
            if (SizeUnitComboBox != null)
                SizeUnitComboBox.SelectedIndex = (int)ViewModel.SizeUnit;
            
            if (ViewModel != null)
            {
                ViewModel.StatusText = "Ready";
            }
            UpdateResultsHeaderVisibility();
            UpdateSearchButtonState();
            UpdateWindowsSearchCheckboxState();
        }

        private void ReplaceCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (ReplaceWithTextBox != null)
            {
                ReplaceWithTextBox.Visibility = Visibility.Visible;
                UpdateSearchButtonState();
            }
        }

        private void ReplaceCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (ReplaceWithTextBox != null)
            {
                ReplaceWithTextBox.Visibility = Visibility.Collapsed;
                if (ViewModel != null)
                {
                    ReplaceWithTextBox.Text = "";
                    ViewModel.ReplaceWith = "";
                }
                UpdateSearchButtonState();
            }
        }

        private void ReplaceWithTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                if (ViewModel != null && ViewModel.CanReplace)
                {
                    ReplaceButton_Click(sender, e);
                }
            }
        }

        private async void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            // Mark this as a Replace operation
            _isCurrentOperationReplace = true;

            var excludeDirs = ExcludeDirsTextBox?.Text ?? string.Empty;
            
            // Validate Exclude Dirs regex if it looks like a regex pattern
            if (!string.IsNullOrEmpty(excludeDirs)) {
                if ( excludeDirs == "**" || excludeDirs == "*" ) {
                        Services.NotificationService.Instance.ShowError(
                            GetString("NoResultsPossibleTitle"), 
                            GetString("NoResultsPossibleMessage"));
                        return;
                } else
                if ( !excludeDirs.Contains(",") && (excludeDirs.Contains("^") || excludeDirs.Contains("$") || excludeDirs.Contains("|")) ) {
                    if (!IsValidRegexPattern(excludeDirs)) {
                        // Invalid regex - show error and cancel
                        Services.NotificationService.Instance.ShowError(
                            GetString("InvalidRegexPatternTitle"), 
                            GetString("InvalidRegexPatternMessage"));
                        return;
                    }
                }
            }
            
            // Check if path looks like WSL accessed through Windows (e.g., C:\home\...)
            // Offer choice between slow Windows search or faster native WSL search
            var searchPath = PathAutoSuggestBox.Text;
            if (IsWindowsWslHomePath(searchPath))
            {
                var dialogResult = await ShowWslPathWarningDialogAsync();
                
                switch (dialogResult)
                {
                    case WslPathDialogResult.Cancelled:
                        return; // User closed dialog without choosing
                        
                    case WslPathDialogResult.WslSearch:
                        // User chose WSL Search - try to convert to native WSL path for faster replace
                        var convertedPath = Services.WindowsSubsystemLinuxService.TryConvertToNativeWslPath(searchPath);
                        if (convertedPath != searchPath)
                        {
                            System.Diagnostics.Debug.WriteLine($"WSL Search: Converted path {searchPath} -> {convertedPath}");
                            searchPath = convertedPath;
                        }
                        else
                        {
                            // Conversion failed, silently fall back to original path
                            System.Diagnostics.Debug.WriteLine($"WSL Search: No matching WSL distribution found, using original path");
                        }
                        break;
                        
                    case WslPathDialogResult.Continue:
                        // User chose to continue with slow Windows path search - use original path as-is
                        System.Diagnostics.Debug.WriteLine($"Continue: Using slow Windows path for replace on {searchPath}");
                        break;
                }
            }

            // Show confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = GetString("ConfirmReplaceTitle"),
                Content = GetString("ConfirmReplaceMessage"),
                PrimaryButtonText = GetString("ProceedButton"),
                SecondaryButtonText = GetString("CancelButton"),
                DefaultButton = ContentDialogButton.Secondary,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            
            // Only proceed if user clicked "Proceed"
            if (result != ContentDialogResult.Primary)
                return;

            // Reset column widths to defaults before replace
            ResetColumnWidthsToDefaults();

            try
            {
                ResultsListView.ItemsSource = null;
                FilesResultsListView.ItemsSource = null;
                
                ViewModel.SearchPath = searchPath;
                ViewModel.SearchTerm = SearchTextBox.Text;
                ViewModel.ReplaceWith = ReplaceWithTextBox.Text;
                ViewModel.ExcludeDirs = excludeDirs;
                
                // Automatically switch to Files mode
                ViewModel.IsFilesSearch = true;
                if (SearchResultsComboBox != null)
                    SearchResultsComboBox.SelectedIndex = 1;
                
                // Save path to recent paths
                if (!string.IsNullOrWhiteSpace(searchPath))
                {
                    RecentPathsService.AddRecentPath(searchPath);
                }
                
                // Ensure column widths are initialized before setting ItemsSource
                InitializeFilesColumnWidths();
                
                // Force header layout update to ensure widths are calculated
                if (FilesHeaderGrid != null)
                {
                    FilesHeaderGrid.UpdateLayout();
                }
                
                // ViewModel will update StatusText which triggers PropertyChanged
                // and shows/hides ProgressRing automatically
                await ViewModel.PerformReplaceAsync();
                
                // Force another layout update after async operation
                if (FilesHeaderGrid != null)
                {
                    FilesHeaderGrid.UpdateLayout();
                }
                
                // Update header visibility based on search mode after replace completes
                UpdateResultsHeader();
                
                // Set the items source for Files mode
                if (FilesResultsListView != null)
                {
                    FilesResultsListView.ItemsSource = ViewModel.FileSearchResults;
                    // Update row widths after containers are created
                    UpdateAllFilesRowColumnWidths();
                }
                
                // Update header visibility based on whether there are results
                UpdateResultsHeaderVisibility();
            }
            catch (Exception ex)
            {
                // Handle errors that might occur outside PerformReplaceAsync
                if (ViewModel != null)
                {
                    ViewModel.StatusText = $"Error: {ex.Message}";
                }
            }
            finally
            {
                UpdateSearchButtonState();
            }
        }

        public void BindViewModel()
        {
            try
            {
                Log("BindViewModel: Starting");
                if (ViewModel == null)
                {
                    Log("BindViewModel: ViewModel is null, skipping");
                    return;
                }

                if (!IsLoaded)
                {
                    Log("BindViewModel: Control not loaded yet, skipping");
                    return;
                }

                // Sync UI with ViewModel - with null checks
                if (PathAutoSuggestBox != null)
                    PathAutoSuggestBox.Text = ViewModel.SearchPath;
                if (SearchTextBox != null)
                    SearchTextBox.Text = ViewModel.SearchTerm;
                if (ReplaceWithTextBox != null)
                    ReplaceWithTextBox.Text = ViewModel.ReplaceWith;
                if (SearchTypeComboBox != null)
                    SearchTypeComboBox.SelectedIndex = ViewModel.IsRegexSearch ? 1 : 0;
                if (SearchResultsComboBox != null)
                    SearchResultsComboBox.SelectedIndex = ViewModel.IsFilesSearch ? 1 : 0;
                if (RespectGitignoreCheckBox != null)
                    RespectGitignoreCheckBox.IsChecked = ViewModel.RespectGitignore;
                if (SearchCaseSensitiveCheckBox != null)
                    SearchCaseSensitiveCheckBox.IsChecked = ViewModel.SearchCaseSensitive;
                if (IncludeSystemFilesCheckBox != null)
                    IncludeSystemFilesCheckBox.IsChecked = ViewModel.IncludeSystemFiles;
                if (IncludeSubfoldersCheckBox != null)
                    IncludeSubfoldersCheckBox.IsChecked = ViewModel.IncludeSubfolders;
                if (IncludeHiddenItemsCheckBox != null)
                    IncludeHiddenItemsCheckBox.IsChecked = ViewModel.IncludeHiddenItems;
                if (IncludeBinaryFilesCheckBox != null)
                    IncludeBinaryFilesCheckBox.IsChecked = ViewModel.IncludeBinaryFiles;
                if (IncludeSymbolicLinksCheckBox != null)
                    IncludeSymbolicLinksCheckBox.IsChecked = ViewModel.IncludeSymbolicLinks;
                if (UseWindowsSearchCheckBox != null)
                    UseWindowsSearchCheckBox.IsChecked = ViewModel.UseWindowsSearchIndex;
                if (MatchFileNamesTextBox != null)
                    MatchFileNamesTextBox.Text = ViewModel.MatchFileNames;
                if (ExcludeDirsTextBox != null)
                    ExcludeDirsTextBox.Text = ViewModel.ExcludeDirs;
                if (SizeLimitComboBox != null)
                {
                    // Set the selected item based on ViewModel.SizeLimitType
                    foreach (ComboBoxItem item in SizeLimitComboBox.Items)
                    {
                        if (item.Tag is string tag)
                        {
                            var itemType = tag switch
                            {
                                "NoLimit" => Models.SizeLimitType.NoLimit,
                                "LessThan" => Models.SizeLimitType.LessThan,
                                "EqualTo" => Models.SizeLimitType.EqualTo,
                                "GreaterThan" => Models.SizeLimitType.GreaterThan,
                                _ => Models.SizeLimitType.NoLimit
                            };
                            if (itemType == ViewModel.SizeLimitType)
                            {
                                SizeLimitComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                }
                if (SizeLimitNumberBox != null)
                {
                    SizeLimitNumberBox.Text = ViewModel.SizeLimitKB?.ToString() ?? string.Empty;
                }
                if (SizeUnitComboBox != null)
                {
                    // Set the selected item based on ViewModel.SizeUnit
                    foreach (ComboBoxItem item in SizeUnitComboBox.Items)
                    {
                        if (item.Tag is string tag)
                        {
                            var itemUnit = tag switch
                            {
                                "KB" => Models.SizeUnit.KB,
                                "MB" => Models.SizeUnit.MB,
                                "GB" => Models.SizeUnit.GB,
                                _ => Models.SizeUnit.KB
                            };
                            if (itemUnit == ViewModel.SizeUnit)
                            {
                                SizeUnitComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                }
                if (SizeLimitInputPanel != null)
                {
                    SizeLimitInputPanel.Visibility = ViewModel.IsSizeLimitEnabled 
                        ? Visibility.Visible 
                        : Visibility.Collapsed;
                }
                UpdateResultsHeaderVisibility();
                if (ResultsListView != null)
                    ResultsListView.ItemsSource = ViewModel.SearchResults;
                if (FilesResultsListView != null)
                    FilesResultsListView.ItemsSource = ViewModel.FileSearchResults;
                
                // Update header visibility based on mode
                UpdateResultsHeader();
                UpdateSearchButtonState();

                // Subscribe to property changes
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                UpdateStatusInfoBar(ViewModel.StatusText);
                UpdateWindowsSearchCheckboxState();
                
                // Initialize context menu checkmarks
                UpdateAllContentColumnContextMenus();
                UpdateAllFilesColumnContextMenus();
                
                Log("BindViewModel: Completed");
            }
            catch (Exception ex)
            {
                Log($"BindViewModel ERROR: {ex}");
                Log($"BindViewModel ERROR StackTrace: {ex.StackTrace}");
            }
        }

        public void UnbindViewModel()
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
        }

        private void ColumnResizer_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb thumb)
                return;

            const double minWidth = 60;
            var delta = e.HorizontalChange;

            switch (thumb.Tag)
            {
                case "Name":
                    NameColumnWidth = CreateWidth(NameColumnDefinition.ActualWidth + delta, minWidth);
                    break;
                case "Line":
                    LineColumnWidth = CreateWidth(LineColumnDefinition.ActualWidth + delta, minWidth);
                    break;
                case "Column":
                    ColumnColumnWidth = CreateWidth(ColumnColumnDefinition.ActualWidth + delta, minWidth);
                    break;
                case "Text":
                    TextColumnWidth = CreateWidth(TextColumnDefinition.ActualWidth + delta, minWidth);
                    break;
            }
        }

        private void FilesColumnResizer_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb thumb)
                return;

            const double minWidth = 60;
            var delta = e.HorizontalChange;

            switch (thumb.Tag?.ToString())
            {
                case "FilesName":
                    FilesNameColumnWidth = CreateWidth(FilesNameColumnDefinition.ActualWidth + delta, minWidth);
                    break;
                case "FilesSize":
                    FilesSizeColumnWidth = CreateWidth(FilesSizeColumnDefinition.ActualWidth + delta, minWidth);
                    break;
                case "FilesMatches":
                    FilesMatchesColumnWidth = CreateWidth(FilesMatchesColumnDefinition.ActualWidth + delta, minWidth);
                    break;
                case "FilesPath":
                    FilesPathColumnWidth = CreateWidth(FilesPathColumnDefinition.ActualWidth + delta, minWidth);
                    break;
                case "FilesExt":
                    FilesExtColumnWidth = CreateWidth(FilesExtColumnDefinition.ActualWidth + delta, minWidth);
                    break;
                case "FilesEncoding":
                    FilesEncodingColumnWidth = CreateWidth(FilesEncodingColumnDefinition.ActualWidth + delta, minWidth);
                    break;
                case "FilesDate":
                    FilesDateColumnWidth = CreateWidth(FilesDateColumnDefinition.ActualWidth + delta, minWidth);
                    break;
            }
        }

        private static GridLength CreateWidth(double width, double minWidth)
        {
            var value = Math.Max(minWidth, width);
            return new GridLength(value, GridUnitType.Pixel);
        }

        private void ColumnResizer_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is not Thumb thumb || ViewModel == null)
                return;

            e.Handled = true;

            switch (thumb.Tag?.ToString())
            {
                case "Name":
                    AutoResizeColumn("Name", result => result.FileName);
                    break;
                case "Line":
                    AutoResizeColumn("Line", result => result.LineNumber.ToString());
                    break;
                case "Column":
                    AutoResizeColumn("Column", result => result.ColumnNumber.ToString());
                    break;
                case "Text":
                    AutoResizeColumn("Text", result => result.TrimmedLineContent);
                    break;
                case "Path":
                    AutoResizeColumn("Path", result => result.DirectoryPath);
                    break;
            }
        }

        private void FilesColumnResizer_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is not Thumb thumb || ViewModel == null)
                return;

            e.Handled = true;

            switch (thumb.Tag?.ToString())
            {
                case "FilesName":
                    AutoResizeFilesColumn("FilesName", result => result.FileName);
                    break;
                case "FilesSize":
                    AutoResizeFilesColumn("FilesSize", result => result.FormattedSize);
                    break;
                case "FilesMatches":
                    AutoResizeFilesColumn("FilesMatches", result => result.MatchCount.ToString());
                    break;
                case "FilesPath":
                    AutoResizeFilesColumn("FilesPath", result => result.DirectoryPath);
                    break;
                case "FilesExt":
                    AutoResizeFilesColumn("FilesExt", result => result.Extension);
                    break;
                case "FilesEncoding":
                    AutoResizeFilesColumn("FilesEncoding", result => result.Encoding);
                    break;
                case "FilesDate":
                    AutoResizeFilesColumn("FilesDate", result => result.FormattedDateModified);
                    break;
            }
        }

        private void AutoResizeColumn(string columnName, Func<SearchResult, string> getValue)
        {
            if (ViewModel?.SearchResults == null || ViewModel.SearchResults.Count == 0)
                return;

            // Measure header text using localization
            var headerText = columnName switch
            {
                "Name" => _localizationService.GetLocalizedString("ContentNameHeaderButton.Content"),
                "Line" => _localizationService.GetLocalizedString("ContentLineHeaderButton.Content"),
                "Column" => _localizationService.GetLocalizedString("ContentColumnHeaderButton.Content"),
                "Text" => _localizationService.GetLocalizedString("ContentTextHeaderButton.Content"),
                "Path" => _localizationService.GetLocalizedString("ContentPathHeaderButton.Content"),
                _ => ""
            };

            double maxWidth = MeasureTextWidth(headerText, 11, FontWeights.SemiBold);

            // Measure all values in the column
            foreach (var result in ViewModel.SearchResults)
            {
                var text = getValue(result);
                if (!string.IsNullOrEmpty(text))
                {
                    var width = MeasureTextWidth(text, 11, FontWeights.Normal);
                    maxWidth = Math.Max(maxWidth, width);
                }
            }

            // Add padding (8px left + 8px right + some extra for safety)
            const double padding = 24;
            maxWidth += padding;

            // Apply the width
            var minWidth = 60.0;
            var finalWidth = Math.Max(minWidth, maxWidth);

            switch (columnName)
            {
                case "Name":
                    NameColumnWidth = new GridLength(finalWidth, GridUnitType.Pixel);
                    break;
                case "Line":
                    LineColumnWidth = new GridLength(finalWidth, GridUnitType.Pixel);
                    break;
                case "Column":
                    ColumnColumnWidth = new GridLength(finalWidth, GridUnitType.Pixel);
                    break;
                case "Text":
                    TextColumnWidth = new GridLength(finalWidth, GridUnitType.Pixel);
                    break;
                case "Path":
                    PathColumnWidth = new GridLength(finalWidth, GridUnitType.Pixel);
                    break;
            }

            // Force layout update on header grid
            if (ResultsHeaderGrid != null)
            {
                ResultsHeaderGrid.UpdateLayout();
            }

            // Update row widths using the calculated width directly
            var newWidth = new GridLength(finalWidth, GridUnitType.Pixel);
            UpdateRowColumnWidth(columnName, newWidth);

            // Also update all rows after layout completes
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ResultsHeaderGrid != null)
                {
                    ResultsHeaderGrid.UpdateLayout();
                }
                UpdateAllRowColumnWidths();
            });
        }

        private void UpdateRowColumnWidth(string columnName, GridLength width)
        {
            if (ResultsListView == null || ResultsListView.Items.Count == 0)
                return;

            int columnIndex = columnName switch
            {
                "Name" => 0,
                "Line" => 1,
                "Column" => 2,
                "Text" => 3,
                "Path" => 4,
                _ => -1
            };

            if (columnIndex < 0)
                return;

            for (int i = 0; i < ResultsListView.Items.Count; i++)
            {
                if (ResultsListView.ContainerFromIndex(i) is ListViewItem container &&
                    container.ContentTemplateRoot is Border border &&
                    border.Child is Grid grid &&
                    grid.ColumnDefinitions.Count > columnIndex)
                {
                    grid.ColumnDefinitions[columnIndex].Width = width;
                }
            }
        }

        private void AutoResizeFilesColumn(string columnName, Func<FileSearchResult, string> getValue)
        {
            if (ViewModel?.FileSearchResults == null || ViewModel.FileSearchResults.Count == 0)
                return;

            // Measure header text using localization
            var headerText = columnName switch
            {
                "FilesName" => _localizationService.GetLocalizedString("FilesNameHeaderButton.Content"),
                "FilesSize" => _localizationService.GetLocalizedString("FilesSizeHeaderButton.Content"),
                "FilesMatches" => _localizationService.GetLocalizedString("FilesMatchesHeaderButton.Content"),
                "FilesPath" => _localizationService.GetLocalizedString("FilesPathHeaderButton.Content"),
                "FilesExt" => _localizationService.GetLocalizedString("FilesExtHeaderButton.Content"),
                "FilesEncoding" => _localizationService.GetLocalizedString("FilesEncodingHeaderButton.Content"),
                "FilesDate" => _localizationService.GetLocalizedString("FilesDateModifiedHeaderButton.Content"),
                _ => ""
            };

            double maxWidth = MeasureTextWidth(headerText, 11, FontWeights.SemiBold);

            // Measure all values in the column
            foreach (var result in ViewModel.FileSearchResults)
            {
                var text = getValue(result);
                if (!string.IsNullOrEmpty(text))
                {
                    var width = MeasureTextWidth(text, 11, FontWeights.Normal);
                    maxWidth = Math.Max(maxWidth, width);
                }
            }

            // Add padding (8px left + 8px right + some extra for safety)
            const double padding = 24;
            maxWidth += padding;

            // Apply the width
            var minWidth = 60.0;
            var finalWidth = Math.Max(minWidth, maxWidth);

            switch (columnName)
            {
                case "FilesName":
                    FilesNameColumnWidth = new GridLength(finalWidth, GridUnitType.Pixel);
                    break;
                case "FilesSize":
                    FilesSizeColumnWidth = new GridLength(finalWidth, GridUnitType.Pixel);
                    break;
                case "FilesMatches":
                    FilesMatchesColumnWidth = new GridLength(finalWidth, GridUnitType.Pixel);
                    break;
                case "FilesPath":
                    FilesPathColumnWidth = new GridLength(finalWidth, GridUnitType.Pixel);
                    break;
                case "FilesExt":
                    FilesExtColumnWidth = new GridLength(finalWidth, GridUnitType.Pixel);
                    break;
                case "FilesEncoding":
                    FilesEncodingColumnWidth = new GridLength(finalWidth, GridUnitType.Pixel);
                    break;
                case "FilesDate":
                    FilesDateColumnWidth = new GridLength(finalWidth, GridUnitType.Pixel);
                    break;
            }

            // Force layout update on header grid
            if (FilesHeaderGrid != null)
            {
                FilesHeaderGrid.UpdateLayout();
            }

            // Update row widths using the calculated width directly
            var newWidth = new GridLength(finalWidth, GridUnitType.Pixel);
            UpdateFilesRowColumnWidth(columnName, newWidth);

            // Also update all rows after layout completes
            DispatcherQueue.TryEnqueue(() =>
            {
                if (FilesHeaderGrid != null)
                {
                    FilesHeaderGrid.UpdateLayout();
                }
                UpdateAllFilesRowColumnWidths();
            });
        }

        private void UpdateFilesRowColumnWidth(string columnName, GridLength width)
        {
            if (FilesResultsListView == null || FilesResultsListView.Items.Count == 0)
                return;

            int columnIndex = columnName switch
            {
                "FilesName" => 0,
                "FilesSize" => 1,
                "FilesMatches" => 2,
                "FilesPath" => 3,
                "FilesExt" => 4,
                "FilesEncoding" => 5,
                "FilesDate" => 6,
                _ => -1
            };

            if (columnIndex < 0)
                return;

            for (int i = 0; i < FilesResultsListView.Items.Count; i++)
            {
                if (FilesResultsListView.ContainerFromIndex(i) is ListViewItem container &&
                    container.ContentTemplateRoot is Border border &&
                    border.Child is Grid grid &&
                    grid.ColumnDefinitions.Count > columnIndex)
                {
                    grid.ColumnDefinitions[columnIndex].Width = width;
                }
            }
        }

        private static double MeasureTextWidth(string text, double fontSize, FontWeight fontWeight)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            try
            {
                // Create a temporary TextBlock to measure the text
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontSize = fontSize,
                    FontWeight = fontWeight
                };

                // Measure the text
                textBlock.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                return textBlock.DesiredSize.Width;
            }
            catch
            {
                // Fallback: estimate based on character count
                // Average character width for font size 11 is approximately 6.5 pixels
                return text.Length * 6.5;
            }
        }

        private void ResultsListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
                return;

            if (args.ItemContainer is ListViewItem item &&
                item.ContentTemplateRoot is Border border &&
                border.Child is Grid grid)
            {
                // Determine which ListView this is and apply appropriate widths
                if (sender == ResultsListView)
                {
                    // Always apply current header widths to ensure no overlap
                    ApplyWidthsToRowGrid(grid);
                }
                else if (sender == FilesResultsListView)
                {
                    // Apply Files header widths
                    ApplyWidthsToFilesRowGrid(grid);
                }
                
                // Attach double-tap handler to the ListViewItem
                item.DoubleTapped -= ListViewItem_DoubleTapped; // Remove first to avoid duplicates
                item.DoubleTapped += ListViewItem_DoubleTapped;
                
                // Also attach to the Border and Grid for better coverage
                border.DoubleTapped -= ListViewItem_DoubleTapped;
                border.DoubleTapped += ListViewItem_DoubleTapped;
                grid.DoubleTapped -= ListViewItem_DoubleTapped;
                grid.DoubleTapped += ListViewItem_DoubleTapped;
            }
        }
        
        private void UpdateRowWidthsAfterItemsSourceSet()
        {
            // Ensure header is measured first
            if (ResultsHeaderGrid != null)
            {
                ResultsHeaderGrid.UpdateLayout();
            }
            
            // Use LayoutUpdated event to ensure containers are created
            int updateAttempts = 0;
            const int maxAttempts = 3;
            
            void OnLayoutUpdated(object? s, object? e)
            {
                updateAttempts++;
                
                // Force header layout update to ensure widths are calculated
                if (ResultsHeaderGrid != null)
                {
                    ResultsHeaderGrid.UpdateLayout();
                }
                
                // Check if header columns are measured
                bool headerMeasured = ResultsHeaderGrid != null &&
                    NameColumnDefinition?.ActualWidth > 0 &&
                    TextColumnDefinition?.ActualWidth > 0 &&
                    PathColumnDefinition?.ActualWidth > 0;
                
                if (headerMeasured || updateAttempts >= maxAttempts)
                {
                    ResultsListView.LayoutUpdated -= OnLayoutUpdated;
                    // Update all row widths with actual measured widths
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateAllRowColumnWidths();
                    });
                }
            }
            
            ResultsListView.LayoutUpdated += OnLayoutUpdated;
            
            // Also do an immediate update attempt
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ResultsHeaderGrid != null)
                {
                    ResultsHeaderGrid.UpdateLayout();
                }
                UpdateAllRowColumnWidths();
            });
        }

        private void InitializeColumnWidths()
        {
            ApplyHeaderWidth(NameColumnDefinition, NameColumnWidth);
            ApplyHeaderWidth(LineColumnDefinition, LineColumnWidth);
            ApplyHeaderWidth(ColumnColumnDefinition, ColumnColumnWidth);
            ApplyHeaderWidth(TextColumnDefinition, TextColumnWidth);
            ApplyHeaderWidth(PathColumnDefinition, PathColumnWidth);
            UpdateAllRowColumnWidths();
        }

        private void ResetColumnWidthsToDefaults()
        {
            // Reset Content mode columns to defaults
            NameColumnWidth = new GridLength(220);
            LineColumnWidth = new GridLength(80);
            ColumnColumnWidth = new GridLength(90);
            TextColumnWidth = new GridLength(2, GridUnitType.Star);
            PathColumnWidth = new GridLength(2, GridUnitType.Star);
            
            // Reset Files mode columns to defaults
            FilesNameColumnWidth = new GridLength(220);
            FilesSizeColumnWidth = new GridLength(100);
            FilesMatchesColumnWidth = new GridLength(80);
            FilesPathColumnWidth = new GridLength(2, GridUnitType.Star);
            FilesExtColumnWidth = new GridLength(80);
            FilesEncodingColumnWidth = new GridLength(100);
            FilesDateColumnWidth = new GridLength(150);
            
            // Force layout update
            if (ResultsHeaderGrid != null)
            {
                ResultsHeaderGrid.UpdateLayout();
            }
            if (FilesHeaderGrid != null)
            {
                FilesHeaderGrid.UpdateLayout();
            }
            UpdateAllRowColumnWidths();
            UpdateAllFilesRowColumnWidths();
        }

        private void InitializeFilesColumnWidths()
        {
            ApplyHeaderWidth(FilesNameColumnDefinition, FilesNameColumnWidth);
            ApplyHeaderWidth(FilesSizeColumnDefinition, FilesSizeColumnWidth);
            ApplyHeaderWidth(FilesMatchesColumnDefinition, FilesMatchesColumnWidth);
            ApplyHeaderWidth(FilesPathColumnDefinition, FilesPathColumnWidth);
            ApplyHeaderWidth(FilesExtColumnDefinition, FilesExtColumnWidth);
            ApplyHeaderWidth(FilesEncodingColumnDefinition, FilesEncodingColumnWidth);
            ApplyHeaderWidth(FilesDateColumnDefinition, FilesDateColumnWidth);
            UpdateAllFilesRowColumnWidths();
        }

        private static void ApplyHeaderWidth(ColumnDefinition? column, GridLength width)
        {
            if (column != null)
            {
                column.Width = width;
            }
        }

        private void AdjustColumnWidthsForContent()
        {
            if (ViewModel == null || ViewModel.SearchResults == null || ViewModel.SearchResults.Count == 0)
            {
                // Reset to defaults when no results
                NameColumnWidth = new GridLength(220);
                PathColumnWidth = new GridLength(2, GridUnitType.Star);
                
                // Force layout update after resetting
                if (ResultsHeaderGrid != null)
                {
                    ResultsHeaderGrid.UpdateLayout();
                }
                return;
            }

            // Check Name column: if any filename is longer than 30 characters, use larger width
            const int nameThreshold = 30;
            const double nameSmallWidth = 90; // Same as Column column
            const double nameLargeWidth = 220; // Current default
            
            bool hasLongFileName = ViewModel.SearchResults.Any(r => 
                !string.IsNullOrEmpty(r.FileName) && r.FileName.Length > nameThreshold);
            
            NameColumnWidth = new GridLength(hasLongFileName ? nameLargeWidth : nameSmallWidth);

            // Check Path column: if all paths are empty, use minimal width
            const double pathMinimalWidth = 60;
            bool hasAnyPath = ViewModel.SearchResults.Any(r => 
                !string.IsNullOrWhiteSpace(r.DirectoryPath));
            
            if (hasAnyPath)
            {
                // Use star sizing to share remaining space with Text column
                PathColumnWidth = new GridLength(2, GridUnitType.Star);
            }
            else
            {
                // All paths are empty, use minimal width
                PathColumnWidth = new GridLength(pathMinimalWidth);
            }
            
            // Force layout update after adjusting column widths
            if (ResultsHeaderGrid != null)
            {
                ResultsHeaderGrid.UpdateLayout();
            }
        }

        private void UpdateAllRowColumnWidths()
        {
            if (ResultsListView == null || ResultsListView.Items.Count == 0)
                return;

            for (int i = 0; i < ResultsListView.Items.Count; i++)
            {
                if (ResultsListView.ContainerFromIndex(i) is ListViewItem container &&
                    container.ContentTemplateRoot is Border border &&
                    border.Child is Grid grid)
                {
                    ApplyWidthsToRowGrid(grid);
                }
            }
        }

        private void ApplyWidthsToRowGrid(Grid grid)
        {
            if (grid is null)
                return;

            if (grid.ColumnDefinitions.Count < 5)
                return;

            // Always use actual measured widths from header to ensure perfect alignment
            // This prevents any overlap between columns
            grid.ColumnDefinitions[0].Width = GetActualHeaderWidth(NameColumnDefinition, new GridLength(220));
            grid.ColumnDefinitions[1].Width = GetActualHeaderWidth(LineColumnDefinition, new GridLength(80));
            grid.ColumnDefinitions[2].Width = GetActualHeaderWidth(ColumnColumnDefinition, new GridLength(90));
            grid.ColumnDefinitions[3].Width = GetActualHeaderWidth(TextColumnDefinition, new GridLength(2, GridUnitType.Star));
            grid.ColumnDefinitions[4].Width = GetActualHeaderWidth(PathColumnDefinition, new GridLength(2, GridUnitType.Star));
        }

        private void UpdateAllFilesRowColumnWidths()
        {
            if (FilesResultsListView == null || FilesResultsListView.Items.Count == 0)
                return;

            for (int i = 0; i < FilesResultsListView.Items.Count; i++)
            {
                if (FilesResultsListView.ContainerFromIndex(i) is ListViewItem container &&
                    container.ContentTemplateRoot is Border border &&
                    border.Child is Grid grid)
                {
                    ApplyWidthsToFilesRowGrid(grid);
                }
            }
        }

        private void ApplyWidthsToFilesRowGrid(Grid grid)
        {
            if (grid is null)
                return;

            if (grid.ColumnDefinitions.Count < 7)
                return;

            // Check if Files header grid exists and is loaded
            if (FilesHeaderGrid == null)
            {
                // Use default widths if header isn't available yet
                grid.ColumnDefinitions[0].Width = new GridLength(220);
                grid.ColumnDefinitions[1].Width = new GridLength(100);
                grid.ColumnDefinitions[2].Width = new GridLength(80);
                grid.ColumnDefinitions[3].Width = new GridLength(2, GridUnitType.Star);
                grid.ColumnDefinitions[4].Width = new GridLength(80);
                grid.ColumnDefinitions[5].Width = new GridLength(100);
                grid.ColumnDefinitions[6].Width = new GridLength(150);
                return;
            }

            // Always use actual measured widths from header to ensure perfect alignment
            grid.ColumnDefinitions[0].Width = GetActualHeaderWidth(FilesNameColumnDefinition, new GridLength(220));
            grid.ColumnDefinitions[1].Width = GetActualHeaderWidth(FilesSizeColumnDefinition, new GridLength(100));
            grid.ColumnDefinitions[2].Width = GetActualHeaderWidth(FilesMatchesColumnDefinition, new GridLength(80));
            grid.ColumnDefinitions[3].Width = GetActualHeaderWidth(FilesPathColumnDefinition, new GridLength(2, GridUnitType.Star));
            grid.ColumnDefinitions[4].Width = GetActualHeaderWidth(FilesExtColumnDefinition, new GridLength(80));
            grid.ColumnDefinitions[5].Width = GetActualHeaderWidth(FilesEncodingColumnDefinition, new GridLength(100));
            grid.ColumnDefinitions[6].Width = GetActualHeaderWidth(FilesDateColumnDefinition, new GridLength(150));
        }

        private static GridLength GetActualHeaderWidth(ColumnDefinition? column, GridLength fallback)
        {
            if (column == null)
                return fallback;
            
            // If the column has been measured, always use the actual pixel width
            // This ensures row columns exactly match header columns, preventing overlap
            var actualWidth = column.ActualWidth;
            if (actualWidth > 0 && double.IsFinite(actualWidth))
            {
                return new GridLength(actualWidth, GridUnitType.Pixel);
            }
            
            // If not measured yet, use the set width
            // For Star columns, this will be recalculated once measured
            return column.Width;
        }
        
        private static GridLength GetHeaderWidthOrDefault(ColumnDefinition? column, GridLength fallback)
        {
            return column is null ? fallback : column.Width;
        }

        private void UpdateResultsHeader()
        {
            if (ViewModel == null)
                return;

            // Show/hide the appropriate table based on mode
            if (ViewModel.IsFilesSearch)
            {
                if (ContentResultsGrid != null)
                    ContentResultsGrid.Visibility = Visibility.Collapsed;
                if (FilesResultsGrid != null)
                    FilesResultsGrid.Visibility = Visibility.Visible;
            }
            else
            {
                if (ContentResultsGrid != null)
                    ContentResultsGrid.Visibility = Visibility.Visible;
                if (FilesResultsGrid != null)
                    FilesResultsGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void NameHeaderButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.SortResults(SearchResultSortField.FileName);
        }

        private void ExtHeaderButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.SortResults(SearchResultSortField.Extension);
        }

        private void EncodingHeaderButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.SortResults(SearchResultSortField.Encoding);
        }

        private void MatchesHeaderButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.SortResults(SearchResultSortField.MatchCount);
        }

        private void ListViewItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            Log($"ListViewItem_DoubleTapped: Event triggered from {sender?.GetType().Name}");
            
            // Try to find the ListViewItem from the sender
            ListViewItem? listViewItem = null;
            
            if (sender is ListViewItem item)
            {
                listViewItem = item;
            }
            else if (sender is FrameworkElement element)
            {
                // Traverse up to find the ListViewItem
                listViewItem = FindParent<ListViewItem>(element);
            }
            
            if (listViewItem != null)
            {
                if (listViewItem.Content is SearchResult result)
                {
                    Log($"ListViewItem_DoubleTapped: Found SearchResult: {result.FileName} at line {result.LineNumber}, column {result.ColumnNumber}");
                    OpenFileInEditor(result);
                    e.Handled = true;
                    return;
                }
                else if (listViewItem.Content is FileSearchResult fileResult)
                {
                    Log($"ListViewItem_DoubleTapped: Found FileSearchResult: {fileResult.FileName}");
                    // Convert FileSearchResult to SearchResult for opening
                    var searchResult = new SearchResult
                    {
                        FileName = fileResult.FileName,
                        FullPath = fileResult.FullPath,
                        RelativePath = fileResult.RelativePath,
                        LineNumber = 1,
                        ColumnNumber = 1,
                        LineContent = string.Empty
                    };
                    OpenFileInEditor(searchResult);
                    e.Handled = true;
                    return;
                }
                else
                {
                    Log($"ListViewItem_DoubleTapped: Content is not SearchResult or FileSearchResult: {listViewItem.Content?.GetType().Name ?? "null"}");
                }
            }
            else
            {
                Log($"ListViewItem_DoubleTapped: Could not find ListViewItem from sender: {sender?.GetType().Name ?? "null"}");
            }
        }

        private DateTime _lastClickTime = DateTime.MinValue;
        private SearchResult? _lastClickedItem = null;
        private const int DoubleClickIntervalMs = 500;

        private void ResultsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is SearchResult result)
            {
                var now = DateTime.Now;
                var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
                
                // Check if this is a double-click (within 500ms and same item)
                if (timeSinceLastClick < DoubleClickIntervalMs && _lastClickedItem == result)
                {
                    Log($"ResultsListView_ItemClick: Double-click detected on {result.FileName}");
                    OpenFileInEditor(result);
                    _lastClickedItem = null; // Reset to prevent triple-click
                    _lastClickTime = DateTime.MinValue;
                }
                else
                {
                    // Store for potential double-click
                    _lastClickedItem = result;
                    _lastClickTime = now;
                }
            }
        }

        private void ResultsListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            // This handler is kept as a fallback
            Log("ResultsListView_DoubleTapped: Event triggered (fallback)");
            
            if (sender is not ListView listView)
            {
                Log("ResultsListView_DoubleTapped: Sender is not ListView");
                return;
            }

            // Try to get the item from the clicked element by traversing the visual tree
            if (e.OriginalSource is FrameworkElement element)
            {
                Log($"ResultsListView_DoubleTapped: OriginalSource is {element.GetType().Name}");
                var listViewItem = FindParent<ListViewItem>(element);
                if (listViewItem != null)
                {
                    Log($"ResultsListView_DoubleTapped: Found ListViewItem");
                    if (listViewItem.Content is SearchResult result)
                    {
                        Log($"ResultsListView_DoubleTapped: Found SearchResult via fallback: {result.FileName} at line {result.LineNumber}");
                        OpenFileInEditor(result);
                        e.Handled = true;
                        return;
                    }
                    else
                    {
                        Log($"ResultsListView_DoubleTapped: ListViewItem.Content is not SearchResult: {listViewItem.Content?.GetType().Name ?? "null"}");
                    }
                }
                else
                {
                    Log("ResultsListView_DoubleTapped: Could not find ListViewItem in visual tree");
                }
            }
            else
            {
                Log($"ResultsListView_DoubleTapped: OriginalSource is not FrameworkElement: {e.OriginalSource?.GetType().Name ?? "null"}");
            }
        }

        private void ResultsListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            Log("ResultsListView_RightTapped: Event triggered");
            
            try
            {
                // Get the clicked element
                if (e.OriginalSource is FrameworkElement element)
                {
                    // Find the ListViewItem parent
                    var listViewItem = FindParent<ListViewItem>(element);
                    if (listViewItem != null && listViewItem.Content is SearchResult result)
                    {
                        Log($"ResultsListView_RightTapped: Found SearchResult: {result.FileName}");
                        
                        // Get screen position - convert window coordinates to screen coordinates
                        var position = e.GetPosition(null);
                        // For MenuFlyout.ShowAt with null, we need screen coordinates
                        // Get the window's position and add the relative position
                        var window = Microsoft.UI.Xaml.Window.Current;
                        int screenX, screenY;
                        if (window != null && window.Content is FrameworkElement rootElement)
                        {
                            var transform = rootElement.TransformToVisual(null);
                            var windowScreenPoint = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                            screenX = (int)(windowScreenPoint.X + position.X);
                            screenY = (int)(windowScreenPoint.Y + position.Y);
                        }
                        else
                        {
                            // Fallback: use relative coordinates
                            screenX = (int)position.X;
                            screenY = (int)position.Y;
                        }
                        
                        // Show context menu - pass the element to get XamlRoot from it
                        if (ViewModel?.IsDockerModeActive == true)
                        {
                            var containerPath = ViewModel.ResolveDockerPath(result.FullPath);
                            ShowDockerContextMenu(containerPath, result.FileName, element);
                            e.Handled = true;
                            return;
                        }

                        _contextMenuService.ShowContextMenu(result.FullPath, screenX, screenY, element);
                        e.Handled = true;
                    }
                    else
                    {
                        Log("ResultsListView_RightTapped: Not clicking on a SearchResult item");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"ResultsListView_RightTapped ERROR: {ex.Message}");
                Log($"ResultsListView_RightTapped ERROR StackTrace: {ex.StackTrace}");
            }
        }

        private void FilesResultsListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            Log("FilesResultsListView_RightTapped: Event triggered");
            
            try
            {
                // Get the clicked element
                if (e.OriginalSource is FrameworkElement element)
                {
                    // Find the ListViewItem parent
                    var listViewItem = FindParent<ListViewItem>(element);
                    if (listViewItem != null && listViewItem.Content is FileSearchResult fileResult)
                    {
                        Log($"FilesResultsListView_RightTapped: Found FileSearchResult: {fileResult.FileName}");
                        
                        // Get screen position - convert window coordinates to screen coordinates
                        var position = e.GetPosition(null);
                        // For MenuFlyout.ShowAt with null, we need screen coordinates
                        // Get the window's position and add the relative position
                        var window = Microsoft.UI.Xaml.Window.Current;
                        int screenX, screenY;
                        if (window != null && window.Content is FrameworkElement rootElement)
                        {
                            var transform = rootElement.TransformToVisual(null);
                            var windowScreenPoint = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                            screenX = (int)(windowScreenPoint.X + position.X);
                            screenY = (int)(windowScreenPoint.Y + position.Y);
                        }
                        else
                        {
                            // Fallback: use relative coordinates
                            screenX = (int)position.X;
                            screenY = (int)position.Y;
                        }
                        
                        // Show context menu - pass the element to get XamlRoot from it
                        if (ViewModel?.IsDockerModeActive == true)
                        {
                            var containerPath = ViewModel.ResolveDockerPath(fileResult.FullPath);
                            ShowDockerContextMenu(containerPath, fileResult.FileName, element);
                            e.Handled = true;
                            return;
                        }

                        _contextMenuService.ShowContextMenu(fileResult.FullPath, screenX, screenY, element);
                        e.Handled = true;
                    }
                    else
                    {
                        Log("FilesResultsListView_RightTapped: Not clicking on a FileSearchResult item");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"FilesResultsListView_RightTapped ERROR: {ex.Message}");
                Log($"FilesResultsListView_RightTapped ERROR StackTrace: {ex.StackTrace}");
            }
        }

        private void ShowDockerContextMenu(string containerPath, string fileName, FrameworkElement anchor)
        {
            try
            {
                var menu = new MenuFlyout
                {
                    XamlRoot = anchor.XamlRoot
                };

                var copyPathItem = new MenuFlyoutItem
                {
                    Text = GetString("DockerContextCopyPath")
                };
                copyPathItem.Click += (_, _) => CopyTextToClipboard(containerPath);
                menu.Items.Add(copyPathItem);

                var copyNameItem = new MenuFlyoutItem
                {
                    Text = GetString("DockerContextCopyName")
                };
                copyNameItem.Click += (_, _) => CopyTextToClipboard(fileName);
                menu.Items.Add(copyNameItem);

                menu.ShowAt(anchor);
            }
            catch (Exception ex)
            {
                Log($"ShowDockerContextMenu ERROR: {ex}");
            }
        }

        private static void CopyTextToClipboard(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    return;

                var package = new DataPackage();
                package.SetText(text);
                Clipboard.SetContent(package);
            }
            catch (Exception ex)
            {
                Log($"CopyTextToClipboard ERROR: {ex}");
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T parentOfType)
                {
                    return parentOfType;
                }
                parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private string ConvertWslPathToWindows(string wslPath)
        {
            // If it's already a Windows path, return as-is
            if (!wslPath.StartsWith("/") || (wslPath.Length > 2 && wslPath[1] == ':'))
            {
                return wslPath;
            }

            // Get the search path from ViewModel to determine the WSL distribution
            string? searchPath = ViewModel?.SearchPath;
            
            // Use wsl.localhost format (keep the same format as the search path)
            string wslPrefix = "\\\\wsl.localhost";
            
            if (string.IsNullOrWhiteSpace(searchPath))
            {
                Log($"ConvertWslPathToWindows: No search path available, using fallback for: {wslPath}");
                // Fallback: try to construct from the WSL path directly
                var defaultDistribution = "Ubuntu-24.04";
                var fallbackWslParts = wslPath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                var fallbackPath = $"{wslPrefix}\\{defaultDistribution}\\{string.Join("\\", fallbackWslParts)}";
                Log($"ConvertWslPathToWindows: Using fallback conversion: {wslPath} to {fallbackPath}");
                return fallbackPath;
            }

            // If search path is a WSL path (\\wsl.localhost\... or \\wsl$\...)
            if (searchPath.StartsWith("\\\\wsl.localhost", StringComparison.OrdinalIgnoreCase) ||
                searchPath.StartsWith("\\\\wsl$", StringComparison.OrdinalIgnoreCase))
            {
                // Extract the distribution name
                // Format: \\wsl.localhost\Ubuntu-24.04\home\user\... or \\wsl$\Ubuntu-24.04\home\user\...
                var parts = searchPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    // parts[0] = "wsl.localhost" or "wsl$"
                    // parts[1] = distribution name (e.g., "Ubuntu-24.04")
                    var distribution = parts[1];
                    
                    // Use the same prefix format as the search path
                    if (searchPath.StartsWith("\\\\wsl$", StringComparison.OrdinalIgnoreCase))
                    {
                        wslPrefix = "\\\\wsl$";
                    }
                    
                    // Convert WSL path to Windows path
                    // wslPath is like /home/user/projects/Grex/.env
                    // We need to construct: \\wsl.localhost\{distribution}\home\user\...
                    var wslPathParts = wslPath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                    var windowsPath = $"{wslPrefix}\\{distribution}\\{string.Join("\\", wslPathParts)}";
                    
                    Log($"ConvertWslPathToWindows: Converted {wslPath} to {windowsPath} (distribution: {distribution})");
                    return windowsPath;
                }
            }

            // Fallback: try to construct from the WSL path directly
            // Assume default distribution if we can't determine it
            var defaultDist = "Ubuntu-24.04";
            var finalWslParts = wslPath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var fallback = $"{wslPrefix}\\{defaultDist}\\{string.Join("\\", finalWslParts)}";
            
            Log($"ConvertWslPathToWindows: Using fallback conversion: {wslPath} to {fallback}");
            return fallback;
        }

        private void OpenFileInEditor(SearchResult result)
        {
            if (string.IsNullOrWhiteSpace(result.FullPath))
            {
                Log("OpenFileInEditor: FullPath is empty");
                return;
            }

            // Convert WSL path to Windows path if needed
            string windowsPath = ConvertWslPathToWindows(result.FullPath);
            
            // For WSL paths, we can't use File.Exists, so we'll try to open it anyway
            // The file system will handle the path resolution
            bool pathExists = false;
            if (windowsPath.StartsWith("\\\\wsl", StringComparison.OrdinalIgnoreCase))
            {
                // For WSL paths, we can't reliably check existence, so we'll try to open it
                pathExists = true;
                Log($"OpenFileInEditor: WSL path detected, will attempt to open: {windowsPath}");
            }
            else
            {
                pathExists = File.Exists(windowsPath);
            }
            
            if (!pathExists)
            {
                Log($"OpenFileInEditor: File does not exist: {windowsPath} (original: {result.FullPath})");
                return;
            }

            try
            {
                var fileExtension = Path.GetExtension(windowsPath).ToLowerInvariant();
                var lineNumber = result.LineNumber;
                var columnNumber = result.ColumnNumber;

                // Handle specific file types
                switch (fileExtension)
                {
                    case ".env":
                        OpenInNotepadPlusPlus(windowsPath, lineNumber, columnNumber);
                        break;
                    case ".php":
                    case ".php3":
                    case ".php4":
                    case ".php5":
                    case ".phtml":
                        OpenInPhpStorm(windowsPath, lineNumber, columnNumber);
                        break;
                    default:
                        // Use default application for other file types
                        OpenWithDefaultApplication(windowsPath, lineNumber, columnNumber);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"OpenFileInEditor ERROR: {ex.Message}");
                Log($"OpenFileInEditor ERROR StackTrace: {ex.StackTrace}");
                // Try to open with default application as fallback
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = windowsPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception fallbackEx)
                {
                    Log($"OpenFileInEditor FALLBACK ERROR: {fallbackEx.Message}");
                }
            }
        }

        private void OpenInNotepadPlusPlus(string filePath, int lineNumber, int columnNumber)
        {
            // Try common Notepad++ installation paths
            var possiblePaths = new[]
            {
                @"C:\Program Files\Notepad++\notepad++.exe",
                @"C:\Program Files (x86)\Notepad++\notepad++.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Notepad++", "notepad++.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Notepad++", "notepad++.exe")
            };

            string? notepadPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    notepadPath = path;
                    break;
                }
            }

            if (notepadPath == null)
            {
                // Try to find it in PATH
                notepadPath = "notepad++.exe";
            }

            var arguments = $"-n{lineNumber} -c{columnNumber} \"{filePath}\"";
            Log($"OpenInNotepadPlusPlus: {notepadPath} {arguments}");

            Process.Start(new ProcessStartInfo
            {
                FileName = notepadPath,
                Arguments = arguments,
                UseShellExecute = false
            });
        }

        private void OpenInPhpStorm(string filePath, int lineNumber, int columnNumber)
        {
            // Try common PhpStorm installation paths
            var possiblePaths = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PhpStorm", "bin", "phpstorm64.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PhpStorm", "bin", "phpstorm.exe"),
                @"C:\Program Files\JetBrains\PhpStorm\bin\phpstorm64.exe",
                @"C:\Program Files\JetBrains\PhpStorm\bin\phpstorm.exe",
                @"C:\Program Files (x86)\JetBrains\PhpStorm\bin\phpstorm64.exe",
                @"C:\Program Files (x86)\JetBrains\PhpStorm\bin\phpstorm.exe"
            };

            // Also check for command-line launcher in common locations
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var jetbrainsPath = Path.Combine(localAppData, "JetBrains");
            if (Directory.Exists(jetbrainsPath))
            {
                // Look for PhpStorm installations in JetBrains folder
                var jetbrainsDirs = Directory.GetDirectories(jetbrainsPath, "PhpStorm*");
                foreach (var dir in jetbrainsDirs)
                {
                    var binPath = Path.Combine(dir, "bin", "phpstorm64.exe");
                    if (File.Exists(binPath))
                    {
                        possiblePaths.Add(binPath);
                    }
                }
            }

            string? phpstormPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    phpstormPath = path;
                    break;
                }
            }

            if (phpstormPath == null)
            {
                // Try to find it in PATH or use the command line launcher
                // PhpStorm often installs a launcher script
                phpstormPath = "phpstorm64.exe";
            }

            // PhpStorm - just open the file (line numbers not supported via command line)
            var arguments = $"\"{filePath}\"";
            Log($"OpenInPhpStorm: {phpstormPath} {arguments}");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = phpstormPath,
                    Arguments = arguments,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                Log($"OpenInPhpStorm ERROR: {ex.Message}");
                // Fallback: just open the file with default application
                // PhpStorm might be associated with the file type
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception fallbackEx)
                {
                    Log($"OpenInPhpStorm FALLBACK ERROR: {fallbackEx.Message}");
                }
            }
        }

        private void OpenWithDefaultApplication(string filePath, int lineNumber, int columnNumber)
        {
            // For default applications, we can try to use the file association
            // Some editors support command line arguments for line/column
            // But most will just open the file
            Log($"OpenWithDefaultApplication: {filePath} (line {lineNumber}, column {columnNumber})");

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }

        private void ContentContextMenu_Opening(object sender, object e)
        {
            UpdateAllContentColumnContextMenus();
        }

        private void ContentColumnContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string columnName && ViewModel != null)
            {
                // Toggle visibility
                switch (columnName)
                {
                    case "Line":
                        ViewModel.ContentLineColumnVisible = !ViewModel.ContentLineColumnVisible;
                        break;
                    case "Column":
                        ViewModel.ContentColumnColumnVisible = !ViewModel.ContentColumnColumnVisible;
                        break;
                    case "Path":
                        ViewModel.ContentPathColumnVisible = !ViewModel.ContentPathColumnVisible;
                        break;
                }
                UpdateContentColumnVisibility();
                UpdateAllContentColumnContextMenus();
            }
        }

        private void FilesContextMenu_Opening(object sender, object e)
        {
            UpdateAllFilesColumnContextMenus();
        }

        private void FilesColumnContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string columnName && ViewModel != null)
            {
                // Toggle visibility
                switch (columnName)
                {
                    case "Size":
                        ViewModel.FilesSizeColumnVisible = !ViewModel.FilesSizeColumnVisible;
                        break;
                    case "Matches":
                        ViewModel.FilesMatchesColumnVisible = !ViewModel.FilesMatchesColumnVisible;
                        break;
                    case "Path":
                        ViewModel.FilesPathColumnVisible = !ViewModel.FilesPathColumnVisible;
                        break;
                    case "Ext":
                        ViewModel.FilesExtColumnVisible = !ViewModel.FilesExtColumnVisible;
                        break;
                    case "Encoding":
                        ViewModel.FilesEncodingColumnVisible = !ViewModel.FilesEncodingColumnVisible;
                        break;
                    case "DateModified":
                        ViewModel.FilesDateModifiedColumnVisible = !ViewModel.FilesDateModifiedColumnVisible;
                        break;
                }
                UpdateFilesColumnVisibility();
                UpdateAllFilesColumnContextMenus();
            }
        }

        private void UpdateAllContentColumnContextMenus()
        {
            if (ViewModel == null)
                return;

            // Update all Content context menu items
            var menuItems = new[]
            {
                ContentHideLineMenuItem, ContentHideLineMenuItem2, ContentHideLineMenuItem3, ContentHideLineMenuItem4, ContentHideLineMenuItem5,
                ContentHideColumnMenuItem, ContentHideColumnMenuItem2, ContentHideColumnMenuItem3, ContentHideColumnMenuItem4, ContentHideColumnMenuItem5,
                ContentHidePathMenuItem, ContentHidePathMenuItem2, ContentHidePathMenuItem3, ContentHidePathMenuItem4, ContentHidePathMenuItem5
            };

            foreach (var item in menuItems)
            {
                if (item != null && item.Tag is string columnName)
                {
                    bool isHidden = columnName switch
                    {
                        "Line" => !ViewModel.ContentLineColumnVisible,
                        "Column" => !ViewModel.ContentColumnColumnVisible,
                        "Path" => !ViewModel.ContentPathColumnVisible,
                        _ => false
                    };
                    
                    string baseText = item.Text.Replace("✓ ", "").Replace("✓", "").Trim();
                    item.Text = isHidden ? $"✓ {baseText}" : baseText;
                }
            }
        }

        private void UpdateAllFilesColumnContextMenus()
        {
            if (ViewModel == null)
                return;

            // Update all Files context menu items
            var menuItems = new[]
            {
                FilesHideSizeMenuItem, FilesHideSizeMenuItem2, FilesHideSizeMenuItem3, FilesHideSizeMenuItem4, FilesHideSizeMenuItem5, FilesHideSizeMenuItem6, FilesHideSizeMenuItem7,
                FilesHideMatchesMenuItem, FilesHideMatchesMenuItem2, FilesHideMatchesMenuItem3, FilesHideMatchesMenuItem4, FilesHideMatchesMenuItem5, FilesHideMatchesMenuItem6, FilesHideMatchesMenuItem7,
                FilesHidePathMenuItem, FilesHidePathMenuItem2, FilesHidePathMenuItem3, FilesHidePathMenuItem4, FilesHidePathMenuItem5, FilesHidePathMenuItem6, FilesHidePathMenuItem7,
                FilesHideExtMenuItem, FilesHideExtMenuItem2, FilesHideExtMenuItem3, FilesHideExtMenuItem4, FilesHideExtMenuItem5, FilesHideExtMenuItem6, FilesHideExtMenuItem7,
                FilesHideEncodingMenuItem, FilesHideEncodingMenuItem2, FilesHideEncodingMenuItem3, FilesHideEncodingMenuItem4, FilesHideEncodingMenuItem5, FilesHideEncodingMenuItem6, FilesHideEncodingMenuItem7,
                FilesHideDateModifiedMenuItem, FilesHideDateModifiedMenuItem2, FilesHideDateModifiedMenuItem3, FilesHideDateModifiedMenuItem4, FilesHideDateModifiedMenuItem5, FilesHideDateModifiedMenuItem6, FilesHideDateModifiedMenuItem7
            };

            foreach (var item in menuItems)
            {
                if (item != null && item.Tag is string columnName)
                {
                    bool isHidden = columnName switch
                    {
                        "Size" => !ViewModel.FilesSizeColumnVisible,
                        "Matches" => !ViewModel.FilesMatchesColumnVisible,
                        "Path" => !ViewModel.FilesPathColumnVisible,
                        "Ext" => !ViewModel.FilesExtColumnVisible,
                        "Encoding" => !ViewModel.FilesEncodingColumnVisible,
                        "DateModified" => !ViewModel.FilesDateModifiedColumnVisible,
                        _ => false
                    };
                    
                    string baseText = item.Text.Replace("✓ ", "").Replace("✓", "").Trim();
                    item.Text = isHidden ? $"✓ {baseText}" : baseText;
                }
            }
        }

        private void UpdateContentColumnVisibility()
        {
            if (ViewModel == null)
                return;

            // Update header column visibility
            LineColumnDefinition.Width = ViewModel.ContentLineColumnVisible 
                ? new GridLength(80) 
                : new GridLength(0);
            ColumnColumnDefinition.Width = ViewModel.ContentColumnColumnVisible 
                ? new GridLength(90) 
                : new GridLength(0);
            PathColumnDefinition.Width = ViewModel.ContentPathColumnVisible 
                ? new GridLength(2, GridUnitType.Star) 
                : new GridLength(0);

            // Update data row column visibility
            if (ResultsListView != null && ResultsListView.Items.Count > 0)
            {
                for (int i = 0; i < ResultsListView.Items.Count; i++)
                {
                    if (ResultsListView.ContainerFromIndex(i) is ListViewItem container &&
                        container.ContentTemplateRoot is Border border &&
                        border.Child is Grid grid &&
                        grid.ColumnDefinitions.Count >= 5)
                    {
                        grid.ColumnDefinitions[1].Width = ViewModel.ContentLineColumnVisible 
                            ? new GridLength(80) 
                            : new GridLength(0);
                        grid.ColumnDefinitions[2].Width = ViewModel.ContentColumnColumnVisible 
                            ? new GridLength(90) 
                            : new GridLength(0);
                        grid.ColumnDefinitions[4].Width = ViewModel.ContentPathColumnVisible 
                            ? new GridLength(2, GridUnitType.Star) 
                            : new GridLength(0);
                    }
                }
            }
        }

        private void UpdateFilesColumnVisibility()
        {
            if (ViewModel == null)
                return;

            // Update header column visibility
            FilesSizeColumnDefinition.Width = ViewModel.FilesSizeColumnVisible 
                ? new GridLength(100) 
                : new GridLength(0);
            FilesMatchesColumnDefinition.Width = ViewModel.FilesMatchesColumnVisible 
                ? new GridLength(80) 
                : new GridLength(0);
            FilesPathColumnDefinition.Width = ViewModel.FilesPathColumnVisible 
                ? new GridLength(2, GridUnitType.Star) 
                : new GridLength(0);
            FilesExtColumnDefinition.Width = ViewModel.FilesExtColumnVisible 
                ? new GridLength(80) 
                : new GridLength(0);
            FilesEncodingColumnDefinition.Width = ViewModel.FilesEncodingColumnVisible 
                ? new GridLength(100) 
                : new GridLength(0);
            FilesDateColumnDefinition.Width = ViewModel.FilesDateModifiedColumnVisible 
                ? new GridLength(150) 
                : new GridLength(0);

            // Update data row column visibility
            if (FilesResultsListView != null && FilesResultsListView.Items.Count > 0)
            {
                for (int i = 0; i < FilesResultsListView.Items.Count; i++)
                {
                    if (FilesResultsListView.ContainerFromIndex(i) is ListViewItem container &&
                        container.ContentTemplateRoot is Border border &&
                        border.Child is Grid grid &&
                        grid.ColumnDefinitions.Count >= 7)
                    {
                        grid.ColumnDefinitions[1].Width = ViewModel.FilesSizeColumnVisible 
                            ? new GridLength(100) 
                            : new GridLength(0);
                        grid.ColumnDefinitions[2].Width = ViewModel.FilesMatchesColumnVisible 
                            ? new GridLength(80) 
                            : new GridLength(0);
                        grid.ColumnDefinitions[3].Width = ViewModel.FilesPathColumnVisible 
                            ? new GridLength(2, GridUnitType.Star) 
                            : new GridLength(0);
                        grid.ColumnDefinitions[4].Width = ViewModel.FilesExtColumnVisible 
                            ? new GridLength(80) 
                            : new GridLength(0);
                        grid.ColumnDefinitions[5].Width = ViewModel.FilesEncodingColumnVisible 
                            ? new GridLength(100) 
                            : new GridLength(0);
                        grid.ColumnDefinitions[6].Width = ViewModel.FilesDateModifiedColumnVisible 
                            ? new GridLength(150) 
                            : new GridLength(0);
                    }
                }
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (ViewModel == null)
                return;

            DispatcherQueue.TryEnqueue(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(ViewModel.StatusText):
                        UpdateStatusInfoBar(ViewModel.StatusText);
                        break;
                    case nameof(ViewModel.IsSearching):
                        // Update the appropriate button based on which operation is in progress
                        if (_isCurrentOperationReplace)
                        {
                            UpdateReplaceButtonLabel(ViewModel.IsSearching);
                        }
                        else
                        {
                            UpdateSearchButtonLabel(ViewModel.IsSearching);
                        }
                    UpdateSearchButtonState();
                        break;
                    case nameof(ViewModel.ContentLineColumnVisible):
                    case nameof(ViewModel.ContentColumnColumnVisible):
                    case nameof(ViewModel.ContentPathColumnVisible):
                        UpdateContentColumnVisibility();
                        UpdateAllContentColumnContextMenus();
                        break;
                    case nameof(ViewModel.FilesSizeColumnVisible):
                    case nameof(ViewModel.FilesMatchesColumnVisible):
                    case nameof(ViewModel.FilesPathColumnVisible):
                    case nameof(ViewModel.FilesExtColumnVisible):
                    case nameof(ViewModel.FilesEncodingColumnVisible):
                    case nameof(ViewModel.FilesDateModifiedColumnVisible):
                        UpdateFilesColumnVisibility();
                        UpdateAllFilesColumnContextMenus();
                        break;
                    case nameof(ViewModel.SearchResults):
                    case nameof(ViewModel.FileSearchResults):
                        // Update header based on current mode (only when results change, not when mode changes)
                        UpdateResultsHeader();
                        
                        // Update header visibility based on whether there are results
                        UpdateResultsHeaderVisibility();
                        
                        // Set the appropriate items source based on search mode
                        if (ViewModel.IsFilesSearch)
                        {
                            if (FilesResultsListView != null)
                            {
                                // Ensure column widths are initialized before setting ItemsSource
                                InitializeFilesColumnWidths();
                                
                                // Force header layout update to ensure widths are calculated
                                if (FilesHeaderGrid != null)
                                {
                                    FilesHeaderGrid.UpdateLayout();
                                }
                                
                                FilesResultsListView.ItemsSource = ViewModel.FileSearchResults;
                                
                                // Update row widths after containers are created
                                UpdateAllFilesRowColumnWidths();
                            }
                        }
                        else
                        {
                            if (ResultsListView != null)
                            {
                                // Ensure column widths are initialized before setting ItemsSource
                                InitializeColumnWidths();
                                
                                // Force header layout update to ensure widths are calculated
                                if (ResultsHeaderGrid != null)
                                {
                                    ResultsHeaderGrid.UpdateLayout();
                                }
                                
                                ResultsListView.ItemsSource = ViewModel.SearchResults;
                                
                                // Calculate and apply dynamic column widths based on content
                                AdjustColumnWidthsForContent();
                                
                                // Update row widths after containers are created
                                UpdateRowWidthsAfterItemsSourceSet();
                            }
                        }
                        break;
                    case nameof(ViewModel.IsFilesSearch):
                        // Update header visibility when search mode changes
                        UpdateResultsHeaderVisibility();
                        // Don't switch tables immediately when mode changes - wait for search
                        // The table will be updated when SearchResults or FileSearchResults change
                        break;
                    case nameof(ViewModel.CanSearch):
                case nameof(ViewModel.CanSearchOrStop):
                case nameof(ViewModel.CanReplace):
                case nameof(ViewModel.CanReplaceOrStop):
                    UpdateSearchButtonState();
                        break;
                    case nameof(ViewModel.IsWindowsSearchOptionEnabled):
                    case nameof(ViewModel.UseWindowsSearchIndex):
                        UpdateWindowsSearchCheckboxState();
                        break;
                }
            });
        }

        private void UpdateStatusInfoBar(string statusText)
        {
            // InfoBar is now in MainWindow, so this method is no longer needed
            // The MainWindow will handle updating the InfoBar based on the selected tab's ViewModel
        }

        private string GetString(string key) =>
            _localizationService.GetLocalizedString(key);

        private string GetString(string key, params object[] args) =>
            _localizationService.GetLocalizedString(key, args);

        /// <summary>
        /// Refreshes all localized UI elements when the application language changes
        /// </summary>
        public void RefreshLocalization()
        {
            try
            {
                var locService = _localizationService;
                
                // Update SearchTypeComboBox items
                if (SearchTypeComboBox != null)
                {
                    foreach (var item in SearchTypeComboBox.Items.OfType<ComboBoxItem>())
                    {
                        if (item.Tag?.ToString() == "Text" && item is ComboBoxItem textItem)
                        {
                            textItem.Content = locService.GetLocalizedString("TextSearchComboBoxItem.Content");
                        }
                        else if (item.Tag?.ToString() == "Regex" && item is ComboBoxItem regexItem)
                        {
                            regexItem.Content = locService.GetLocalizedString("RegexSearchComboBoxItem.Content");
                        }
                    }

                    RefreshComboBoxSelection(SearchTypeComboBox, SearchTypeComboBox_SelectionChanged);
                }
                
                // Update SearchResultsComboBox items
                if (SearchResultsComboBox != null)
                {
                    foreach (var item in SearchResultsComboBox.Items.OfType<ComboBoxItem>())
                    {
                        if (item.Tag?.ToString() == "Content" && item is ComboBoxItem contentItem)
                        {
                            contentItem.Content = locService.GetLocalizedString("ContentComboBoxItem.Content");
                        }
                        else if (item.Tag?.ToString() == "Files" && item is ComboBoxItem filesItem)
                        {
                            filesItem.Content = locService.GetLocalizedString("FilesComboBoxItem.Content");
                        }
                    }

                    RefreshComboBoxSelection(SearchResultsComboBox, SearchResultsComboBox_SelectionChanged);
                }
                
                // Update Filter Options checkboxes - these use x:Uid so we need to manually update them
                if (RespectGitignoreCheckBox != null)
                {
                    RespectGitignoreCheckBox.Content = locService.GetLocalizedString("RespectGitignoreCheckBox.Content");
                }
                if (SearchCaseSensitiveCheckBox != null)
                {
                    SearchCaseSensitiveCheckBox.Content = locService.GetLocalizedString("SearchCaseSensitiveCheckBox.Content");
                }
                if (IncludeSystemFilesCheckBox != null)
                {
                    IncludeSystemFilesCheckBox.Content = locService.GetLocalizedString("IncludeSystemFilesCheckBox.Content");
                }
                if (IncludeSubfoldersCheckBox != null)
                {
                    IncludeSubfoldersCheckBox.Content = locService.GetLocalizedString("IncludeSubfoldersCheckBox.Content");
                }
                if (IncludeHiddenItemsCheckBox != null)
                {
                    IncludeHiddenItemsCheckBox.Content = locService.GetLocalizedString("IncludeHiddenItemsCheckBox.Content");
                }
                if (IncludeBinaryFilesCheckBox != null)
                {
                    IncludeBinaryFilesCheckBox.Content = locService.GetLocalizedString("IncludeBinaryFilesCheckBox.Content");
                }
                if (IncludeSymbolicLinksCheckBox != null)
                {
                    IncludeSymbolicLinksCheckBox.Content = locService.GetLocalizedString("IncludeSymbolicLinksCheckBox.Content");
                }
                if (UseWindowsSearchCheckBox != null)
                {
                    UseWindowsSearchCheckBox.Content = locService.GetLocalizedString("UseWindowsSearchCheckBox.Content");
                }
                
                // Update FilterOptionsToggleButton
                if (FilterOptionsToggleButton != null)
                {
                    FilterOptionsToggleButton.Label = locService.GetLocalizedString("FilterOptionsToggleButton.Label");
                }
                
                // Update AppBarButton labels
                if (AppBarSearchButton != null)
                {
                    AppBarSearchButton.Label = locService.GetLocalizedString("AppBarSearchButton.Label");
                }
                if (AppBarReplaceButton != null)
                {
                    AppBarReplaceButton.Label = locService.GetLocalizedString("AppBarReplaceButton.Label");
                }
                if (AppBarResetButton != null)
                {
                    AppBarResetButton.Label = locService.GetLocalizedString("AppBarResetButton.Label");
                }
                
                // Update ReplaceCheckBox
                if (ReplaceCheckBox != null)
                {
                    ReplaceCheckBox.Content = locService.GetLocalizedString("ReplaceCheckBox.Content");
                }
                
                // Update TextBlock labels
                if (MatchFilesTextBlock != null)
                {
                    MatchFilesTextBlock.Text = locService.GetLocalizedString("MatchFilesTextBlock.Text");
                }
                if (ExcludeDirsTextBlock != null)
                {
                    ExcludeDirsTextBlock.Text = locService.GetLocalizedString("ExcludeDirsTextBlock.Text");
                }
                if (SearchTypeTextBlock != null)
                {
                    SearchTypeTextBlock.Text = locService.GetLocalizedString("SearchTypeTextBlock.Text");
                }
                if (SearchResultsTextBlock != null)
                {
                    SearchResultsTextBlock.Text = locService.GetLocalizedString("SearchResultsTextBlock.Text");
                }
                if (SizeLimitTextBlock != null)
                {
                    SizeLimitTextBlock.Text = locService.GetLocalizedString("SizeLimitTextBlock.Text");
                }
                
                // Update placeholder texts
                if (PathAutoSuggestBox != null)
                {
                    PathAutoSuggestBox.PlaceholderText = locService.GetLocalizedString("PathAutoSuggestBox.PlaceholderText");
                }
                if (SearchTextBox != null)
                {
                    SearchTextBox.PlaceholderText = locService.GetLocalizedString("SearchTextBox.PlaceholderText");
                }
                if (ReplaceWithTextBox != null)
                {
                    ReplaceWithTextBox.PlaceholderText = locService.GetLocalizedString("ReplaceWithTextBox.PlaceholderText");
                }
                
                // Update SizeLimitComboBox items
                if (SizeLimitComboBox != null)
                {
                    foreach (var item in SizeLimitComboBox.Items.OfType<ComboBoxItem>())
                    {
                        var tag = item.Tag?.ToString();
                        if (tag == "NoLimit")
                        {
                            item.Content = locService.GetLocalizedString("NoLimitComboBoxItem.Content");
                        }
                        else if (tag == "LessThan")
                        {
                            item.Content = locService.GetLocalizedString("LessThanComboBoxItem.Content");
                        }
                        else if (tag == "EqualTo")
                        {
                            item.Content = locService.GetLocalizedString("EqualToComboBoxItem.Content");
                        }
                        else if (tag == "GreaterThan")
                        {
                            item.Content = locService.GetLocalizedString("GreaterThanComboBoxItem.Content");
                        }
                    }

                    RefreshComboBoxSelection(SizeLimitComboBox, SizeLimitComboBox_SelectionChanged);
                }
                
                // Update SizeUnitComboBox items
                if (SizeUnitComboBox != null)
                {
                    foreach (var item in SizeUnitComboBox.Items.OfType<ComboBoxItem>())
                    {
                        var tag = item.Tag?.ToString();
                        if (tag == "KB")
                        {
                            item.Content = locService.GetLocalizedString("KBComboBoxItem.Content");
                        }
                        else if (tag == "MB")
                        {
                            item.Content = locService.GetLocalizedString("MBComboBoxItem.Content");
                        }
                        else if (tag == "GB")
                        {
                            item.Content = locService.GetLocalizedString("GBComboBoxItem.Content");
                        }
                    }
                }
                
                // Update Content Results column headers
                if (ContentNameHeaderButton != null)
                {
                    ContentNameHeaderButton.Content = locService.GetLocalizedString("ContentNameHeaderButton.Content");
                }
                if (ContentLineHeaderButton != null)
                {
                    ContentLineHeaderButton.Content = locService.GetLocalizedString("ContentLineHeaderButton.Content");
                }
                if (ContentColumnHeaderButton != null)
                {
                    ContentColumnHeaderButton.Content = locService.GetLocalizedString("ContentColumnHeaderButton.Content");
                }
                if (ContentTextHeaderButton != null)
                {
                    ContentTextHeaderButton.Content = locService.GetLocalizedString("ContentTextHeaderButton.Content");
                }
                if (ContentPathHeaderButton != null)
                {
                    ContentPathHeaderButton.Content = locService.GetLocalizedString("ContentPathHeaderButton.Content");
                }
                
                // Update Files Results column headers
                if (FilesNameHeaderButton != null)
                {
                    FilesNameHeaderButton.Content = locService.GetLocalizedString("FilesNameHeaderButton.Content");
                }
                if (FilesSizeHeaderButton != null)
                {
                    FilesSizeHeaderButton.Content = locService.GetLocalizedString("FilesSizeHeaderButton.Content");
                }
                if (FilesMatchesHeaderButton != null)
                {
                    FilesMatchesHeaderButton.Content = locService.GetLocalizedString("FilesMatchesHeaderButton.Content");
                }
                if (FilesPathHeaderButton != null)
                {
                    FilesPathHeaderButton.Content = locService.GetLocalizedString("FilesPathHeaderButton.Content");
                }
                if (FilesExtHeaderButton != null)
                {
                    FilesExtHeaderButton.Content = locService.GetLocalizedString("FilesExtHeaderButton.Content");
                }
                if (FilesEncodingHeaderButton != null)
                {
                    FilesEncodingHeaderButton.Content = locService.GetLocalizedString("FilesEncodingHeaderButton.Content");
                }
                if (FilesDateModifiedHeaderButton != null)
                {
                    FilesDateModifiedHeaderButton.Content = locService.GetLocalizedString("FilesDateModifiedHeaderButton.Content");
                }
                
                // Force layout update
                this.InvalidateArrange();
                this.InvalidateMeasure();
                this.UpdateLayout();
            }
            catch (Exception ex)
            {
                Log($"RefreshLocalization error: {ex.Message}");
            }
        }

    }
}

