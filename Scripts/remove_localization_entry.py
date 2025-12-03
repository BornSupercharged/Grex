#!/usr/bin/env python3
"""
remove_localization_entry.py

Usage: python Scripts/remove_localization_entry.py "SomeButton.Content"

Deletes the specified localization entry from all Strings/*/Resources.resw files.
"""

import os
import sys
import glob
import xml.etree.ElementTree as ET
from pathlib import Path

def get_strings_directory():
    """Get the Strings directory path relative to this script."""
    script_dir = Path(__file__).parent
    strings_dir = script_dir.parent / "Strings"
    return strings_dir

def remove_entry(entry_name, strings_dir):
    """Remove the specified data entry from all Resources.resw files."""
    # Find all Resources.resw files matching the pattern
    files = glob.glob(str(strings_dir / "*/Resources.resw"))
    
    if not files:
        print("No Resources.resw files found in Strings/*/Resources.resw")
        return 0
    
    removed_count = 0
    for file_path in files:
        abs_path = Path(file_path).resolve()
        try:
            tree = ET.parse(abs_path)
            root = tree.getroot()
            
            # Find and remove data elements with matching name attribute
            data_elements = root.findall(f'.//data[@name="{entry_name}"]')
            
            for data_elem in data_elements:
                root.remove(data_elem)
                print(f"✓ Removed '{entry_name}' from {abs_path}")
                removed_count += 1
            
            # Write back only if changes were made
            if data_elements:
                tree.write(abs_path, encoding='utf-8', xml_declaration=True)
                
        except ET.ParseError as e:
            print(f"✗ Parse error in {abs_path}: {e}")
        except Exception as e:
            print(f"✗ Error processing {abs_path}: {e}")
    
    return removed_count

def main():
    if len(sys.argv) != 2:
        print("Usage: python Scripts/remove_localization_entry.py \"SomeButton.Content\"")
        print("Note: Entry name must be quoted if it contains dots or special characters.")
        sys.exit(1)
    
    entry_name = sys.argv[1].strip()
    if not entry_name:
        print("Error: Entry name cannot be empty")
        sys.exit(1)
    
    strings_dir = get_strings_directory()
    if not strings_dir.exists():
        print(f"Error: Strings directory not found at {strings_dir}")
        sys.exit(1)
    
    print(f"Removing localization entry: '{entry_name}'")
    print(f"Searching in: {strings_dir}/*/Resources.resw")
    
    count = remove_entry(entry_name, strings_dir)
    
    if count == 0:
        print("No matching entries found.")
    else:
        print(f"\n✅ Completed: Removed {count} entries across {len(glob.glob(str(strings_dir / '*/Resources.resw')))} files.")

if __name__ == "__main__":
    main()

