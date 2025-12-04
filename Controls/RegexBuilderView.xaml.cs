using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Text;
using Windows.UI;
using Grex.Services;

namespace Grex.Controls
{
    public sealed partial class RegexBuilderView : UserControl
    {
        private Regex? _currentRegex;
        private bool _isUpdating = false;
        private readonly ILocalizationService _localizationService = LocalizationService.Instance;
        private bool _areToolTipsRegistered;

        public RegexBuilderView()
        {
            this.InitializeComponent();
            RegisterLocalizedToolTips();
            this.Loaded += RegexBuilderView_Loaded;
            this.Unloaded += RegexBuilderView_Unloaded;
        }
        
        private void RegexBuilderView_Loaded(object sender, RoutedEventArgs e)
        {
            // Subscribe to theme changes and apply initial theme
            MainWindow.ThemeChanged += OnThemeChanged;
            
            // Delay theme application to ensure visual tree is fully populated
            DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                ApplyCurrentThemeColors();
            });
        }
        
        private void RegexBuilderView_Unloaded(object sender, RoutedEventArgs e)
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
                
                // Clear first to ensure clean state when switching between high-contrast themes
                this.Resources?.Clear();
                
                // Apply text color to all TextBlocks
                ApplyForegroundToAllTextBlocks(this, e.TextBrush, e.AccentBrush, e.TertiaryBrush);
                
                // Apply background
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
                this.Resources["CheckBoxCheckBackgroundFillChecked"] = e.TertiaryBrush;
                this.Resources["CheckBoxCheckBackgroundFillCheckedPointerOver"] = e.AccentBrush;
                
                // TextBox resources
                this.Resources["TextBoxForeground"] = e.TextBrush;
                this.Resources["TextControlForeground"] = e.TextBrush;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyThemeColors ERROR: {ex}");
            }
        }
        
        private void ApplyForegroundToAllTextBlocks(DependencyObject parent, SolidColorBrush foreground, SolidColorBrush accent, SolidColorBrush tertiary)
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
                    checkBox.Resources["CheckBoxCheckBackgroundFillChecked"] = tertiary;
                    checkBox.Resources["CheckBoxCheckBackgroundFillCheckedPointerOver"] = accent;
                    checkBox.Resources["CheckBoxCheckBackgroundFillCheckedPressed"] = accent;
                    
                    // Force visual state refresh to apply new resources
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
                ApplyForegroundToAllTextBlocks(child, foreground, accent, tertiary);
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

        private void RegisterLocalizedToolTips()
        {
            if (_areToolTipsRegistered)
            {
                return;
            }

            _areToolTipsRegistered = true;

            LocalizedToolTipRegistry.Register(SampleTextTextBox, "Controls.RegexBuilderView.SampleTextTextBox.ToolTip");
            LocalizedToolTipRegistry.Register(RegexPatternTextBox, "Controls.RegexBuilderView.RegexPatternTextBox.ToolTip");
            LocalizedToolTipRegistry.Register(CaseInsensitiveCheckBox, "Controls.RegexBuilderView.CaseInsensitiveCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(MultilineCheckBox, "Controls.RegexBuilderView.MultilineCheckBox.ToolTip");
            LocalizedToolTipRegistry.Register(GlobalMatchCheckBox, "Controls.RegexBuilderView.GlobalMatchCheckBox.ToolTip");
        }

        private void SampleTextTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUpdating)
            {
                UpdateMatchResults();
            }
        }

        private void RegexPatternTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUpdating)
            {
                UpdateRegex();
                UpdateMatchResults();
                UpdateBreakdown();
            }
        }

        private void OptionsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isUpdating)
            {
                UpdateRegex();
                UpdateMatchResults();
            }
        }

        private void UpdateRegex()
        {
            try
            {
                var pattern = RegexPatternTextBox.Text;
                if (string.IsNullOrEmpty(pattern))
                {
                    _currentRegex = null;
                    return;
                }

                var options = RegexOptions.None;
                if (CaseInsensitiveCheckBox.IsChecked == true)
                {
                    options |= RegexOptions.IgnoreCase;
                }
                if (MultilineCheckBox.IsChecked == true)
                {
                    options |= RegexOptions.Multiline;
                }

                _currentRegex = new Regex(pattern, options);
            }
            catch (ArgumentException)
            {
                // Invalid regex pattern
                _currentRegex = null;
            }
        }

        private void UpdateMatchResults()
        {
            MatchResultsTextBlock.Inlines.Clear();

            if (_currentRegex == null || string.IsNullOrEmpty(RegexPatternTextBox.Text))
            {
                var message = _localizationService.GetLocalizedString("EnterValidPatternMessage");
                MatchResultsTextBlock.Inlines.Add(new Run { Text = message });
                return;
            }

            var sampleText = SampleTextTextBox.Text;
            if (string.IsNullOrEmpty(sampleText))
            {
                var message = _localizationService.GetLocalizedString("EnterSampleTextMessage");
                MatchResultsTextBlock.Inlines.Add(new Run { Text = message });
                return;
            }

            // Get the blue color used for regex (same as breakdown)
            Brush matchHighlightColor;
            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var accentColorObj) && accentColorObj is Windows.UI.Color accentColor)
            {
                matchHighlightColor = new SolidColorBrush(accentColor);
            }
            else
            {
                // Fallback to a medium blue that has good contrast in both themes
                matchHighlightColor = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215));
            }

            try
            {
                if (GlobalMatchCheckBox.IsChecked == true)
                {
                    // Global match - find all matches
                    var matches = _currentRegex.Matches(sampleText);
                    if (matches.Count == 0)
                    {
                        MatchResultsTextBlock.Inlines.Add(new Run
                        {
                            Text = GetString("RegexBreakdownNoMatchesFound"),
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange)
                        });
                    }
                    else
                    {
                        int lastIndex = 0;
                        foreach (Match match in matches)
                        {
                            // Add text before match
                            if (match.Index > lastIndex)
                            {
                                MatchResultsTextBlock.Inlines.Add(new Run 
                                { 
                                    Text = sampleText.Substring(lastIndex, match.Index - lastIndex) 
                                });
                            }

                            // Add highlighted match - use blue color like regex breakdown
                            var highlightedRun = new Run 
                            { 
                                Text = match.Value,
                                Foreground = matchHighlightColor,
                                FontWeight = Microsoft.UI.Text.FontWeights.Bold
                            };
                            // Note: Run doesn't support Background, so we'll use a different approach
                            MatchResultsTextBlock.Inlines.Add(highlightedRun);

                            lastIndex = match.Index + match.Length;
                        }

                        // Add remaining text
                        if (lastIndex < sampleText.Length)
                        {
                            MatchResultsTextBlock.Inlines.Add(new Run 
                            { 
                                Text = sampleText.Substring(lastIndex) 
                            });
                        }

                        // Add match count
                        MatchResultsTextBlock.Inlines.Add(new LineBreak());
                        MatchResultsTextBlock.Inlines.Add(new Run
                        {
                            Text = GetString("RegexBreakdownFoundMatches", matches.Count),
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green),
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                        });
                    }
                }
                else
                {
                    // Single match - find first match only
                    var match = _currentRegex.Match(sampleText);
                    if (!match.Success)
                    {
                        MatchResultsTextBlock.Inlines.Add(new Run
                        {
                            Text = GetString("RegexBreakdownNoMatchFound"),
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange)
                        });
                    }
                    else
                    {
                        // Add text before match
                        if (match.Index > 0)
                        {
                            MatchResultsTextBlock.Inlines.Add(new Run 
                            { 
                                Text = sampleText.Substring(0, match.Index) 
                            });
                        }

                        // Add highlighted match - use blue color like regex breakdown
                        var highlightedRun = new Run 
                        { 
                            Text = match.Value,
                            Foreground = matchHighlightColor,
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold
                        };
                        // Note: Run doesn't support Background, so we'll use a different approach
                        MatchResultsTextBlock.Inlines.Add(highlightedRun);

                        // Add text after match
                        if (match.Index + match.Length < sampleText.Length)
                        {
                            MatchResultsTextBlock.Inlines.Add(new Run 
                            { 
                                Text = sampleText.Substring(match.Index + match.Length) 
                            });
                        }

                        MatchResultsTextBlock.Inlines.Add(new LineBreak());
                        MatchResultsTextBlock.Inlines.Add(new Run
                        {
                            Text = GetString("RegexBreakdownFoundOneMatch"),
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green),
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MatchResultsTextBlock.Inlines.Add(new Run
                {
                    Text = GetString("RegexBreakdownErrorMessage", ex.Message),
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red)
                });
            }
        }

        private void UpdateBreakdown()
        {
            BreakdownStackPanel.Children.Clear();

            var pattern = RegexPatternTextBox.Text;
            if (string.IsNullOrEmpty(pattern))
            {
                var textBlock = new TextBlock
                {
                    Text = GetString("RegexBreakdownEnterPatternMessage"),
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
                BreakdownStackPanel.Children.Add(textBlock);
                return;
            }

            try
            {
                // Validate pattern first
                var testRegex = new Regex(pattern);
                
                // Parse and display breakdown
                var breakdown = ParseRegexBreakdown(pattern);
                
                foreach (var item in breakdown)
                {
                    var stackPanel = new StackPanel 
                    { 
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Margin = new Thickness(0, 4, 0, 4)
                    };

                    // Use theme-aware color that works in both light and dark themes
                    // Try to get system accent color, fallback to a medium blue
                    Brush typeForeground;
                    if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var accentColorObj) && accentColorObj is Color accentColor)
                    {
                        typeForeground = new SolidColorBrush(accentColor);
                    }
                    else
                    {
                        // Fallback to a medium blue that has good contrast in both themes
                        typeForeground = new SolidColorBrush(Color.FromArgb(255, 0, 120, 215));
                    }

                    var typeBlock = new TextBlock
                    {
                        Text = $"[{item.Type}]",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = typeForeground,
                        MinWidth = 120
                    };

                    var contentBlock = new TextBlock
                    {
                        Text = item.Content,
                        FontFamily = new FontFamily("Consolas"),
                        TextWrapping = TextWrapping.Wrap
                    };

                    if (!string.IsNullOrEmpty(item.Description))
                    {
                        var descBlock = new TextBlock
                        {
                            Text = $" - {item.Description}",
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray)
                        };
                        // Note: FontStyle.Italic is not available in Microsoft.UI.Text, using Foreground color instead
                        stackPanel.Children.Add(typeBlock);
                        stackPanel.Children.Add(contentBlock);
                        stackPanel.Children.Add(descBlock);
                    }
                    else
                    {
                        stackPanel.Children.Add(typeBlock);
                        stackPanel.Children.Add(contentBlock);
                    }

                    BreakdownStackPanel.Children.Add(stackPanel);
                }
            }
            catch (ArgumentException ex)
            {
                var errorBlock = new TextBlock
                {
                    Text = GetString("RegexBreakdownInvalidPatternMessage", ex.Message),
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                    TextWrapping = TextWrapping.Wrap
                };
                BreakdownStackPanel.Children.Add(errorBlock);
            }
        }

        private List<BreakdownItem> ParseRegexBreakdown(string pattern)
        {
            var items = new List<BreakdownItem>();
            var i = 0;

            while (i < pattern.Length)
            {
                var ch = pattern[i];

                // Character classes
                if (ch == '[')
                {
                    var endIndex = pattern.IndexOf(']', i);
                    if (endIndex > i)
                    {
                        var content = pattern.Substring(i, endIndex - i + 1);
                        items.Add(new BreakdownItem
                        {
                            Type = GetString("RegexBreakdownTypeCharacterClass"),
                            Content = content,
                            Description = GetString("RegexBreakdownDescCharacterClass")
                        });
                        i = endIndex + 1;
                        continue;
                    }
                }

                // Groups
                if (ch == '(')
                {
                    var endIndex = FindMatchingParen(pattern, i);
                    if (endIndex > i)
                    {
                        var content = pattern.Substring(i, endIndex - i + 1);
                        var isNonCapturing = i + 1 < pattern.Length && pattern[i + 1] == '?';
                        items.Add(new BreakdownItem
                        {
                            Type = isNonCapturing ? GetString("RegexBreakdownTypeNonCapturingGroup") : GetString("RegexBreakdownTypeCapturingGroup"),
                            Content = content,
                            Description = isNonCapturing ? GetString("RegexBreakdownDescNonCapturingGroup") : GetString("RegexBreakdownDescCapturingGroup")
                        });
                        i = endIndex + 1;
                        continue;
                    }
                }

                // Quantifiers
                if (ch == '*' || ch == '+' || ch == '?' || ch == '{')
                {
                    string content;
                    string description;
                    
                    if (ch == '{')
                    {
                        var endIndex = pattern.IndexOf('}', i);
                        if (endIndex > i)
                        {
                            content = pattern.Substring(i, endIndex - i + 1);
                            description = GetString("RegexBreakdownDescQuantifierRange");
                        }
                        else
                        {
                            content = ch.ToString();
                            description = GetString("RegexBreakdownTypeQuantifier");
                            endIndex = i;
                        }
                        i = endIndex + 1;
                    }
                    else
                    {
                        content = ch.ToString();
                        description = ch switch
                        {
                            '*' => GetString("RegexBreakdownDescZeroOrMore"),
                            '+' => GetString("RegexBreakdownDescOneOrMore"),
                            '?' => GetString("RegexBreakdownDescZeroOrOne"),
                            _ => GetString("RegexBreakdownTypeQuantifier")
                        };
                        i++;
                    }
                    
                    items.Add(new BreakdownItem
                    {
                        Type = GetString("RegexBreakdownTypeQuantifier"),
                        Content = content,
                        Description = description
                    });
                    continue;
                }

                // Anchors
                if (ch == '^' || ch == '$')
                {
                    items.Add(new BreakdownItem
                    {
                        Type = GetString("RegexBreakdownTypeAnchor"),
                        Content = ch.ToString(),
                        Description = ch == '^' ? GetString("RegexBreakdownDescAnchorStart") : GetString("RegexBreakdownDescAnchorEnd")
                    });
                    i++;
                    continue;
                }

                // Escape sequences
                if (ch == '\\' && i + 1 < pattern.Length)
                {
                    var next = pattern[i + 1];
                    var content = pattern.Substring(i, 2);
                    string description = next switch
                    {
                        'd' => GetString("RegexBreakdownDescDigit"),
                        'D' => GetString("RegexBreakdownDescNonDigit"),
                        'w' => GetString("RegexBreakdownDescWordChar"),
                        'W' => GetString("RegexBreakdownDescNonWordChar"),
                        's' => GetString("RegexBreakdownDescWhitespace"),
                        'S' => GetString("RegexBreakdownDescNonWhitespace"),
                        'n' => GetString("RegexBreakdownDescNewline"),
                        't' => GetString("RegexBreakdownDescTab"),
                        'r' => GetString("RegexBreakdownDescCarriageReturn"),
                        _ => GetString("RegexBreakdownTypeEscapeSequence")
                    };
                    items.Add(new BreakdownItem
                    {
                        Type = GetString("RegexBreakdownTypeEscapeSequence"),
                        Content = content,
                        Description = description
                    });
                    i += 2;
                    continue;
                }

                // Literal character
                items.Add(new BreakdownItem
                {
                    Type = GetString("RegexBreakdownTypeLiteral"),
                    Content = ch.ToString(),
                    Description = GetString("RegexBreakdownDescLiteralChar")
                });
                i++;
            }

            return items;
        }

        private int FindMatchingParen(string pattern, int startIndex)
        {
            int depth = 1;
            int i = startIndex + 1;
            while (i < pattern.Length && depth > 0)
            {
                if (pattern[i] == '(')
                    depth++;
                else if (pattern[i] == ')')
                    depth--;
                i++;
            }
            return depth == 0 ? i - 1 : -1;
        }

        private async void EmailPresetButton_Click(object sender, RoutedEventArgs e)
        {
            await ApplyPreset("Email", @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
        }

        private async void PhonePresetButton_Click(object sender, RoutedEventArgs e)
        {
            await ApplyPreset("Phone", @"^(\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}$");
        }

        private async void DatePresetButton_Click(object sender, RoutedEventArgs e)
        {
            await ApplyPreset("Date", @"^\d{4}-\d{2}-\d{2}$");
        }

        private async void DigitsPresetButton_Click(object sender, RoutedEventArgs e)
        {
            await ApplyPreset("Digits", @"^\d+$");
        }

        private async void UrlPresetButton_Click(object sender, RoutedEventArgs e)
        {
            await ApplyPreset("URL", @"^https?://[^\s/$.?#].[^\s]*$");
        }

        private async System.Threading.Tasks.Task ApplyPreset(string presetName, string regexPattern)
        {
            // If the pattern box is empty, apply the preset directly without confirmation
            if (string.IsNullOrWhiteSpace(RegexPatternTextBox.Text))
            {
                _isUpdating = true;
                RegexPatternTextBox.Text = regexPattern;
                _isUpdating = false;
                UpdateRegex();
                UpdateMatchResults();
                UpdateBreakdown();
                return;
            }

            // If the pattern box has content, show confirmation dialog
            var dialog = new ContentDialog
            {
                Title = GetString("RegexBreakdownOverwritePatternTitle"),
                Content = GetString("RegexBreakdownOverwritePatternMessage", presetName),
                PrimaryButtonText = GetString("ProceedButton"),
                SecondaryButtonText = GetString("CancelButton"),
                DefaultButton = ContentDialogButton.Secondary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _isUpdating = true;
                RegexPatternTextBox.Text = regexPattern;
                _isUpdating = false;
                UpdateRegex();
                UpdateMatchResults();
                UpdateBreakdown();
            }
        }

        private void Button_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Set cursor to hand using reflection to access ProtectedCursor
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
                    // If reflection fails, set on this control
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

        private string GetString(string key) =>
            _localizationService.GetLocalizedString(key);

        private string GetString(string key, params object[] args) =>
            _localizationService.GetLocalizedString(key, args);

        /// <summary>
        /// Refreshes all localized UI elements when the application language changes
        /// </summary>
        public void RefreshLocalization()
        {
            // Prevent concurrent refreshes
            if (_isUpdating)
                return;

            try
            {
                _isUpdating = true;
                var locService = _localizationService;
                
                // Update TextBlocks with null checks and error handling
                try
                {
                    if (SampleTextTextBlock != null)
                    {
                        var localizedText = locService.GetLocalizedString("SampleTextTextBlock.Text");
                        SampleTextTextBlock.Text = !string.IsNullOrEmpty(localizedText) && localizedText != "SampleTextTextBlock.Text"
                            ? localizedText
                            : "Sample Text";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating SampleTextTextBlock: {ex.Message}");
                }

                try
                {
                    if (RegexPatternTextBlock != null)
                    {
                        var localizedText = locService.GetLocalizedString("RegexPatternTextBlock.Text");
                        RegexPatternTextBlock.Text = !string.IsNullOrEmpty(localizedText) && localizedText != "RegexPatternTextBlock.Text"
                            ? localizedText
                            : "Regex Pattern";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating RegexPatternTextBlock: {ex.Message}");
                }

                try
                {
                    if (LiveMatchResultsTextBlock != null)
                    {
                        var localizedText = locService.GetLocalizedString("LiveMatchResultsTextBlock.Text");
                        LiveMatchResultsTextBlock.Text = !string.IsNullOrEmpty(localizedText) && localizedText != "LiveMatchResultsTextBlock.Text"
                            ? localizedText
                            : "Live Match Results";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating LiveMatchResultsTextBlock: {ex.Message}");
                }

                try
                {
                    if (VisualRegexBreakdownTextBlock != null)
                    {
                        var localizedText = locService.GetLocalizedString("VisualRegexBreakdownTextBlock.Text");
                        VisualRegexBreakdownTextBlock.Text = !string.IsNullOrEmpty(localizedText) && localizedText != "VisualRegexBreakdownTextBlock.Text"
                            ? localizedText
                            : "Visual Regex Breakdown";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating VisualRegexBreakdownTextBlock: {ex.Message}");
                }

                try
                {
                    if (PresetsTextBlock != null)
                    {
                        var localizedText = locService.GetLocalizedString("PresetsTextBlock.Text");
                        PresetsTextBlock.Text = !string.IsNullOrEmpty(localizedText) && localizedText != "PresetsTextBlock.Text"
                            ? localizedText
                            : "Presets:";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating PresetsTextBlock: {ex.Message}");
                }

                try
                {
                    if (OptionsTextBlock != null)
                    {
                        var localizedText = locService.GetLocalizedString("OptionsTextBlock.Text");
                        OptionsTextBlock.Text = !string.IsNullOrEmpty(localizedText) && localizedText != "OptionsTextBlock.Text"
                            ? localizedText
                            : "Options";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating OptionsTextBlock: {ex.Message}");
                }
                
                // Update TextBox placeholders with error handling
                try
                {
                    if (SampleTextTextBox != null)
                    {
                        var localizedText = locService.GetLocalizedString("SampleTextTextBox.PlaceholderText");
                        SampleTextTextBox.PlaceholderText = !string.IsNullOrEmpty(localizedText) && localizedText != "SampleTextTextBox.PlaceholderText"
                            ? localizedText
                            : "Enter sample text to test your regex pattern against...";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating SampleTextTextBox placeholder: {ex.Message}");
                }

                try
                {
                    if (RegexPatternTextBox != null)
                    {
                        var localizedText = locService.GetLocalizedString("RegexPatternTextBox.PlaceholderText");
                        RegexPatternTextBox.PlaceholderText = !string.IsNullOrEmpty(localizedText) && localizedText != "RegexPatternTextBox.PlaceholderText"
                            ? localizedText
                            : "Enter regex pattern...";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating RegexPatternTextBox placeholder: {ex.Message}");
                }
                
                // Update preset buttons with error handling
                try
                {
                    if (EmailPresetButton != null)
                    {
                        var localizedText = locService.GetLocalizedString("EmailPresetButton.Content");
                        EmailPresetButton.Content = !string.IsNullOrEmpty(localizedText) && localizedText != "EmailPresetButton.Content"
                            ? localizedText
                            : "Email";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating EmailPresetButton: {ex.Message}");
                }

                try
                {
                    if (PhonePresetButton != null)
                    {
                        var localizedText = locService.GetLocalizedString("PhonePresetButton.Content");
                        PhonePresetButton.Content = !string.IsNullOrEmpty(localizedText) && localizedText != "PhonePresetButton.Content"
                            ? localizedText
                            : "Phone";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating PhonePresetButton: {ex.Message}");
                }

                try
                {
                    if (DatePresetButton != null)
                    {
                        var localizedText = locService.GetLocalizedString("DatePresetButton.Content");
                        DatePresetButton.Content = !string.IsNullOrEmpty(localizedText) && localizedText != "DatePresetButton.Content"
                            ? localizedText
                            : "Date";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating DatePresetButton: {ex.Message}");
                }

                try
                {
                    if (DigitsPresetButton != null)
                    {
                        var localizedText = locService.GetLocalizedString("DigitsPresetButton.Content");
                        DigitsPresetButton.Content = !string.IsNullOrEmpty(localizedText) && localizedText != "DigitsPresetButton.Content"
                            ? localizedText
                            : "Digits";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating DigitsPresetButton: {ex.Message}");
                }

                try
                {
                    if (URLPresetButton != null)
                    {
                        var localizedText = locService.GetLocalizedString("URLPresetButton.Content");
                        URLPresetButton.Content = !string.IsNullOrEmpty(localizedText) && localizedText != "URLPresetButton.Content"
                            ? localizedText
                            : "URL";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating URLPresetButton: {ex.Message}");
                }
                
                // Update checkboxes with error handling
                try
                {
                    if (CaseInsensitiveCheckBox != null)
                    {
                        var localizedText = locService.GetLocalizedString("CaseInsensitiveCheckBox.Content");
                        CaseInsensitiveCheckBox.Content = !string.IsNullOrEmpty(localizedText) && localizedText != "CaseInsensitiveCheckBox.Content"
                            ? localizedText
                            : "Case insensitive";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating CaseInsensitiveCheckBox: {ex.Message}");
                }

                try
                {
                    if (MultilineCheckBox != null)
                    {
                        var localizedText = locService.GetLocalizedString("MultilineCheckBox.Content");
                        MultilineCheckBox.Content = !string.IsNullOrEmpty(localizedText) && localizedText != "MultilineCheckBox.Content"
                            ? localizedText
                            : "Multiline";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating MultilineCheckBox: {ex.Message}");
                }

                try
                {
                    if (GlobalMatchCheckBox != null)
                    {
                        var localizedText = locService.GetLocalizedString("GlobalMatchCheckBox.Content");
                        GlobalMatchCheckBox.Content = !string.IsNullOrEmpty(localizedText) && localizedText != "GlobalMatchCheckBox.Content"
                            ? localizedText
                            : "Global match";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating GlobalMatchCheckBox: {ex.Message}");
                }
                
                // Refresh dynamic content (match results and breakdown) when language changes
                // This ensures all text in the Visual Regex Breakdown updates properly
                try
                {
                    UpdateMatchResults();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating match results during localization: {ex.Message}");
                }

                try
                {
                    UpdateBreakdown();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating breakdown during localization: {ex.Message}");
                }
                
                // Force layout update
                this.InvalidateArrange();
                this.InvalidateMeasure();
                this.UpdateLayout();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RegexBuilderView.RefreshLocalization error: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private class BreakdownItem
        {
            public string Type { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }
    }
}

