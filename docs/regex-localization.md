# Regex Localization

## Overview

This document provides comprehensive documentation for the Visual Regex Breakdown localization implementation in Grex. The implementation enables full internationalization of the Visual Regex Breakdown feature, supporting dynamic language switching and providing localized strings for all UI elements, messages, and Regex component descriptions.

## Translation Keys Added

The following localization keys were added to support the Visual Regex Breakdown feature:

### Messages
- `EnterValidPatternMessage` - "Enter a valid Regex pattern to see matches."
- `EnterSampleTextMessage` - "Enter sample text to test your Regex pattern."
- `RegexBreakdownNoMatchesFound` - "No matches found."
- `RegexBreakdownFoundMatches` - "Found {0} match(es)."
- `RegexBreakdownNoMatchFound` - "No match found."
- `RegexBreakdownFoundOneMatch` - "Found 1 match."
- `RegexBreakdownErrorMessage` - "Error: {0}"
- `RegexBreakdownEnterPatternMessage` - "Enter a Regex pattern to see the breakdown."
- `RegexBreakdownInvalidPatternMessage` - "Invalid Regex pattern: {0}"

### Regex Breakdown Types
- `RegexBreakdownTypeCharacterClass` - "Character Class"
- `RegexBreakdownTypeNonCapturingGroup` - "Non-Capturing Group"
- `RegexBreakdownTypeCapturingGroup` - "Capturing Group"
- `RegexBreakdownTypeQuantifier` - "Quantifier"
- `RegexBreakdownTypeAnchor` - "Anchor"
- `RegexBreakdownTypeEscapeSequence` - "Escape Sequence"
- `RegexBreakdownTypeLiteral` - "Literal"

### Regex Breakdown Descriptions
- `RegexBreakdownDescCharacterClass` - "Matches any character in the set"
- `RegexBreakdownDescNonCapturingGroup` - "Groups without capturing"
- `RegexBreakdownDescCapturingGroup` - "Captures matched text"
- `RegexBreakdownDescQuantifierRange` - "Quantifier: specifies exact count or range"
- `RegexBreakdownDescZeroOrMore` - "Zero or more"
- `RegexBreakdownDescOneOrMore` - "One or more"
- `RegexBreakdownDescZeroOrOne` - "Zero or one"
- `RegexBreakdownDescAnchorStart` - "Start of line/string"
- `RegexBreakdownDescAnchorEnd` - "End of line/string"
- `RegexBreakdownDescDigit` - "Digit (0-9)"
- `RegexBreakdownDescNonDigit` - "Non-digit"
- `RegexBreakdownDescWordChar` - "Word character (a-z, A-Z, 0-9, _)"
- `RegexBreakdownDescNonWordChar` - "Non-word character"
- `RegexBreakdownDescWhitespace` - "Whitespace"
- `RegexBreakdownDescNonWhitespace` - "Non-whitespace"
- `RegexBreakdownDescNewline` - "Newline"
- `RegexBreakdownDescTab` - "Tab"
- `RegexBreakdownDescCarriageReturn` - "Carriage return"
- `RegexBreakdownDescLiteralChar` - "Literal character"

### Dialog Messages
- `RegexBreakdownOverwritePatternTitle` - "Overwrite Regex Pattern?"
- `RegexBreakdownOverwritePatternMessage` - "Do you want to overwrite the current Regex Pattern with the {0} preset?"
- `ProceedButton` - "Proceed"
- `CancelButton` - "Cancel"

### UI Elements
- `SampleTextTextBlock.Text` - "Sample Text"
- `RegexPatternTextBlock.Text` - "Regex Pattern"
- `LiveMatchResultsTextBlock.Text` - "Live Match Results"
- `VisualRegexBreakdownTextBlock.Text` - "Visual Regex Breakdown"
- `PresetsTextBlock.Text` - "Presets:"
- `OptionsTextBlock.Text` - "Options"

### Placeholders
- `SampleTextTextBox.PlaceholderText` - "Enter sample text to test your Regex pattern against..."
- `RegexPatternTextBox.PlaceholderText` - "Enter Regex pattern..."

### Preset Buttons
- `EmailPresetButton.Content` - "Email"
- `PhonePresetButton.Content` - "Phone"
- `DatePresetButton.Content` - "Date"
- `DigitsPresetButton.Content` - "Digits"
- `URLPresetButton.Content` - "URL"

### Checkboxes
- `CaseInsensitiveCheckBox.Content` - "Case insensitive"
- `MultilineCheckBox.Content` - "Multiline"
- `GlobalMatchCheckBox.Content` - "Global match"

### Tooltips
- `Controls.RegexBuilderView.SampleTextTextBox.ToolTip` - "Provide sample text that Regex will be tested against."
- `Controls.RegexBuilderView.RegexPatternTextBox.ToolTip` - "Enter Regex pattern you want to evaluate."
- `Controls.RegexBuilderView.CaseInsensitiveCheckBox.ToolTip` - "Ignore character casing when evaluating matches."
- `Controls.RegexBuilderView.MultilineCheckBox.ToolTip` - "Treat ^ and $ as the start and end of each line instead of the whole text."
- `Controls.RegexBuilderView.GlobalMatchCheckBox.ToolTip` - "Find every match instead of stopping after the first one."

## How Localization Works for Visual Regex Breakdown

The Visual Regex Breakdown localization implementation follows Grex's established localization patterns:

### 1. Resource File Structure
- All localization strings are stored in `.resw` files in the `Strings/` directory
- Each supported language has its own subdirectory:
  - `Strings/en-US/Resources.resw` (English - Default)
  - `Strings/de-DE/Resources.resw` (German)
  - `Strings/es-ES/Resources.resw` (Spanish)
  - `Strings/fr-FR/Resources.resw` (French)

### 2. Localization Service Integration
- The [`RegexBuilderView.xaml.cs`](Controls/RegexBuilderView.xaml.cs) uses the singleton [`LocalizationService.Instance`](Services/LocalizationService.cs) to access localized strings
- All string retrieval is done through the `GetString()` and `GetString(key, args)` helper methods
- The localization service handles resource loading based on the current culture setting

### 3. Dynamic Language Switching
- The [`RefreshLocalization()`](Controls/RegexBuilderView.xaml.cs:666) method updates all UI elements when language changes
- This method is called when the application language is changed
- It updates:
  - Text labels for all UI elements
  - Placeholder text for input fields
  - Content for preset buttons
  - Options for checkboxes
  - Dynamic content (match results and Regex breakdown)

### 4. Regex Breakdown Specific Localization
- The Regex parser in [`ParseRegexBreakdown()`](Controls/RegexBuilderView.xaml.cs:351) uses localized strings for all component types and descriptions
- Match result messages are localized with proper pluralization support
- Error messages for invalid patterns are localized
- Dialog messages for preset overwrites are fully localized

## Implementation Details and Special Considerations

### 1. Error Handling
- Comprehensive error handling in [`RefreshLocalization()`](Controls/RegexBuilderView.xaml.cs:666) prevents crashes during language switching
- Each UI element update is wrapped in try-catch blocks with detailed error logging
- Fallback values are provided when localization fails

### 2. Performance Considerations
- The `_isUpdating` flag prevents recursive updates during language switches
- Resource context caching in [`LocalizationService`](Services/LocalizationService.cs) minimizes repeated resource lookups
- Dynamic content updates only refresh changed elements, not the entire UI

### 3. Theme Awareness
- The implementation uses system accent colors with fallbacks for both light and dark themes
- Color selection: `Application.Current.Resources.TryGetValue("SystemAccentColor", out var accentColorObj)`
- Fallback color: `new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215))`

### 4. Accessibility
- All UI elements have proper localized tooltips for screen readers
- High contrast colors are used for important UI elements
- Font weights and styling are maintained across all languages

### 5. Testing Strategy
- Comprehensive test suite with 129 total tests covering:
  - Unit tests: 102 tests for core services and view models
  - Integration tests: 12 tests for end-to-end workflows
  - UI tests: 15 tests for user interface interactions
- Specialized Regex Builder test classes:
    - [`RegexBuilderLocalizationKeysTests`](Tests/Services/RegexBuilderLocalizationKeysTests.cs) - Verifies all keys exist
    - [`RegexBuilderLanguageSwitchingTests`](Tests/Services/RegexBuilderLanguageSwitchingTests.cs) - Tests language switching behavior
    - [`RegexBuilderLanguageIntegrationTests`](Tests/Services/RegexBuilderLanguageIntegrationTests.cs) - Tests integration scenarios

## Guide for Future Developers

### Adding New Translation Strings

1. **Add Keys to Resource Files**
   - Add the new key to all four language files:
     - `Strings/en-US/Resources.resw` (English - reference implementation)
     - `Strings/de-DE/Resources.resw` (German translation)
     - `Strings/es-ES/Resources.resw` (Spanish translation)
     - `Strings/fr-FR/Resources.resw` (French translation)

2. **Key Naming Conventions**
   - Use descriptive, hierarchical naming: `FeatureAreaComponentName`
   - Examples:
     - `RegexBreakdownTypeCharacterClass` for component type names
     - `RegexBreakdownDescCharacterClass` for component descriptions
     - `RegexBreakdownErrorMessage` for messages

3. **Update RegexBuilderLocalizationKeysTests**
   - Add the new key to the `RegexBuilderLocalizationKeys` array in [`RegexBuilderLocalizationKeysTests.cs`](Tests/Services/RegexBuilderLocalizationKeysTests.cs:26)
   - This ensures test coverage for all localization keys

4. **Implement in Code**
   - Use `_localizationService.GetLocalizedString("YourNewKey")` in [`RegexBuilderView.xaml.cs`](Controls/RegexBuilderView.xaml.cs)
   - For formatted strings: `_localizationService.GetLocalizedString("YourKey", parameter1, parameter2)`

### Updating Existing Translations

1. **Modify All Language Files**
   - Update the value in each `.resw` file to maintain consistency
   - Ensure parameter placeholders (e.g., `{0}`, `{1}`) are preserved

2. **Test Parameterized Strings**
   - Verify that formatted strings work correctly with different parameter counts
   - Test edge cases like empty parameters, null parameters, etc.

3. **Refresh Dynamic Content**
   - Call `RefreshLocalization()` after updating resources to ensure immediate UI updates
   - Test with all supported languages to verify consistency

### Testing Localization Changes

1. **Run Unit Tests**
   ```bash
   dotnet test Tests/Services/RegexBuilderLocalizationKeysTests.cs
   ```

2. **Run Integration Tests**
   ```bash
   dotnet test Tests/Services/RegexBuilderLanguageIntegrationTests.cs
   ```

3. **Manual Language Switching Test**
   - Use the [`TestLanguageSwitching`](TestLanguageSwitching.cs:12) utility to test all languages
   - Verify that all UI elements update correctly

## Implementation Summary

### Files Modified

#### Core Implementation Files
1. **Controls/RegexBuilderView.xaml.cs**
   - Added comprehensive localization support with 40+ localized string references
   - Implemented `RefreshLocalization()` method for dynamic language switching
   - Added error handling and logging for all localization operations

#### Resource Files
1. **Strings/en-US/Resources.resw**
   - Added 40+ new localization keys for Visual Regex Breakdown
   - Includes all messages, UI elements, tooltips, and Regex component descriptions

2. **Strings/de-DE/Resources.resw**
   - Complete German translations for all Visual Regex Breakdown keys
   - Maintains consistency with existing German localization patterns

3. **Strings/es-ES/Resources.resw**
   - Complete Spanish translations for all Visual Regex Breakdown keys
   - Follows established Spanish localization conventions

4. **Strings/fr-FR/Resources.resw**
   - Complete French translations for all Visual Regex Breakdown keys
   - Maintains consistency with existing French localization patterns

#### Test Files
1. **Tests/Services/RegexBuilderLocalizationKeysTests.cs**
   - Comprehensive test suite verifying all localization keys are defined
   - Tests key uniqueness, naming conventions, and parameter handling

2. **Tests/Services/RegexBuilderLanguageSwitchingTests.cs**
   - Tests dynamic language switching functionality
   - Verifies proper fallback behavior and error handling

3. **Tests/Services/RegexBuilderLanguageIntegrationTests.cs**
   - Integration tests for full localization workflow
   - Tests culture changes, property change notifications, and edge cases

4. **TestLanguageSwitching.cs**
   - Simple utility for manual testing of all supported languages
   - Validates that all localization keys return valid values

### New Files Created

No new files were created - the implementation extends existing files following Grex's established patterns.

## Architecture of the Solution

### Component Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                    Visual Regex Breakdown Localization                     │
├───────────────────────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────────────┐  ┌────────────────────────────────────────────────────────┐   │
│  │  Resource Files   │  │        RegexBuilderView.xaml.cs             │   │
│  │                 │  │        (Main UI Component)                │   │
│  │  • en-US          │  │                                       │   │
│  │  • de-DE          │  │  ┌───────────────────────────────────────────┐   │
│  │  • es-ES          │  │  │        LocalizationService              │   │
│  │  • fr-FR          │  │  │        (Singleton Service)              │   │
│  └─────────────────────┘  │  │                                       │   │
│                          │  │  └───────────────────────────────────────────┘   │
│                          │  │                                       │
├───────────────────────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────────────┐  ┌────────────────────────────────────────────────────────┐   │
│  │    Test Suite     │  │           Test Architecture               │   │
│  │                   │  │                                       │   │
│  │ • Unit Tests      │  │  ┌───────────────────────────────────────────┐   │
│  │ • Integration    │  │  │        RegexBuilderLocalizationKeysTests │   │
│  │ • UI Tests        │  │  │        RegexBuilderLanguageSwitchingTests │   │
│  │                   │  │        RegexBuilderLanguageIntegrationTests │   │
│  └─────────────────────┘  │  │  └───────────────────────────────────────────┘   │
│                          │  │                                       │
└───────────────────────────────────────────────────────────────────────────────────┘
│                                                                   │
```

### Data Flow

1. **Language Selection**: User selects language in Settings → [`LocalizationService.SetCulture()`](Services/LocalizationService.cs:163)
2. **Resource Loading**: [`LocalizationService`](Services/LocalizationService.cs:112) loads appropriate `.resw` file based on culture
3. **UI Updates**: [`RefreshLocalization()`](Controls/RegexBuilderView.xaml.cs:666) called → All UI elements updated with new strings
4. **Dynamic Content**: Regex parsing and match results use localized strings for types, descriptions, and messages

### Key Design Patterns

1. **Singleton Pattern**: [`LocalizationService.Instance`](Services/LocalizationService.cs:28) ensures consistent resource access
2. **Observer Pattern**: PropertyChanged notifications enable reactive UI updates
3. **Fallback Strategy**: Graceful degradation when resources are missing
4. **Error Boundaries**: Comprehensive try-catch blocks prevent crashes

## Future Maintenance Guidelines

### Adding New Regex Components

1. **Extend ParseRegexBreakdown()**
   - Add new Regex component type with localized type and description
   - Update [`RegexBuilderLocalizationKeysTests`](Tests/Services/RegexBuilderLocalizationKeysTests.cs) with new keys
   - Add translations to all four language resource files

2. **Example for New Component**:
   ```csharp
   // Add to ParseRegexBreakdown method
   if (ch == 'NEW_SYMBOL')
   {
       items.Add(new BreakdownItem
       {
           Type = GetString("RegexBreakdownTypeNewSymbol"),
           Content = content,
           Description = GetString("RegexBreakdownDescNewSymbol")
       });
   }
   ```

### Supporting New Languages

1. **Add New Language Directory**
   - Create `Strings/xx-XX/Resources.resw` following existing pattern
   - Add language to supported cultures in [`LocalizationService.IsValidCulture()`](Services/LocalizationService.cs:234)

2. **Update Application Configuration**
   - Modify build configuration to include new language resources
   - Update any language selection UI to include new option

### Performance Optimization

1. **Minimize Refresh Scope**
   - Only update UI elements that actually changed
   - Use the `_isUpdating` flag to prevent unnecessary refreshes

2. **Resource Caching**
   - The [`LocalizationService`](Services/LocalizationService.cs) already implements resource context caching
   - Ensure new resources benefit from this caching

### Quality Assurance

1. **Comprehensive Testing**
   - Add test cases for any new localization keys
   - Test with all supported languages
   - Include edge cases (missing keys, malformed strings, etc.)

2. **Code Review Checklist**
   - All new strings use `_localizationService.GetLocalizedString()`
   - No hardcoded strings in UI logic
   - Proper error handling for all localization calls
   - Consistent naming conventions followed

## Conclusion

The Visual Regex Breakdown localization implementation provides a robust, extensible foundation for internationalization that:

- **Supports 4 languages** with complete translations
- **Handles dynamic language switching** with immediate UI updates
- **Provides comprehensive test coverage** with 40+ test methods
- **Follows established Grex patterns** for maintainability
- **Includes proper error handling** and fallback mechanisms
- **Is thoroughly documented** for future maintenance

This implementation ensures that the Visual Regex Breakdown feature is fully accessible to international users while maintaining the high-quality standards established throughout the Grex application.
