#!/usr/bin/env python3
"""
Script to report translation status of all language translations.
Uses <comment> elements in .resw files to track translation status.
"""

import xml.etree.ElementTree as ET
import sys
from pathlib import Path
from typing import Tuple

def get_comment_status(data_elem: ET.Element) -> str:
    """Get the comment status from a data element. Returns 'incomplete' if no comment exists."""
    comment_elem = data_elem.find('comment')
    if comment_elem is not None and comment_elem.text:
        status = comment_elem.text.strip()
        if status.startswith('status:complete'):
            return 'complete'
        elif status.startswith('status:error'):
            return 'error'
        elif status.startswith('status:incomplete'):
            return 'incomplete'
    # Default to incomplete if no comment or unrecognized status
    return 'incomplete'

def parse_resw_file_status(file_path: Path) -> Tuple[int, int, int, int]:
    """
    Parse a .resw file and count entries by status.
    Returns: (total, complete, incomplete, error)
    """
    try:
        tree = ET.parse(file_path)
        root = tree.getroot()
        
        total = 0
        complete = 0
        incomplete = 0
        error = 0
        
        for data in root.findall('.//data'):
            name = data.get('name')
            value_elem = data.find('value')
            if name and value_elem is not None:
                total += 1
                status = get_comment_status(data)
                if status == 'complete':
                    complete += 1
                elif status == 'error':
                    error += 1
                else:  # incomplete
                    incomplete += 1
        
        return total, complete, incomplete, error
    except Exception as e:
        print(f"Error parsing {file_path}: {e}")
        return 0, 0, 0, 0

def determine_language_status(total: int, complete: int, incomplete: int, error: int) -> str:
    """
    Determine overall language status based on entry counts.
    - 'complete': All entries are complete (incomplete == 0 and error == 0)
    - 'error': Has errors (error > 0)
    - 'incomplete': Has incomplete entries (incomplete > 0 and error == 0)
    """
    if total == 0:
        return 'incomplete'  # No entries found
    
    if incomplete == 0 and error == 0:
        return 'complete'
    elif error > 0:
        return 'error'
    else:
        return 'incomplete'

def main():
    # Script may be in Scripts/ directory, so look for Strings in parent directory
    script_dir = Path(__file__).parent
    base_dir = script_dir.parent / 'Strings'
    english_file = base_dir / 'en-US' / 'Resources.resw'
    
    if not english_file.exists():
        print(f"Error: {english_file} not found")
        sys.exit(1)
    
    # Get total entries from English file (reference)
    print("Parsing English resource file...")
    total_entries, _, _, _ = parse_resw_file_status(english_file)
    print(f"Found {total_entries} entries in English file\n")
    
    # Find all language directories
    languages = sorted(
        p.name
        for p in base_dir.iterdir()
        if p.is_dir() and p.name != "en-US"
    )
    
    print(f"Processing {len(languages)} language files...\n")
    
    translation_status = {}
    complete_languages = 0
    incomplete_languages = 0
    error_languages = 0
    
    for lang_code in languages:
        lang_dir = base_dir / lang_code
        lang_file = lang_dir / 'Resources.resw'
        
        if not lang_file.exists():
            print(f"Warning: {lang_file} not found, skipping...")
            translation_status[lang_code] = {
                "status": "incomplete",
                "total_entries": 0,
                "complete": 0,
                "incomplete": 0,
                "error": 0,
                "entries_remaining": 0,
                "error_message": "File not found"
            }
            incomplete_languages += 1
            continue
        
        # Parse language file and count statuses
        total, complete, incomplete, error = parse_resw_file_status(lang_file)
        
        # Determine overall language status
        status = determine_language_status(total, complete, incomplete, error)
        
        # Calculate entries remaining (incomplete + error)
        entries_remaining = incomplete + error
        
        translation_status[lang_code] = {
            "status": status,
            "total_entries": total,
            "complete": complete,
            "incomplete": incomplete,
            "error": error,
            "entries_remaining": entries_remaining
        }
        
        # Count languages by status
        if status == 'complete':
            complete_languages += 1
        elif status == 'error':
            error_languages += 1
        else:
            incomplete_languages += 1
    
    print(f"{'='*80}")
    print(f"Translation Status Summary")
    print(f"{'='*80}")
    print(f"Complete: {complete_languages}")
    print(f"Incomplete: {incomplete_languages}")
    print(f"Error: {error_languages}")
    print(f"Total: {complete_languages + incomplete_languages + error_languages}")
    
    # Show top 10 languages with fewest entries remaining
    incomplete_languages_list = [
        (lang, data['entries_remaining'], data['incomplete'], data['error'])
        for lang, data in translation_status.items()
        if data['status'] in ['incomplete', 'error'] and data['entries_remaining'] > 0
    ]
    incomplete_languages_list.sort(key=lambda x: x[1])
    
    if incomplete_languages_list:
        print(f"\n{'='*80}")
        print(f"Top 10 Languages with Fewest Entries Remaining")
        print(f"{'='*80}")
        print(f"{'Language':<20} {'Remaining':<12} {'Incomplete':<12} {'Error':<12} {'Status':<12}")
        print(f"{'-'*80}")
        for lang, remaining, incomplete, error in incomplete_languages_list[:10]:
            status = translation_status[lang]['status']
            print(f"{lang:<20} {remaining:<12} {incomplete:<12} {error:<12} {status:<12}")
    
    # Show languages with errors
    error_languages_list = [
        (lang, data['error'])
        for lang, data in translation_status.items()
        if data['status'] == 'error' and data['error'] > 0
    ]
    error_languages_list.sort(key=lambda x: x[1], reverse=True)
    
    if error_languages_list:
        print(f"\n{'='*80}")
        print(f"Languages with Translation Errors")
        print(f"{'='*80}")
        print(f"{'Language':<20} {'Errors':<12}")
        print(f"{'-'*80}")
        for lang, error_count in error_languages_list[:10]:
            print(f"{lang:<20} {error_count:<12}")

if __name__ == '__main__':
    main()
