#!/usr/bin/env python3
"""
Version Update Script for Grex

Updates the version number across all relevant project files.

Usage:
    python update_version.py <new_version>
    
Example:
    python update_version.py 1.2
    python update_version.py 2.0
"""

import re
import sys
from pathlib import Path


def get_project_root() -> Path:
    """Get the project root directory (parent of Scripts folder)."""
    return Path(__file__).parent.parent


def read_file_binary(file_path: Path) -> bytes:
    """Read file content as binary to preserve exact byte content."""
    with open(file_path, "rb") as f:
        return f.read()


def write_file_binary(file_path: Path, content: bytes) -> None:
    """Write file content as binary to preserve exact byte content."""
    with open(file_path, "wb") as f:
        f.write(content)


def regex_replace_binary(content: bytes, pattern: str, replacement: str) -> tuple[bytes, int]:
    """Perform regex replacement on binary content, preserving line endings."""
    # Decode for regex, but track positions
    text = content.decode("utf-8")
    new_text, count = re.subn(pattern, replacement, text)
    return new_text.encode("utf-8"), count


def update_about_view(project_root: Path, new_version: str) -> bool:
    """Update Controls/AboutView.xaml.cs with the new version."""
    file_path = project_root / "Controls" / "AboutView.xaml.cs"
    
    if not file_path.exists():
        print(f"ERROR: File not found: {file_path}")
        return False
    
    content = read_file_binary(file_path)
    
    # Pattern to match: VersionTextBlock.Text = "Version X.X";
    pattern = r'VersionTextBlock\.Text = "Version [0-9]+\.[0-9]+";'
    replacement = f'VersionTextBlock.Text = "Version {new_version}";'
    
    new_content, count = regex_replace_binary(content, pattern, replacement)
    
    if count == 0:
        print(f"WARNING: No version pattern found in {file_path}")
        return False
    
    write_file_binary(file_path, new_content)
    print(f"Updated {file_path} ({count} replacement(s))")
    return True


def update_package_manifest(project_root: Path, new_version: str) -> bool:
    """Update Package.appxmanifest with the new version."""
    file_path = project_root / "Package.appxmanifest"
    
    if not file_path.exists():
        print(f"ERROR: File not found: {file_path}")
        return False
    
    content = read_file_binary(file_path)
    
    # Pattern to match: Version="X.X.0.0"
    pattern = r'Version="[0-9]+\.[0-9]+\.0\.0"'
    replacement = f'Version="{new_version}.0.0"'
    
    new_content, count = regex_replace_binary(content, pattern, replacement)
    
    if count == 0:
        print(f"WARNING: No version pattern found in {file_path}")
        return False
    
    write_file_binary(file_path, new_content)
    print(f"Updated {file_path} ({count} replacement(s))")
    return True


def update_assembly_info(project_root: Path, new_version: str) -> bool:
    """Update Properties/AssemblyInfo.cs with the new version."""
    file_path = project_root / "Properties" / "AssemblyInfo.cs"
    
    if not file_path.exists():
        print(f"ERROR: File not found: {file_path}")
        return False
    
    content = read_file_binary(file_path)
    text = content.decode("utf-8")
    
    # Pattern for AssemblyVersion and AssemblyFileVersion: X.X.0.0
    pattern_version = r'\[assembly: AssemblyVersion\("[0-9]+\.[0-9]+\.0\.0"\)\]'
    replacement_version = f'[assembly: AssemblyVersion("{new_version}.0.0")]'
    
    pattern_file_version = r'\[assembly: AssemblyFileVersion\("[0-9]+\.[0-9]+\.0\.0"\)\]'
    replacement_file_version = f'[assembly: AssemblyFileVersion("{new_version}.0.0")]'
    
    # Pattern for AssemblyInformationalVersion: X.X
    pattern_info_version = r'\[assembly: AssemblyInformationalVersion\("[0-9]+\.[0-9]+"\)\]'
    replacement_info_version = f'[assembly: AssemblyInformationalVersion("{new_version}")]'
    
    new_text = text
    total_count = 0
    
    new_text, count = re.subn(pattern_version, replacement_version, new_text)
    total_count += count
    
    new_text, count = re.subn(pattern_file_version, replacement_file_version, new_text)
    total_count += count
    
    new_text, count = re.subn(pattern_info_version, replacement_info_version, new_text)
    total_count += count
    
    if total_count == 0:
        print(f"WARNING: No version patterns found in {file_path}")
        return False
    
    write_file_binary(file_path, new_text.encode("utf-8"))
    print(f"Updated {file_path} ({total_count} replacement(s))")
    return True


def update_app_manifest(project_root: Path, new_version: str) -> bool:
    """Update app.manifest with the new version."""
    file_path = project_root / "app.manifest"
    
    if not file_path.exists():
        print(f"ERROR: File not found: {file_path}")
        return False
    
    content = read_file_binary(file_path)
    
    # Pattern to match: <assemblyIdentity version="X.X.0.0"
    pattern = r'<assemblyIdentity version="[0-9]+\.[0-9]+\.0\.0"'
    replacement = f'<assemblyIdentity version="{new_version}.0.0"'
    
    new_content, count = regex_replace_binary(content, pattern, replacement)
    
    if count == 0:
        print(f"WARNING: No version pattern found in {file_path}")
        return False
    
    write_file_binary(file_path, new_content)
    print(f"Updated {file_path} ({count} replacement(s))")
    return True


def validate_version(version: str) -> bool:
    """Validate that the version string is in the correct format (e.g., 1.1, 2.0)."""
    pattern = r'^[0-9]+\.[0-9]+$'
    return bool(re.match(pattern, version))


def main():
    if len(sys.argv) != 2:
        print("Usage: python update_version.py <new_version>")
        print("Example: python update_version.py 1.2")
        sys.exit(1)
    
    new_version = sys.argv[1]
    
    if not validate_version(new_version):
        print(f"ERROR: Invalid version format '{new_version}'")
        print("Version must be in format: X.Y (e.g., 1.1, 2.0, 10.5)")
        sys.exit(1)
    
    project_root = get_project_root()
    print(f"Project root: {project_root}")
    print(f"Updating to version: {new_version}")
    print("-" * 50)
    
    results = []
    results.append(("AboutView.xaml.cs", update_about_view(project_root, new_version)))
    results.append(("Package.appxmanifest", update_package_manifest(project_root, new_version)))
    results.append(("AssemblyInfo.cs", update_assembly_info(project_root, new_version)))
    results.append(("app.manifest", update_app_manifest(project_root, new_version)))
    
    print("-" * 50)
    
    success_count = sum(1 for _, success in results if success)
    total_count = len(results)
    
    if success_count == total_count:
        print(f"SUCCESS: All {total_count} files updated to version {new_version}")
        print(f"Run 'git add .' and 'git commit -m \"Update version to {new_version}\"' to commit the changes.")
        print(f"Run 'git push' to push the changes to the remote repository.")
        print(f"Run 'git tag v{new_version}' to create a new tag for the version.")
        print(f"Run 'git push origin v{new_version}' to push the tag to the remote repository.")
        print(f"Run 'git push origin --tags' to push all tags to the remote repository.")
        sys.exit(0)
    else:
        print(f"PARTIAL: {success_count}/{total_count} files updated")
        for name, success in results:
            status = "OK" if success else "FAILED"
            print(f"  {status}: {name}")
        sys.exit(1)


if __name__ == "__main__":
    main()

