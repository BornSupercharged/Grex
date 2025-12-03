# Translation and Localization Guide

## Overview

Grex supports 100+ languages through Windows resource files (`.resw`). All UI text is localized through the `LocalizationService` and stored in `Strings/{culture}/Resources.resw` files.

## Translation Status Tracking

Each entry in a `.resw` file uses a `<comment>` child element to track translation status:

### Status Values

- **`status:incomplete`** - Entry has not been translated yet (default for non-English files)
- **`status:complete`** - Entry has been successfully translated
- **`status:error:{error_details}`** - Translation failed with a specific error
- **`status:error:permanent`** - Translation failed and should not be retried

### Example Entries

**Incomplete (needs translation):**
```xml
<data name="RegexBuilderTab" xml:space="preserve">
  <value>Regex Builder</value>
  <comment>status:incomplete</comment>
</data>
```

**Complete (translated):**
```xml
<data name="RegexBuilderTab" xml:space="preserve">
  <value>Regex Builder</value>
  <comment>status:complete</comment>
</data>
```

**Error (translation failed):**
```xml
<data name="RegexBuilderTab" xml:space="preserve">
  <value>Regex Builder</value>
  <comment>status:error:Could not perform translation because XYZ</comment>
</data>
```

**Permanent Error (will not retry):**
```xml
<data name="RegexBuilderTab" xml:space="preserve">
  <value>Regex Builder</value>
  <comment>status:error:permanent</comment>
</data>
```

## Adding New UI Text

**IMPORTANT**: When adding new UI text to the application:

1. **Route through LocalizationService**: All new UI text must be added to the resource files, not hardcoded in the application.

2. **Use the add_localization_entry.py script (RECOMMENDED)**: This script automatically adds entries to all 100+ language files at once:

   ```bash
   python Scripts/add_localization_entry.py "<key>" "<value>"
   ```

   **Example:**
   ```bash
   python Scripts/add_localization_entry.py "NewFeatureButton.Content" "New Feature"
   ```

   The script automatically:
   - Adds the entry to all `.resw` files in the Strings directory
   - Sets `status:complete` for en-US (English)
   - Sets `status:incomplete` for all other languages (needs translation)
   - Skips files where the key already exists (with a warning)
   - Preserves XML formatting and indentation

3. **Manual method (alternative)**: If you need to add entries manually:

   **Add to English file first**: Add the entry to `Strings/en-US/Resources.resw` with:
   - The English text as the `<value>`
   - `<comment>status:complete</comment>` (English is always complete)

   **Add to all language files**: The same entry must be added to **all 100+ language files** with:
   - The English text as a placeholder in the `<value>`
   - `<comment>status:incomplete</comment>` (default for non-English files)

4. **Example entry format:**
   
   **English (Strings/en-US/Resources.resw):**
   ```xml
   <data name="NewFeatureButton.Content" xml:space="preserve">
     <value>New Feature</value>
     <comment>status:complete</comment>
   </data>
   ```
   
   **All other languages (e.g., Strings/es-ES/Resources.resw):**
   ```xml
   <data name="NewFeatureButton.Content" xml:space="preserve">
     <value>New Feature</value>
     <comment>status:incomplete</comment>
   </data>
   ```

5. **Use the translation script**: Run `python Scripts/translate_remaining_entries.py` to automatically translate all incomplete entries.

## Automated Translation

The `Scripts/translate_remaining_entries.py` script automatically translates entries:

### What it does:

1. **Finds entries to translate**: Only entries that are exact matches to the English value and have `status:incomplete` or `status:error` (not permanent)

2. **Skips completed entries**: Entries with `status:complete` are never processed

3. **Skips permanent errors**: Entries with `status:error:permanent` are never processed

4. **Marks existing translations**: If an entry's value doesn't match English, it's automatically marked as `status:complete`

5. **Creates missing comments**: If an entry has no `<comment>` element, one is created:
   - `status:complete` for English file
   - `status:incomplete` for other files (if value matches English)
   - `status:complete` for other files (if value differs from English)

6. **Handles errors gracefully**: If translation fails:
   - First failure: Marks as `status:error:{error_details}`
   - Subsequent failure: Marks as `status:error:permanent` (won't retry)

7. **Never overwrites**: The script never overwrites existing translations, even if they look like English

### Running the script:

```bash
python Scripts/translate_remaining_entries.py
```

The script will:
- Process all languages alphabetically
- Only translate entries that exactly match English
- Update comment status as it goes
- Handle rate limiting automatically

## Manual Translation

If you need to manually translate entries:

1. Open the appropriate `Strings/{culture}/Resources.resw` file
2. Find the entry you want to translate
3. Update the `<value>` element with the translated text
4. Update the `<comment>` element to `status:complete`

## Technical Terms

Some entries are kept in English across all languages (technical terms, proper nouns, etc.). These are handled automatically by the translation scripts and should not be translated manually.

## File Structure

All language files follow the same structure:
- Location: `Strings/{culture-code}/Resources.resw`
- Format: XML with `<data>`, `<value>`, and `<comment>` elements
- Encoding: UTF-8

## Best Practices

1. **Always add comments**: Every entry must have a `<comment>` element
2. **Use exact matching**: Only translate entries that exactly match English
3. **Never overwrite**: Don't replace existing translations with English text
4. **Test translations**: Verify translations make sense in context
5. **Keep technical terms**: Don't translate technical terms, proper nouns, or acronyms that should stay in English

## Translation Tools

- **Add Entry**: `Scripts/add_localization_entry.py` - Adds a new localization entry to all 100+ language files at once
  ```bash
  python Scripts/add_localization_entry.py "<key>" "<value>"
  ```
- **Remove Entry**: `Scripts/remove_localization_entry.py` - Removes a localization entry from all 100+ language files at once
  ```bash
  python Scripts/remove_localization_entry.py "<key>"
  ```
- **Automated Translation**: `Scripts/translate_remaining_entries.py` - Uses Google Translate API to translate entries marked as `status:incomplete`
- **Status Check**: `Scripts/generate_translation_status.py` - Reports translation status of all language translations

### Running the Scripts

All scripts are located in the `Scripts/` directory and can be run from the project root:

```bash
# Add a new localization entry to all languages
python Scripts/add_localization_entry.py "MyButton.Content" "My Button"

# Remove a localization entry from all languages
python Scripts/remove_localization_entry.py "OldButton.Content"

# Translate all incomplete entries
python Scripts/translate_remaining_entries.py

# Check translation status
python Scripts/generate_translation_status.py
```

### Testing the Scripts

Unit tests for the localization scripts are available:

```bash
# Run tests with pytest (if pytest installed)
python -m pytest Scripts/test_add_localization_entry.py -v
python -m pytest Scripts/test_remove_localization_entry.py -v

# Or run tests directly with unittest
python Scripts/test_add_localization_entry.py
python Scripts/test_remove_localization_entry.py
```

## Support

For questions or issues with translations, refer to:
- `README.md` - General localization information
- `docs/architecture.md` - LocalizationService architecture

