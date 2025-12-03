using System;
using System.Collections.Generic;
using System.IO;
using Grex.Models;

namespace Grex.Tests
{
    /// <summary>
    /// Helper class for creating test data for unit and integration tests
    /// </summary>
    public static class TestDataHelper
    {
        /// <summary>
        /// Creates a temporary test directory
        /// </summary>
        /// <returns>Path to the created test directory</returns>
        public static string CreateTestDirectory()
        {
            var testDir = Path.Combine(Path.GetTempPath(), "grex_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            return testDir;
        }

        /// <summary>
        /// Creates a test file with the specified content
        /// </summary>
        /// <param name="directory">Directory to create the file in</param>
        /// <param name="fileName">Name of the file to create</param>
        /// <param name="content">Content to write to the file</param>
        /// <returns>Full path to the created file</returns>
        public static string CreateTestFile(string directory, string fileName, string content)
        {
            var filePath = Path.Combine(directory, fileName);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        /// <summary>
        /// Creates sample search results for testing
        /// </summary>
        /// <returns>List of sample search results</returns>
        public static List<SearchResult> CreateSampleSearchResults()
        {
            return new List<SearchResult>
            {
                new SearchResult
                {
                    FileName = "file1.txt",
                    LineNumber = 1,
                    ColumnNumber = 1,
                    LineContent = "This is a test file with some content",
                    FullPath = "C:\\Test\\file1.txt",
                    RelativePath = "file1.txt"
                },
                new SearchResult
                {
                    FileName = "file2.txt",
                    LineNumber = 3,
                    ColumnNumber = 5,
                    LineContent = "Another test line with matching content",
                    FullPath = "C:\\Test\\file2.txt",
                    RelativePath = "file2.txt"
                },
                new SearchResult
                {
                    FileName = "subdir\\file3.txt",
                    LineNumber = 2,
                    ColumnNumber = 10,
                    LineContent = "Test content in a subdirectory file",
                    FullPath = "C:\\Test\\subdir\\file3.txt",
                    RelativePath = "subdir\\file3.txt"
                }
            };
        }

        /// <summary>
        /// Creates sample file search results for testing
        /// </summary>
        /// <returns>List of sample file search results</returns>
        public static List<FileSearchResult> CreateSampleFileSearchResults()
        {
            return new List<FileSearchResult>
            {
                new FileSearchResult
                {
                    FileName = "file1.txt",
                    Size = 1024,
                    MatchCount = 3,
                    FullPath = "C:\\Test\\file1.txt",
                    RelativePath = "file1.txt",
                    Extension = "txt",
                    Encoding = "UTF-8",
                    DateModified = DateTime.Now.AddDays(-1)
                },
                new FileSearchResult
                {
                    FileName = "file2.txt",
                    Size = 2048,
                    MatchCount = 1,
                    FullPath = "C:\\Test\\file2.txt",
                    RelativePath = "file2.txt",
                    Extension = "txt",
                    Encoding = "UTF-8",
                    DateModified = DateTime.Now.AddDays(-2)
                },
                new FileSearchResult
                {
                    FileName = "file3.txt",
                    Size = 512,
                    MatchCount = 2,
                    FullPath = "C:\\Test\\subdir\\file3.txt",
                    RelativePath = "subdir\\file3.txt",
                    Extension = "txt",
                    Encoding = "UTF-8",
                    DateModified = DateTime.Now.AddDays(-3)
                }
            };
        }

        /// <summary>
        /// Creates a sample directory structure with test files
        /// </summary>
        /// <param name="rootDirectory">Root directory to create the structure in</param>
        /// <returns>Dictionary of file paths and their contents</returns>
        public static Dictionary<string, string> CreateSampleFiles(string rootDirectory)
        {
            var files = new Dictionary<string, string>();

            // Create root level files
            files["root.txt"] = CreateTestFile(rootDirectory, "root.txt", "Root level test content");
            files["config.json"] = CreateTestFile(rootDirectory, "config.json", "{ \"test\": \"value\" }");

            // Create subdirectory
            var subDir = Directory.CreateDirectory(Path.Combine(rootDirectory, "subdir"));
            files["subdir\\nested.txt"] = CreateTestFile(subDir.FullName, "nested.txt", "Nested test content");

            // Create another subdirectory
            var subDir2 = Directory.CreateDirectory(Path.Combine(rootDirectory, "src"));
            files["src\\code.cs"] = CreateTestFile(subDir2.FullName, "code.cs", "public class Test { }");

            // Create deeper nested structure
            var deepDir = Directory.CreateDirectory(Path.Combine(rootDirectory, "deep", "nested", "structure"));
            files["deep\\nested\\structure\\deep.txt"] = CreateTestFile(deepDir.FullName, "deep.txt", "Deep nested content");

            return files;
        }

        /// <summary>
        /// Creates multiple test files with specified content
        /// </summary>
        /// <param name="directory">Directory to create files in</param>
        /// <param name="count">Number of files to create</param>
        /// <returns>List of created file paths</returns>
        public static List<string> CreateMultipleTestFiles(string directory, int count)
        {
            var files = new List<string>();
            
            for (int i = 0; i < count; i++)
            {
                var fileName = $"testfile_{i:D3}.txt";
                var content = $"Test content for file {i}\nLine 2 of file {i}\nSearch term: test";
                var filePath = CreateTestFile(directory, fileName, content);
                files.Add(filePath);
            }

            return files;
        }

        /// <summary>
        /// Creates test files with different extensions for testing file filtering
        /// </summary>
        /// <param name="directory">Directory to create files in</param>
        /// <returns>Dictionary of file paths and their extensions</returns>
        public static Dictionary<string, string> CreateFilesWithDifferentExtensions(string directory)
        {
            var files = new Dictionary<string, string>();

            files["test.txt"] = CreateTestFile(directory, "test.txt", "Text file content");
            files["test.cs"] = CreateTestFile(directory, "test.cs", "C# code content");
            files["test.js"] = CreateTestFile(directory, "test.js", "JavaScript content");
            files["test.json"] = CreateTestFile(directory, "test.json", "{ \"key\": \"value\" }");
            files["test.xml"] = CreateTestFile(directory, "test.xml", "<root><element>value</element></root>");
            files["test.exe"] = CreateTestFile(directory, "test.exe", "Binary content simulation");
            files["test.dll"] = CreateTestFile(directory, "test.dll", "Binary content simulation");
            files["test.log"] = CreateTestFile(directory, "test.log", "Log file content");

            return files;
        }

        /// <summary>
        /// Creates a .gitignore file with specified patterns
        /// </summary>
        /// <param name="directory">Directory to create the .gitignore in</param>
        /// <param name="patterns">Patterns to include in the .gitignore file</param>
        /// <returns>Path to the created .gitignore file</returns>
        public static string CreateGitIgnoreFile(string directory, params string[] patterns)
        {
            var gitIgnorePath = Path.Combine(directory, ".gitignore");
            var content = string.Join(Environment.NewLine, patterns);
            File.WriteAllText(gitIgnorePath, content);
            return gitIgnorePath;
        }

        /// <summary>
        /// Creates hidden files for testing hidden file functionality
        /// </summary>
        /// <param name="directory">Directory to create hidden files in</param>
        /// <returns>List of created hidden file paths</returns>
        public static List<string> CreateHiddenFiles(string directory)
        {
            var hiddenFiles = new List<string>();

            var hiddenFile1 = CreateTestFile(directory, ".hidden.txt", "Hidden file content");
            var hiddenFile2 = CreateTestFile(directory, ".config", "Configuration content");
            var hiddenFile3 = CreateTestFile(directory, "temp.tmp", "Temporary file content");

            // Set files as hidden
            File.SetAttributes(hiddenFile1, File.GetAttributes(hiddenFile1) | FileAttributes.Hidden);
            File.SetAttributes(hiddenFile2, File.GetAttributes(hiddenFile2) | FileAttributes.Hidden);
            File.SetAttributes(hiddenFile3, File.GetAttributes(hiddenFile3) | FileAttributes.Hidden);

            hiddenFiles.Add(hiddenFile1);
            hiddenFiles.Add(hiddenFile2);
            hiddenFiles.Add(hiddenFile3);

            return hiddenFiles;
        }

        /// <summary>
        /// Creates system files for testing system file functionality
        /// </summary>
        /// <param name="directory">Directory to create system files in</param>
        /// <returns>List of created system file paths</returns>
        public static List<string> CreateSystemFiles(string directory)
        {
            var systemFiles = new List<string>();

            var systemFile1 = CreateTestFile(directory, "system.sys", "System file content");
            var systemFile2 = CreateTestFile(directory, "pagefile.sys", "Page file content");

            // Set files as system
            File.SetAttributes(systemFile1, File.GetAttributes(systemFile1) | FileAttributes.System);
            File.SetAttributes(systemFile2, File.GetAttributes(systemFile2) | FileAttributes.System);

            systemFiles.Add(systemFile1);
            systemFiles.Add(systemFile2);

            return systemFiles;
        }

        /// <summary>
        /// Creates symbolic links for testing symbolic link functionality (if supported)
        /// </summary>
        /// <param name="directory">Directory to create symbolic links in</param>
        /// <param name="targetPath">Target path for the symbolic link</param>
        /// <returns>Path to the created symbolic link, or null if not supported</returns>
        public static string? CreateSymbolicLink(string directory, string targetPath)
        {
            try
            {
                var linkPath = Path.Combine(directory, "symlink.txt");
                
                // Create target file first
                CreateTestFile(directory, "target.txt", "Target file content");
                
                // Create symbolic link
                File.CreateSymbolicLink(linkPath, Path.Combine(directory, "target.txt"));
                
                return linkPath;
            }
            catch
            {
                // Symbolic links might not be supported on all systems
                return null;
            }
        }

        /// <summary>
        /// Cleans up a test directory and all its contents
        /// </summary>
        /// <param name="directory">Directory to clean up</param>
        public static void CleanupTestDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    // Remove read-only and hidden attributes before deletion
                    var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var attributes = File.GetAttributes(file);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly ||
                            (attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        {
                            File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly & ~FileAttributes.Hidden);
                        }
                    }

                    Directory.Delete(directory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Creates a directory structure with various file types for comprehensive testing
        /// </summary>
        /// <param name="rootDirectory">Root directory to create the structure in</param>
        /// <returns>Complex test structure information</returns>
        public static TestStructure CreateComplexTestStructure(string rootDirectory)
        {
            var structure = new TestStructure { RootDirectory = rootDirectory };

            // Create various file types
            structure.Files = CreateFilesWithDifferentExtensions(rootDirectory);

            // Create subdirectories with files
            structure.SourceDir = Directory.CreateDirectory(Path.Combine(rootDirectory, "src"));
            structure.TestDir = Directory.CreateDirectory(Path.Combine(rootDirectory, "tests"));
            structure.BuildDir = Directory.CreateDirectory(Path.Combine(rootDirectory, "build"));

            // Add files to source directory
            structure.Files["src\\program.cs"] = CreateTestFile(structure.SourceDir.FullName, "program.cs", 
                "using System;\n\nclass Program { static void Main() { Console.WriteLine(\"Hello\"); } }");
            structure.Files["src\\utils.cs"] = CreateTestFile(structure.SourceDir.FullName, "utils.cs", 
                "public static class Utils { public static string Test() => \"test\"; }");

            // Add files to test directory
            structure.Files["tests\\program.test.cs"] = CreateTestFile(structure.TestDir.FullName, "program.test.cs", 
                "using Xunit;\n\npublic class ProgramTests { [Fact] public void Test() { Assert.True(true); } }");

            // Add files to build directory
            structure.Files["build\\output.exe"] = CreateTestFile(structure.BuildDir.FullName, "output.exe", "Binary content");

            // Create .gitignore
            structure.GitIgnoreFile = CreateGitIgnoreFile(rootDirectory, 
                "*.exe", "build/", "*.obj", "*.pdb", "bin/", "obj/");

            // Create hidden files
            structure.HiddenFiles = CreateHiddenFiles(rootDirectory);

            return structure;
        }

        /// <summary>
        /// Represents a complex test structure for testing
        /// </summary>
        public class TestStructure
        {
            public string RootDirectory { get; set; } = string.Empty;
            public Dictionary<string, string> Files { get; set; } = new();
            public DirectoryInfo SourceDir { get; set; } = null!;
            public DirectoryInfo TestDir { get; set; } = null!;
            public DirectoryInfo BuildDir { get; set; } = null!;
            public string GitIgnoreFile { get; set; } = string.Empty;
            public List<string> HiddenFiles { get; set; } = new();
        }
    }
}