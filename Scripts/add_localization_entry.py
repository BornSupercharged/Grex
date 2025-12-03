#!/usr/bin/env python3
"""
Add a localization entry to all language resource files.

Usage:
    python add_localization_entry.py <key> <value>

Example:
    python add_localization_entry.py "StopButton" "Stop"

This script will:
- Add the entry to all .resw files in the Strings directory
- For en-US: uses "status:complete"
- For all other languages: uses "status:incomplete"
- If the key already exists, it will skip that file (with a warning)
"""

import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def get_strings_directory():
    """Get the Strings directory path relative to this script."""
    script_dir = Path(__file__).parent
    strings_dir = script_dir.parent / "Strings"
    return strings_dir


def add_entry_to_resw(file_path: Path, key: str, value: str, is_english: bool) -> tuple[bool, str]:
    """
    Add an entry to a .resw file.
    
    Returns:
        tuple: (success: bool, message: str)
    """
    try:
        # Parse the XML file
        tree = ET.parse(file_path)
        root = tree.getroot()
        
        # Check if the key already exists
        for data_elem in root.findall('data'):
            if data_elem.get('name') == key:
                return False, f"Key '{key}' already exists"
        
        # Create the new data element
        data_elem = ET.SubElement(root, 'data')
        data_elem.set('name', key)
        data_elem.set('{http://www.w3.org/XML/1998/namespace}space', 'preserve')
        
        # Add value element
        value_elem = ET.SubElement(data_elem, 'value')
        value_elem.text = value
        
        # Add comment element with status
        comment_elem = ET.SubElement(data_elem, 'comment')
        comment_elem.text = "status:complete" if is_english else "status:incomplete"
        
        # Write the file back
        # We need to handle the formatting manually to match the existing style
        tree.write(file_path, encoding='utf-8', xml_declaration=True)
        
        # Read and reformat to match existing style (with proper indentation)
        reformat_resw_file(file_path)
        
        return True, "Added successfully"
        
    except ET.ParseError as e:
        return False, f"XML parse error: {e}"
    except Exception as e:
        return False, f"Error: {e}"


def reformat_resw_file(file_path: Path):
    """Reformat the .resw file to have proper indentation."""
    try:
        tree = ET.parse(file_path)
        root = tree.getroot()
        
        # Indent the tree
        indent_xml(root)
        
        # Write with proper XML declaration
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write('<?xml version="1.0" encoding="utf-8"?>\n')
            # Convert to string and write
            xml_str = ET.tostring(root, encoding='unicode')
            f.write(xml_str)
            f.write('\n')
            
    except Exception as e:
        print(f"Warning: Could not reformat {file_path}: {e}")


def indent_xml(elem, level=0):
    """Add proper indentation to XML elements."""
    indent = "\n" + "  " * level
    if len(elem):
        if not elem.text or not elem.text.strip():
            elem.text = indent + "  "
        if not elem.tail or not elem.tail.strip():
            elem.tail = indent
        for child in elem:
            indent_xml(child, level + 1)
        if not child.tail or not child.tail.strip():
            child.tail = indent
    else:
        if level and (not elem.tail or not elem.tail.strip()):
            elem.tail = indent


def main():
    if len(sys.argv) != 3:
        print("Usage: python add_localization_entry.py <key> <value>")
        print("Example: python add_localization_entry.py StopButton Stop")
        sys.exit(1)
    
    key = sys.argv[1]
    value = sys.argv[2]
    
    strings_dir = get_strings_directory()
    
    if not strings_dir.exists():
        print(f"Error: Strings directory not found at {strings_dir}")
        sys.exit(1)
    
    print(f"Adding entry: key='{key}', value='{value}'")
    print(f"Strings directory: {strings_dir}")
    print("-" * 50)
    
    success_count = 0
    skip_count = 0
    error_count = 0
    
    # Get all language directories
    lang_dirs = sorted([d for d in strings_dir.iterdir() if d.is_dir()])
    
    for lang_dir in lang_dirs:
        resw_file = lang_dir / "Resources.resw"
        
        if not resw_file.exists():
            print(f"  {lang_dir.name}: No Resources.resw found")
            error_count += 1
            continue
        
        is_english = lang_dir.name == "en-US"
        success, message = add_entry_to_resw(resw_file, key, value, is_english)
        
        status_marker = "✓" if success else ("⚠" if "already exists" in message else "✗")
        print(f"  {status_marker} {lang_dir.name}: {message}")
        
        if success:
            success_count += 1
        elif "already exists" in message:
            skip_count += 1
        else:
            error_count += 1
    
    print("-" * 50)
    print(f"Summary: {success_count} added, {skip_count} skipped (already exist), {error_count} errors")
    print(f"Total language directories processed: {len(lang_dirs)}")


if __name__ == "__main__":
    main()

