#!/usr/bin/env python3
"""
Script to translate remaining English placeholder values in localization files.
Uses <comment> elements in .resw files to track translation status.
Translates entries alphabetically by language code.
"""

import xml.etree.ElementTree as ET
import sys
import re
from pathlib import Path
from typing import Dict, Optional, Tuple
import time

# Custom exception for invalid destination language
class InvalidDestinationLanguageError(Exception):
    """Raised when the destination language is invalid and we should skip the entire language."""
    pass

# Try to import translation library
try:
    from googletrans import Translator
    HAS_TRANSLATOR = True
except ImportError:
    HAS_TRANSLATOR = False
    print("Warning: googletrans not installed. Install with: pip install googletrans==4.0.0rc1")
    print("Will only identify entries that need translation.")

# Technical keys that are typically kept in English
TECHNICAL_KEYS = {
    'AppName',  # Application name (proper noun)
    'KBComboBoxItem.Content',  # Technical unit
    'MBComboBoxItem.Content',  # Technical unit
    'GBComboBoxItem.Content',  # Technical unit
    'URLPresetButton.Content',  # Technical acronym
}

# Language code mapping for googletrans
LANG_CODE_MAP = {
    'af-ZA': 'af',  # Afrikaans
    'am-ET': 'am',  # Amharic
    'az-AZ': 'az',  # Azerbaijani
    'bs-BA': 'bs',  # Bosnian
    'ca-ES': 'ca',  # Catalan
    'ceb-PH': 'ceb',  # Cebuano
    'cs-CZ': 'cs',  # Czech
    'cy-GB': 'cy',  # Welsh
    'da-DK': 'da',  # Danish
    'de-DE': 'de',  # German
    'es-ES': 'es',  # Spanish
    'fi-FI': 'fi',  # Finnish
    'fil-PH': 'tl',  # Filipino
    'fj-FJ': 'en',  # Fijian (not supported by googletrans, will skip)
    'fr-FR': 'fr',  # French
    'gl-ES': 'gl',  # Galician
    'haw-US': 'haw',  # Hawaiian
    'id-ID': 'id',  # Indonesian
    'it-IT': 'it',  # Italian
    'ja-JP': 'ja',  # Japanese
    'jv-Latn-ID': 'jw',  # Javanese
    'ky-KG': 'ky',  # Kyrgyz
    'lb-LU': 'lb',  # Luxembourgish
    'lo-LA': 'lo',  # Lao
    'mg-MG': 'mg',  # Malagasy
    'mi-NZ': 'mi',  # Maori
    'mk-MK': 'mk',  # Macedonian
    'ml-IN': 'ml',  # Malayalam
    'mn-MN': 'mn',  # Mongolian
    'mr-IN': 'mr',  # Marathi
    'ms-MY': 'ms',  # Malay
    'mt-MT': 'mt',  # Maltese
    'my-MM': 'my',  # Myanmar
    'ne-NP': 'ne',  # Nepali
    'nl-NL': 'nl',  # Dutch
    'no-NO': 'no',  # Norwegian
    'nr-Latn-ZA': 'nr',  # Southern Ndebele
    'nso-Latn-ZA': 'nso',  # Northern Sotho
    'or-IN': 'or',  # Odia
    'pl-PL': 'pl',  # Polish
    'pt-BR': 'pt',  # Portuguese (Brazil)
    'pt-PT': 'pt',  # Portuguese (Portugal)
    'ro-RO': 'ro',  # Romanian
    'ru-RU': 'ru',  # Russian
    'rw-RW': 'rw',  # Kinyarwanda
    'si-LK': 'si',  # Sinhala
    'sk-SK': 'sk',  # Slovak
    'sl-SI': 'sl',  # Slovenian
    'sm-WS': 'sm',  # Samoan
    'sn-Latn-ZW': 'sn',  # Shona
    'so-SO': 'so',  # Somali
    'sq-AL': 'sq',  # Albanian
    'sr-Latn-RS': 'sr',  # Serbian
    'ss-Latn-ZA': 'ss',  # Swati
    'st-Latn-ZA': 'st',  # Southern Sotho
    'su-Latn-ID': 'su',  # Sundanese
    'sv-SE': 'sv',  # Swedish
    'sw-KE': 'sw',  # Swahili
    'ta-IN': 'ta',  # Tamil
    'te-IN': 'te',  # Telugu
    'tg-TJ': 'tg',  # Tajik
    'th-TH': 'th',  # Thai
    'tk-TM': 'tk',  # Turkmen
    'tn-Latn-ZA': 'tn',  # Tswana
    'to-TO': 'to',  # Tongan
    'tr-TR': 'tr',  # Turkish
    'ts-Latn-ZA': 'ts',  # Tsonga
    'ty-Latn-PF': 'ty',  # Tahitian
    'ug-CN': 'ug',  # Uyghur
    'uk-UA': 'uk',  # Ukrainian
    'ur-PK': 'ur',  # Urdu
    'uz-UZ': 'uz',  # Uzbek
    've-Latn-ZA': 've',  # Venda
    'vi-VN': 'vi',  # Vietnamese
    'xh-ZA': 'xh',  # Xhosa
    'yo-NG': 'yo',  # Yoruba
    'zh-CN': 'zh-cn',  # Chinese (Simplified)
    'zh-TW': 'zh-tw',  # Chinese (Traditional)
    'zu-ZA': 'zu',  # Zulu
}

def get_comment_status(data_elem: ET.Element) -> Optional[str]:
    """Get the comment status from a data element. Returns None if no comment exists."""
    comment_elem = data_elem.find('comment')
    if comment_elem is not None and comment_elem.text:
        return comment_elem.text.strip()
    return None

def set_comment_status(data_elem: ET.Element, status: str):
    """Set or update the comment status on a data element."""
    comment_elem = data_elem.find('comment')
    if comment_elem is None:
        # Create new comment element
        comment_elem = ET.SubElement(data_elem, 'comment')
    comment_elem.text = status

def parse_resw_file(file_path: Path) -> Tuple[Dict[str, str], Dict[str, ET.Element]]:
    """
    Parse a .resw file and return:
    1. Dictionary of name -> value
    2. Dictionary of name -> data element (for updating comments)
    """
    tree = ET.parse(file_path)
    root = tree.getroot()
    
    entries = {}
    data_elements = {}
    for data in root.findall('.//data'):
        name = data.get('name')
        value_elem = data.find('value')
        if name and value_elem is not None:
            entries[name] = value_elem.text or ''
            data_elements[name] = data
    
    return entries, data_elements

def find_untranslated_entries(english_entries: Dict[str, str], 
                             target_entries: Dict[str, str],
                             target_data_elements: Dict[str, ET.Element],
                             is_english_file: bool = False) -> Dict[str, str]:
    """
    Find entries in target that need translation.
    - Only finds entries that are EXACT matches to English
    - Skips entries with status:complete or status:error:permanent
    - Marks entries that don't match en-US as status:complete
    - Always marks AppName as status:complete (application name, never translated)
    """
    untranslated = {}
    
    for key, english_value in english_entries.items():
        # Skip technical keys that are typically kept in English
        if key in TECHNICAL_KEYS:
            # Ensure technical keys (AppName, KB, MB, GB) are always marked as complete
            if key in target_data_elements:
                set_comment_status(target_data_elements[key], 'status:complete')
            continue
        
        # AppName, KB, MB, GB should always be marked as complete (never translated)
        if key == 'AppName' or key in ['KBComboBoxItem.Content', 'MBComboBoxItem.Content', 'GBComboBoxItem.Content']:
            if key in target_data_elements:
                set_comment_status(target_data_elements[key], 'status:complete')
            continue
        
        # Note: "Tab" entries will be attempted for translation, but if translation fails,
        # they will be marked as complete in update_resw_file() since it's a technical term
        
        # Get comment status if entry exists
        comment_status = None
        if key in target_data_elements:
            comment_status = get_comment_status(target_data_elements[key])
        
        # Skip if already marked as complete
        if comment_status == 'status:complete':
            continue
        
        # Skip if marked as permanent error
        if comment_status and comment_status.startswith('status:error:permanent'):
            continue
        
        if key not in target_entries:
            # Missing entry - needs translation
            untranslated[key] = english_value
        else:
            target_value = target_entries[key]
            
            # If value doesn't match English, mark as complete (already translated)
            if target_value != english_value:
                if key in target_data_elements:
                    set_comment_status(target_data_elements[key], 'status:complete')
                continue
            
            # Value matches English exactly - check if we should translate
            # Only translate if status is incomplete or error (not permanent)
            if comment_status is None:
                # No comment - default to incomplete
                untranslated[key] = english_value
            elif comment_status == 'status:incomplete':
                # Explicitly marked as incomplete
                untranslated[key] = english_value
            elif comment_status.startswith('status:error:'):
                # Previous error (not permanent) - try again
                untranslated[key] = english_value
            # If status:complete or status:error:permanent, skip (already handled above)
    
    return untranslated

def translate_text(text: str, target_lang: str, translator: Optional[Translator] = None, max_retries: int = 10) -> Optional[str]:
    """
    Translate text to target language with retry logic.
    Returns None if translation fails (caller should leave entry as is).
    Never adds prefixes like [HQ: xx-XX] or [MT: xx-XX].
    """
    if not HAS_TRANSLATOR or translator is None:
        return None
    
    # Remove any existing prefixes if present (but we won't add new ones)
    text_clean = re.sub(r'^\[(HQ|MT):\s*[^\]]+\]\s*', '', text)
    
    # Retry logic for rate limiting and API issues
    for attempt in range(max_retries):
        try:
            # Translate
            result = translator.translate(text_clean, dest=target_lang, src='en')
            translated = result.text
            
            # Use the translation even if it looks like English
            # (some languages may have similar words/phrases)
            return translated
        except AttributeError as e:
            # Handle googletrans internal errors (like 'raise_Exception' attribute errors)
            # This often indicates rate limiting or API changes
            if 'raise_Exception' in str(e) or 'Translator' in str(e) or 'object has no attribute' in str(e):
                if attempt < max_retries - 1:
                    # Rate limiting or API issue - wait longer and retry
                    # Wait 2s after first attempt, 4s after second, 6s after third, etc.
                    wait_time = (attempt + 1) * 2
                    print(f"    Rate limit/API issue detected ({e}), waiting {wait_time}s before retry {attempt + 2}/{max_retries}...")
                    time.sleep(wait_time)
                    continue
                else:
                    print(f"    Translation failed after {max_retries} attempts (rate limit/API issue: {e})")
                    return None
            else:
                print(f"    Error translating: {e}")
                return None
        except Exception as e:
            error_str = str(e).lower()
            # Check for invalid destination language - this means we should skip the entire language
            if 'invalid destination language' in error_str:
                print(f"    Error translating: {e}")
                raise InvalidDestinationLanguageError(f"Invalid destination language: {target_lang}")
            # Check for rate limiting indicators
            if any(indicator in error_str for indicator in ['rate', 'limit', 'quota', '429', 'too many', 'throttle']):
                if attempt < max_retries - 1:
                    # Wait 2s after first attempt, 4s after second, 6s after third, etc.
                    wait_time = (attempt + 1) * 2
                    print(f"    Rate limit detected, waiting {wait_time}s before retry {attempt + 2}/{max_retries}...")
                    time.sleep(wait_time)
                    continue
                else:
                    print(f"    Translation failed after {max_retries} attempts (rate limited)")
                    return None
            else:
                # Other errors - don't retry
                print(f"    Error translating: {e}")
                return None
    
    return None  # All retries failed

def update_resw_file(file_path: Path, 
                    translations: Dict[str, Optional[str]], 
                    translation_errors: Dict[str, str],
                    english_entries: Dict[str, str],
                    is_english_file: bool = False):
    """
    Update a .resw file with translated values and comment status.
    IMPORTANT: Only updates entries that are exact matches to English.
    Never overwrites existing translations.
    Creates comment elements if they don't exist.
    """
    tree = ET.parse(file_path)
    root = tree.getroot()

    # Build lookup from this tree so updates persist when writing the file back
    tree_data_elements: Dict[str, ET.Element] = {}
    for data in root.findall('.//data'):
        name = data.get('name')
        if name:
            tree_data_elements[name] = data
    
    updated_count = 0
    skipped_count = 0
    marked_complete_count = 0
    error_count = 0
    
    # Process translations
    for key, translated_value in translations.items():
        data_elem = tree_data_elements.get(key)

        if data_elem is None:
            # Entry doesn't exist - create it
            new_data = ET.Element('data', name=key, attrib={'xml:space': 'preserve'})
            value_elem = ET.SubElement(new_data, 'value')
            value_elem.text = translated_value if translated_value else english_entries.get(key, '')
            set_comment_status(new_data, 'status:incomplete' if not translated_value else 'status:complete')
            root.append(new_data)
            tree_data_elements[key] = new_data
            if translated_value:
                updated_count += 1
            continue

        value_elem = data_elem.find('value')
        current_value = (value_elem.text or '') if value_elem is not None else ''
        english_value = english_entries.get(key, '')
        
        # Safety check: Only update if current value is exact match to English
        if current_value != english_value:
            # Already translated - mark as complete
            set_comment_status(data_elem, 'status:complete')
            marked_complete_count += 1
            continue
        
        # Update value if translation succeeded
        if value_elem is None:
            value_elem = ET.SubElement(data_elem, 'value')

        if translated_value:
            value_elem.text = translated_value
            set_comment_status(data_elem, 'status:complete')
            updated_count += 1
        elif translated_value is None:
            # Translation failed or returned same as English
            error_msg = translation_errors.get(key, 'Translation failed')
            
            # Special handling: If translation returned same as English, mark as complete
            if error_msg == "Translation same as English":
                # Translation service returned same text - mark as complete (technical term)
                set_comment_status(data_elem, 'status:complete')
                marked_complete_count += 1
            # Special handling: If the value is "Tab" (technical term), mark as complete
            elif english_value == 'Tab':
                # Tab is a technical term - mark as complete even if translation failed
                set_comment_status(data_elem, 'status:complete')
                marked_complete_count += 1
            else:
                # Regular translation failure
                error_count += 1
                # Check if this is a retry of a previous error
                comment_status = get_comment_status(data_elem)
                if comment_status and comment_status.startswith('status:error:'):
                    # Previous error - mark as permanent
                    set_comment_status(data_elem, 'status:error:permanent')
                else:
                    # First error - mark with error details
                    set_comment_status(data_elem, f'status:error:{error_msg}')
    
    # Ensure all entries have comment elements
    # This MUST run for every entry in the file to ensure no entries are missing comments
    for data in root.findall('.//data'):
        name = data.get('name')
        value_elem = data.find('value')
        
        # Skip entries without a name or value
        if not name or value_elem is None:
            continue
        
        # AppName, KB, MB, GB should always be marked as complete (never translated)
        if name == 'AppName' or name in ['KBComboBoxItem.Content', 'MBComboBoxItem.Content', 'GBComboBoxItem.Content']:
            set_comment_status(data, 'status:complete')
            continue
        
        # Note: "Tab" entries will be attempted for translation, but if translation fails,
        # they will be marked as complete in the translation error handling since it's a technical term
        
        # Check if entry has a comment
        comment_status = get_comment_status(data)
        # Check existing comment status first
        if comment_status is not None:
            if comment_status.startswith('status:complete') or comment_status.startswith('status:error:'):
                continue

        if comment_status is None:
            # No comment - create default based on file type
            if is_english_file:
                # English file: all entries default to complete
                set_comment_status(data, 'status:complete')
            else:
                # Non-English file: check if value matches English
                current_value = value_elem.text or ''
                english_value = english_entries.get(name, '')
                if name in english_entries:
                    # Entry exists in English file - compare values
                    if current_value == english_value:
                        # Value matches English - needs translation
                        set_comment_status(data, 'status:incomplete')
                    else:
                        # Value differs from English - already translated
                        set_comment_status(data, 'status:complete')
                else:
                    # Entry not in English file - mark as incomplete (shouldn't happen, but handle gracefully)
                    set_comment_status(data, 'status:incomplete')
        else:
            # If comment already set, do not override 
            if comment_status.startswith('status:complete') or comment_status.startswith('status:error:'):
                continue

    # Format XML with proper indentation
    # ET.indent() is available in Python 3.9+
    try:
        ET.indent(tree, space="  ", level=0)
    except AttributeError:
        # Python < 3.9 doesn't have indent(), use manual formatting
        pass
    
    # Write back to file
    tree.write(file_path, encoding='utf-8', xml_declaration=True)
    
    # Post-process to ensure comment elements are on their own line
    # This is needed because ET.write() doesn't always format comments nicely
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Fix comment elements that are on the same line as closing data tag
    # Pattern: </comment></data> -> </comment>\n  </data>
    # Match with optional whitespace between
    content = re.sub(r'</comment>\s*</data>', r'</comment>\n  </data>', content)
    # Pattern: <comment>...</comment></data> -> <comment>...</comment>\n  </data>
    # Match comment element followed by closing data tag
    content = re.sub(r'(<comment>.*?</comment>)\s*</data>', r'\1\n  </data>', content, flags=re.DOTALL)
    
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)
    
    return updated_count, skipped_count, marked_complete_count, error_count

def main():
    # Script may be in Scripts/ directory, so look for Strings in parent directory
    script_dir = Path(__file__).parent
    base_dir = script_dir.parent / 'Strings'
    english_file = base_dir / 'en-US' / 'Resources.resw'
    
    if not english_file.exists():
        print(f"Error: {english_file} not found")
        sys.exit(1)
    
    # Parse English file
    print("Parsing English resource file...")
    english_entries, english_data_elements = parse_resw_file(english_file)
    print(f"Found {len(english_entries)} entries in English file")
    
    # Ensure English file has comment elements (all should be status:complete)
    print("Ensuring English file has comment elements...")
    update_resw_file(english_file, {}, {}, english_entries, is_english_file=True)
    print("English file comments updated.\n")
    
    # Find all language directories (excluding en-US)
    languages = sorted(
        p.name
        for p in base_dir.iterdir()
        if p.is_dir() and p.name != "en-US"
    )
    
    print(f"Found {len(languages)} language files to process")
    print(f"Languages will be processed in alphabetical order.\n")
    
    # Initialize translator if available
    translator = None
    if HAS_TRANSLATOR:
        try:
            translator = Translator()
            print("Translation service initialized.\n")
        except Exception as e:
            print(f"Warning: Could not initialize translator: {e}\n")
            print("Will only identify entries that need translation.\n")
    
    # Process each language
    for lang_code in languages:
        lang_dir = base_dir / lang_code
        lang_file = lang_dir / 'Resources.resw'
        
        if not lang_file.exists():
            print(f"[WARN] {lang_code}: File not found, skipping...")
            continue
        
        print(f"{'='*80}")
        print(f"Processing: {lang_code}")
        print(f"{'='*80}")
        
        # Parse target language file
        target_entries, target_data_elements = parse_resw_file(lang_file)
        
        # FIRST: Ensure all entries have comment elements BEFORE processing
        # This must run for every file to ensure no entries are missing comments
        update_resw_file(lang_file, {}, {}, english_entries, is_english_file=False)
        
        # Re-parse after ensuring comment elements
        target_entries, target_data_elements = parse_resw_file(lang_file)
        
        # Load the XML tree to update comment status
        tree = ET.parse(lang_file)
        root = tree.getroot()
        
        # Create mapping of name -> data element from the tree
        tree_data_elements = {}
        for data in root.findall('.//data'):
            name = data.get('name')
            if name:
                tree_data_elements[name] = data
        
        # Find untranslated entries (this also marks non-matching entries as complete)
        # Pass tree_data_elements so changes are made to the actual XML tree
        untranslated = find_untranslated_entries(english_entries, target_entries, tree_data_elements)
        
        # Save any comment status changes made during find_untranslated_entries
        tree.write(lang_file, encoding='utf-8', xml_declaration=True)
        
        # Post-process to ensure comment elements are on their own line
        with open(lang_file, 'r', encoding='utf-8') as f:
            content = f.read()
        content = re.sub(r'</comment>\s*</data>', r'</comment>\n  </data>', content)
        content = re.sub(r'(<comment>.*?</comment>)\s*</data>', r'\1\n  </data>', content, flags=re.DOTALL)
        with open(lang_file, 'w', encoding='utf-8') as f:
            f.write(content)
        
        # Re-parse to get updated data elements
        target_entries, target_data_elements = parse_resw_file(lang_file)
        
        if not untranslated:
            print(f"  [OK] No untranslated entries found")
            continue
        
        print(f"  Found {len(untranslated)} entries to translate")
        
        # Get target language code for translation
        target_lang_code = LANG_CODE_MAP.get(lang_code, lang_code.split('-')[0])
        
        # Check if language is supported (skip if target is 'en' for unsupported languages)
        if target_lang_code == 'en' and lang_code != 'en-US':
            print(f"  [WARN] Language {lang_code} is not supported by translation service")
            print(f"  Marking untranslated entries appropriately")
            # Mark entries: if value differs from English, mark as complete; otherwise mark as error:permanent
            tree = ET.parse(lang_file)
            root = tree.getroot()
            for data in root.findall('.//data'):
                name = data.get('name')
                if name in untranslated:
                    value_elem = data.find('value')
                    if value_elem is not None:
                        current_value = value_elem.text or ''
                        english_value = english_entries.get(name, '')
                        # If value differs from English, it's already translated - mark as complete
                        if current_value != english_value:
                            set_comment_status(data, 'status:complete')
                        # Special handling for technical terms
                        elif english_value == 'Tab':
                            set_comment_status(data, 'status:complete')
                        else:
                            # Value matches English and not a technical term - mark as permanent error
                            set_comment_status(data, 'status:error:permanent')
            tree.write(lang_file, encoding='utf-8', xml_declaration=True)
            # Post-process XML formatting
            with open(lang_file, 'r', encoding='utf-8') as f:
                content = f.read()
            content = re.sub(r'</comment>\s*</data>', r'</comment>\n  </data>', content)
            content = re.sub(r'(<comment>.*?</comment>)\s*</data>', r'\1\n  </data>', content, flags=re.DOTALL)
            with open(lang_file, 'w', encoding='utf-8') as f:
                f.write(content)
            continue
        
        # Translate entries
        translations = {}
        translation_errors = {}
        consecutive_failures = 0
        total_updated = 0
        total_skipped = 0
        total_marked_complete = 0
        total_errors = 0
        BATCH_SIZE = 5  # Write to file every 5 translations
        
        for i, (key, english_value) in enumerate(untranslated.items(), 1):
            print(f"  [{i}/{len(untranslated)}] Translating: {key}")
            
            if translator:
                try:
                    translated = translate_text(english_value, target_lang_code, translator)
                except InvalidDestinationLanguageError as e:
                    # Invalid destination language - skip remaining entries for this language
                    # Write accumulated translations before handling the error
                    if translations or translation_errors:
                        updated, skipped, marked_complete, errors = update_resw_file(
                            lang_file, translations, translation_errors, english_entries
                        )
                        total_updated += updated
                        total_skipped += skipped
                        total_marked_complete += marked_complete
                        total_errors += errors
                        translations.clear()
                        translation_errors.clear()
                    
                    print(f"\n  [ERROR] {e}")
                    print(f"  Skipping remaining entries for {lang_code} due to invalid destination language")
                    # Mark current entry as error
                    translations[key] = None
                    translation_errors[key] = "Invalid destination language"
                    update_resw_file(lang_file, translations, translation_errors, english_entries)
                    total_errors += 1
                    translations.clear()
                    translation_errors.clear()
                    
                    # Mark all remaining untranslated entries as permanent errors
                    remaining_entries = list(untranslated.items())[i:]  # From next entry onwards
                    batch_translations = {}
                    batch_errors = {}
                    for remaining_key, remaining_value in remaining_entries:
                        batch_translations[remaining_key] = None
                        batch_errors[remaining_key] = "Invalid destination language"
                    update_resw_file(lang_file, batch_translations, batch_errors, english_entries)
                    total_errors += len(batch_translations)
                    break  # Exit the translation loop for this language
                
                if translated is None:
                    consecutive_failures += 1
                    error_msg = "Translation service returned None"
                    print(f"    [WARN] Translation failed - will mark as error")
                    translations[key] = None
                    translation_errors[key] = error_msg
                    
                    # If we have many consecutive failures, increase delay to avoid rate limiting
                    if consecutive_failures >= 5:
                        print(f"    Multiple consecutive failures detected, increasing delay...")
                        time.sleep(2)
                        consecutive_failures = 0  # Reset counter
                    else:
                        time.sleep(0.2)
                else:
                    # Translation succeeded - reset failure counter
                    consecutive_failures = 0
                    # Check if translation is the same as English (common for technical terms)
                    if translated.strip() == english_value.strip():
                        # Translation returned same text - mark as complete (technical term or no translation needed)
                        print(f"    Translation same as English - marking as complete (technical term)")
                        translations[key] = None  # Don't update value, but mark as complete
                        translation_errors[key] = "Translation same as English"  # Special marker
                    else:
                        translations[key] = translated
                        print(f"    English: {english_value[:60]}...")
                        print(f"    {lang_code}: {translated[:60]}...")
                    # Small delay to avoid rate limiting
                    time.sleep(0.2)
            else:
                # Just show what needs translation
                print(f"    Needs translation: {english_value[:80]}...")
                translations[key] = None  # No translator available
                translation_errors[key] = "Translator not available"
            
            # Save every BATCH_SIZE translations or on the final entry
            should_save = (i % BATCH_SIZE == 0) or (i == len(untranslated))
            if should_save and (translations or translation_errors):
                updated, skipped, marked_complete, errors = update_resw_file(
                    lang_file, translations, translation_errors, english_entries
                )
                total_updated += updated
                total_skipped += skipped
                total_marked_complete += marked_complete
                total_errors += errors
                # Clear the dictionaries after saving
                translations.clear()
                translation_errors.clear()
        
        # Print final summary
        print(f"\n  Translation summary for {lang_code}:")
        print(f"  [OK] Updated {total_updated} entries")
        if total_marked_complete > 0:
            print(f"  [OK] Marked {total_marked_complete} entries as complete (already translated)")
        if total_errors > 0:
            print(f"  [WARN] {total_errors} entries failed translation (marked as error)")
        if total_skipped > 0:
            print(f"  [WARN] Skipped {total_skipped} entries")
        
        print()
        
        # Small delay between languages to avoid rate limiting
        time.sleep(1)

if __name__ == '__main__':
    main()
