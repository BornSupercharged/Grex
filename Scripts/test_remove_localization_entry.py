#!/usr/bin/env python3
"""
Unit tests for remove_localization_entry.py

Run with: python -m pytest Scripts/test_remove_localization_entry.py -v
Or: python Scripts/test_remove_localization_entry.py
"""

import os
import sys
import tempfile
import shutil
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

# Add the Scripts directory to the path so we can import the module
sys.path.insert(0, os.path.dirname(__file__))

from remove_localization_entry import remove_entry, get_strings_directory


class TestRemoveLocalizationEntry(unittest.TestCase):
    """Test cases for the remove_localization_entry script."""
    
    def setUp(self):
        """Set up test fixtures."""
        self.test_dir = tempfile.mkdtemp()
        self.en_us_dir = Path(self.test_dir) / "en-US"
        self.fr_fr_dir = Path(self.test_dir) / "fr-FR"
        self.en_us_dir.mkdir()
        self.fr_fr_dir.mkdir()
        
        # Create sample .resw files with multiple entries
        self.sample_resw_content = '''<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="ExistingKey" xml:space="preserve">
    <value>Existing Value</value>
    <comment>status:complete</comment>
  </data>
  <data name="KeyToRemove" xml:space="preserve">
    <value>Value To Remove</value>
    <comment>status:complete</comment>
  </data>
  <data name="AnotherKey" xml:space="preserve">
    <value>Another Value</value>
    <comment>status:complete</comment>
  </data>
</root>
'''
        (self.en_us_dir / "Resources.resw").write_text(self.sample_resw_content, encoding='utf-8')
        (self.fr_fr_dir / "Resources.resw").write_text(self.sample_resw_content, encoding='utf-8')
    
    def tearDown(self):
        """Clean up test fixtures."""
        shutil.rmtree(self.test_dir)
    
    def test_remove_existing_entry(self):
        """Test removing an existing entry from files."""
        removed_count = remove_entry("KeyToRemove", Path(self.test_dir))
        
        # Should have removed from both files
        self.assertEqual(removed_count, 2)
        
        # Verify the entry was removed from en-US
        tree = ET.parse(self.en_us_dir / "Resources.resw")
        root = tree.getroot()
        
        keys_found = set()
        for data in root.findall('data'):
            keys_found.add(data.get('name'))
        
        self.assertNotIn("KeyToRemove", keys_found)
        self.assertIn("ExistingKey", keys_found)
        self.assertIn("AnotherKey", keys_found)
    
    def test_remove_nonexistent_entry(self):
        """Test removing an entry that doesn't exist."""
        removed_count = remove_entry("NonExistentKey", Path(self.test_dir))
        
        # Should not have removed anything
        self.assertEqual(removed_count, 0)
        
        # Verify original entries are still present
        tree = ET.parse(self.en_us_dir / "Resources.resw")
        root = tree.getroot()
        
        keys_found = set()
        for data in root.findall('data'):
            keys_found.add(data.get('name'))
        
        self.assertIn("ExistingKey", keys_found)
        self.assertIn("KeyToRemove", keys_found)
        self.assertIn("AnotherKey", keys_found)
    
    def test_remove_preserves_other_entries(self):
        """Test that removing one entry preserves all other entries."""
        # Get original key count
        tree = ET.parse(self.en_us_dir / "Resources.resw")
        root = tree.getroot()
        original_count = len(root.findall('data'))
        
        # Remove one entry
        remove_entry("KeyToRemove", Path(self.test_dir))
        
        # Verify only one entry was removed
        tree = ET.parse(self.en_us_dir / "Resources.resw")
        root = tree.getroot()
        new_count = len(root.findall('data'))
        
        self.assertEqual(new_count, original_count - 1)
    
    def test_remove_from_multiple_language_files(self):
        """Test that entries are removed from all language files."""
        remove_entry("KeyToRemove", Path(self.test_dir))
        
        # Verify removal from en-US
        tree_en = ET.parse(self.en_us_dir / "Resources.resw")
        keys_en = {data.get('name') for data in tree_en.getroot().findall('data')}
        self.assertNotIn("KeyToRemove", keys_en)
        
        # Verify removal from fr-FR
        tree_fr = ET.parse(self.fr_fr_dir / "Resources.resw")
        keys_fr = {data.get('name') for data in tree_fr.getroot().findall('data')}
        self.assertNotIn("KeyToRemove", keys_fr)
    
    def test_get_strings_directory(self):
        """Test that get_strings_directory returns a valid path."""
        strings_dir = get_strings_directory()
        
        # The path should end with 'Strings'
        self.assertEqual(strings_dir.name, "Strings")
    
    def test_remove_entry_with_dots_in_name(self):
        """Test removing an entry with dots in the key name (e.g., 'Button.Content')."""
        # Add an entry with dots in the name
        resw_file = self.en_us_dir / "Resources.resw"
        tree = ET.parse(resw_file)
        root = tree.getroot()
        
        new_data = ET.SubElement(root, 'data')
        new_data.set('name', 'MyButton.Content')
        new_data.set('{http://www.w3.org/XML/1998/namespace}space', 'preserve')
        value = ET.SubElement(new_data, 'value')
        value.text = "Click Me"
        comment = ET.SubElement(new_data, 'comment')
        comment.text = "status:complete"
        
        tree.write(resw_file, encoding='utf-8', xml_declaration=True)
        
        # Now remove it
        removed_count = remove_entry("MyButton.Content", Path(self.test_dir))
        
        # Should have removed from at least one file
        self.assertGreaterEqual(removed_count, 1)
        
        # Verify it was removed
        tree = ET.parse(resw_file)
        root = tree.getroot()
        
        keys_found = {data.get('name') for data in root.findall('data')}
        self.assertNotIn("MyButton.Content", keys_found)
    
    def test_remove_all_entries_of_same_name(self):
        """Test that all entries with the same name are removed (if duplicates exist)."""
        # This tests the behavior when processing multiple files
        removed_count = remove_entry("ExistingKey", Path(self.test_dir))
        
        # Should have removed from both files
        self.assertEqual(removed_count, 2)


class TestRemoveLocalizationEntryIntegration(unittest.TestCase):
    """Integration tests that verify the script works with the real Strings directory."""
    
    def test_strings_directory_exists(self):
        """Verify that the Strings directory exists in the project."""
        strings_dir = get_strings_directory()
        
        # This test will only pass if run from the project context
        if strings_dir.exists():
            self.assertTrue(strings_dir.is_dir())
            
            # Check that en-US exists
            en_us = strings_dir / "en-US"
            self.assertTrue(en_us.exists(), "en-US directory should exist")
            
            # Check that Resources.resw exists in en-US
            resw = en_us / "Resources.resw"
            self.assertTrue(resw.exists(), "en-US/Resources.resw should exist")
    
    def test_script_does_not_remove_required_entries(self):
        """Verify that required entries like AppName still exist."""
        strings_dir = get_strings_directory()
        
        if not strings_dir.exists():
            self.skipTest("Strings directory not found")
        
        en_us_resw = strings_dir / "en-US" / "Resources.resw"
        if not en_us_resw.exists():
            self.skipTest("en-US/Resources.resw not found")
        
        tree = ET.parse(en_us_resw)
        root = tree.getroot()
        
        # Verify essential entries exist
        essential_keys = ["AppName", "SearchTab", "SettingsTab"]
        keys_found = {data.get('name') for data in root.findall('data')}
        
        for key in essential_keys:
            self.assertIn(key, keys_found, f"Essential key '{key}' should exist in en-US/Resources.resw")


if __name__ == '__main__':
    unittest.main(verbosity=2)

