#!/usr/bin/env python3
"""
Unit tests for add_localization_entry.py

Run with: python -m pytest Scripts/test_add_localization_entry.py -v
Or: python Scripts/test_add_localization_entry.py
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

from add_localization_entry import add_entry_to_resw, get_strings_directory


class TestAddLocalizationEntry(unittest.TestCase):
    """Test cases for the add_localization_entry script."""
    
    def setUp(self):
        """Set up test fixtures."""
        self.test_dir = tempfile.mkdtemp()
        self.en_us_dir = Path(self.test_dir) / "en-US"
        self.fr_fr_dir = Path(self.test_dir) / "fr-FR"
        self.en_us_dir.mkdir()
        self.fr_fr_dir.mkdir()
        
        # Create sample .resw files
        self.sample_resw_content = '''<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="ExistingKey" xml:space="preserve">
    <value>Existing Value</value>
    <comment>status:complete</comment>
  </data>
</root>
'''
        (self.en_us_dir / "Resources.resw").write_text(self.sample_resw_content, encoding='utf-8')
        (self.fr_fr_dir / "Resources.resw").write_text(self.sample_resw_content, encoding='utf-8')
    
    def tearDown(self):
        """Clean up test fixtures."""
        shutil.rmtree(self.test_dir)
    
    def test_add_entry_to_english_file(self):
        """Test adding an entry to en-US file sets status:complete."""
        resw_file = self.en_us_dir / "Resources.resw"
        
        success, message = add_entry_to_resw(resw_file, "NewKey", "New Value", is_english=True)
        
        self.assertTrue(success)
        self.assertEqual(message, "Added successfully")
        
        # Verify the entry was added with correct status
        tree = ET.parse(resw_file)
        root = tree.getroot()
        
        new_data = None
        for data in root.findall('data'):
            if data.get('name') == 'NewKey':
                new_data = data
                break
        
        self.assertIsNotNone(new_data)
        self.assertEqual(new_data.find('value').text, "New Value")
        self.assertEqual(new_data.find('comment').text, "status:complete")
    
    def test_add_entry_to_non_english_file(self):
        """Test adding an entry to non-English file sets status:incomplete."""
        resw_file = self.fr_fr_dir / "Resources.resw"
        
        success, message = add_entry_to_resw(resw_file, "NewKey", "New Value", is_english=False)
        
        self.assertTrue(success)
        self.assertEqual(message, "Added successfully")
        
        # Verify the entry was added with correct status
        tree = ET.parse(resw_file)
        root = tree.getroot()
        
        new_data = None
        for data in root.findall('data'):
            if data.get('name') == 'NewKey':
                new_data = data
                break
        
        self.assertIsNotNone(new_data)
        self.assertEqual(new_data.find('value').text, "New Value")
        self.assertEqual(new_data.find('comment').text, "status:incomplete")
    
    def test_skip_existing_key(self):
        """Test that existing keys are skipped."""
        resw_file = self.en_us_dir / "Resources.resw"
        
        success, message = add_entry_to_resw(resw_file, "ExistingKey", "Different Value", is_english=True)
        
        self.assertFalse(success)
        self.assertIn("already exists", message)
    
    def test_get_strings_directory(self):
        """Test that get_strings_directory returns a valid path."""
        strings_dir = get_strings_directory()
        
        # The path should end with 'Strings'
        self.assertEqual(strings_dir.name, "Strings")
    
    def test_add_entry_with_special_characters(self):
        """Test adding an entry with special XML characters."""
        resw_file = self.en_us_dir / "Resources.resw"
        
        success, message = add_entry_to_resw(
            resw_file, 
            "SpecialKey", 
            "Value with <special> & \"characters\"", 
            is_english=True
        )
        
        self.assertTrue(success)
        
        # Verify the entry was added and XML is still valid
        tree = ET.parse(resw_file)
        root = tree.getroot()
        
        new_data = None
        for data in root.findall('data'):
            if data.get('name') == 'SpecialKey':
                new_data = data
                break
        
        self.assertIsNotNone(new_data)
        self.assertEqual(new_data.find('value').text, 'Value with <special> & "characters"')
    
    def test_add_multiple_entries(self):
        """Test adding multiple entries to the same file."""
        resw_file = self.en_us_dir / "Resources.resw"
        
        # Add first entry
        success1, _ = add_entry_to_resw(resw_file, "Key1", "Value1", is_english=True)
        # Add second entry
        success2, _ = add_entry_to_resw(resw_file, "Key2", "Value2", is_english=True)
        
        self.assertTrue(success1)
        self.assertTrue(success2)
        
        # Verify both entries exist
        tree = ET.parse(resw_file)
        root = tree.getroot()
        
        keys_found = set()
        for data in root.findall('data'):
            keys_found.add(data.get('name'))
        
        self.assertIn("Key1", keys_found)
        self.assertIn("Key2", keys_found)
        self.assertIn("ExistingKey", keys_found)


class TestAddLocalizationEntryIntegration(unittest.TestCase):
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
    
    def test_existing_stop_button_entry(self):
        """Verify that the StopButton entry exists (added by previous script run)."""
        strings_dir = get_strings_directory()
        
        if not strings_dir.exists():
            self.skipTest("Strings directory not found")
        
        en_us_resw = strings_dir / "en-US" / "Resources.resw"
        if not en_us_resw.exists():
            self.skipTest("en-US/Resources.resw not found")
        
        tree = ET.parse(en_us_resw)
        root = tree.getroot()
        
        stop_button_found = False
        for data in root.findall('data'):
            if data.get('name') == 'StopButton':
                stop_button_found = True
                # Verify it has the correct value
                value = data.find('value')
                self.assertIsNotNone(value)
                self.assertEqual(value.text, "Stop")
                
                # Verify it has status:complete for en-US
                comment = data.find('comment')
                self.assertIsNotNone(comment)
                self.assertEqual(comment.text, "status:complete")
                break
        
        self.assertTrue(stop_button_found, "StopButton entry should exist in en-US/Resources.resw")


if __name__ == '__main__':
    unittest.main(verbosity=2)

