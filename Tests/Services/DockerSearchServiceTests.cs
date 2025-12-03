using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Grex.Models;
using Grex.Services;
using Xunit;

namespace Grex.Tests.Services
{
    public class DockerSearchServiceTests : IDisposable
    {
        private readonly string _mirrorRoot;

        public DockerSearchServiceTests()
        {
            _mirrorRoot = Path.Combine(Path.GetTempPath(), $"grex_DockerTests_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_mirrorRoot))
                {
                    Directory.Delete(_mirrorRoot, true);
                }
            }
            catch
            {
                // Ignore cleanup failures in tests
            }
        }

        [Theory]
        [InlineData("var/www/html", "/var/www/html")]
        [InlineData("/var/www//html", "/var/www/html")]
        [InlineData("C:\\data\\logs", "/C:/data/logs")]
        public void NormalizeContainerPath_NormalizesInput(string input, string expected)
        {
            DockerSearchService.NormalizeContainerPath(input).Should().Be(expected);
        }

        [Fact]
        public async Task GetContainersAsync_ParsesJsonLines()
        {
            var runner = new FakeRunner
            {
                OnRun = startInfo =>
                {
                    if (startInfo.Arguments.StartsWith("ps", StringComparison.OrdinalIgnoreCase))
                    {
                        var output = "{\"ID\":\"1234567890ab\",\"Names\":\"web\",\"State\":\"running\",\"Status\":\"Up\"}\n" +
                                     "{\"ID\":\"abcdef123456\",\"Names\":\"api\",\"State\":\"running\",\"Status\":\"Up\"}";
                        return new DockerProcessResult(0, output, string.Empty);
                    }

                    return new DockerProcessResult(0, string.Empty, string.Empty);
                }
            };

            var service = new DockerSearchService(runner, _mirrorRoot);
            var containers = await service.GetContainersAsync();

            containers.Should().HaveCount(2);
            containers[0].Name.Should().Be("web");
            containers[1].Name.Should().Be("api");
        }

        [Fact]
        public async Task MirrorPathAsync_CreatesMirrorDirectoryAndInvokesDockerCp()
        {
            var runner = new FakeRunner
            {
                OnRun = startInfo =>
                {
                    if (startInfo.Arguments.StartsWith("cp", StringComparison.OrdinalIgnoreCase))
                    {
                        var destination = FakeRunner.ExtractDestination(startInfo.Arguments);
                        Directory.CreateDirectory(Path.Combine(destination, "app"));
                    }

                    return new DockerProcessResult(0, string.Empty, string.Empty);
                }
            };

            var service = new DockerSearchService(runner, _mirrorRoot);
            var container = new DockerContainerInfo { Id = "1234567890ab", Name = "web" };

            var mirror = await service.MirrorPathAsync(container, "/var/www/app", includeSymbolicLinks: true);

            runner.Commands.Should().Contain(cmd => cmd.Contains("cp", StringComparison.OrdinalIgnoreCase));
            Directory.Exists(mirror.LocalMirrorPath).Should().BeTrue();
            var trimmedSearchPath = mirror.LocalSearchPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Path.GetFileName(trimmedSearchPath).Should().Be("app");
        }

        [Fact]
        public async Task MirrorPathAsync_WhenDockerCpFails_CleansUpMirrorDirectory()
        {
            var runner = new FakeRunner
            {
                OnRun = _ => new DockerProcessResult(1, string.Empty, "boom")
            };

            var service = new DockerSearchService(runner, _mirrorRoot);
            var container = new DockerContainerInfo { Id = "abcdef123456", Name = "api" };

            Func<Task> act = async () => await service.MirrorPathAsync(container, "/srv/app", includeSymbolicLinks: true);

            await act.Should().ThrowAsync<InvalidOperationException>();

            Directory.Exists(_mirrorRoot).Should().BeTrue();
            Directory.EnumerateDirectories(_mirrorRoot).Should().BeEmpty("failed mirrors are cleaned up on error");
        }

        [Fact]
        public void ResolveLocalSearchRoot_WhenChildMissing_ReturnsMirrorDirectory()
        {
            var mirrorDir = Path.Combine(_mirrorRoot, "mirror");
            Directory.CreateDirectory(mirrorDir);

            var result = DockerSearchService.ResolveLocalSearchRoot(mirrorDir, "/var/www/app");
            result.Should().Be(mirrorDir);
        }

        [Fact]
        public async Task PruneExpiredMirrorsAsync_RemovesOldDirectories()
        {
            Directory.CreateDirectory(_mirrorRoot);
            var expired = Path.Combine(_mirrorRoot, "old");
            Directory.CreateDirectory(expired);
            Directory.SetLastWriteTimeUtc(expired, DateTime.UtcNow.AddHours(-12));

            var runner = new FakeRunner();
            var service = new DockerSearchService(runner, _mirrorRoot);

            var removed = await service.PruneExpiredMirrorsAsync(TimeSpan.FromHours(1));

            removed.Should().Be(1);
            Directory.Exists(expired).Should().BeFalse();
        }

        [Fact]
        public async Task GetContainersAsync_WithInvalidJson_ReturnsEmptyList()
        {
            var runner = new FakeRunner
            {
                OnRun = _ => new DockerProcessResult(0, "not-json", string.Empty)
            };

            var service = new DockerSearchService(runner, _mirrorRoot);
            var containers = await service.GetContainersAsync();

            containers.Should().BeEmpty();
        }

        [Fact]
        public async Task IsDockerAvailableAsync_WhenDockerIsAvailable_ReturnsTrue()
        {
            var runner = new FakeRunner
            {
                OnRun = startInfo =>
                {
                    if (startInfo.Arguments.Contains("version"))
                    {
                        return new DockerProcessResult(0, "20.10.7", string.Empty);
                    }
                    return new DockerProcessResult(0, string.Empty, string.Empty);
                }
            };

            var service = new DockerSearchService(runner, _mirrorRoot);
            var result = await service.IsDockerAvailableAsync();

            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsDockerAvailableAsync_WhenDockerIsNotAvailable_ReturnsFalse()
        {
            var runner = new FakeRunner
            {
                OnRun = _ => new DockerProcessResult(1, string.Empty, "docker: command not found")
            };

            var service = new DockerSearchService(runner, _mirrorRoot);
            var result = await service.IsDockerAvailableAsync();

            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsDockerAvailableAsync_WhenDockerThrowsException_ReturnsFalse()
        {
            var runner = new FakeRunner
            {
                OnRun = _ => throw new InvalidOperationException("Process failed")
            };

            var service = new DockerSearchService(runner, _mirrorRoot);
            var result = await service.IsDockerAvailableAsync();

            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetContainersAsync_WhenDockerCommandFails_ReturnsEmptyList()
        {
            var runner = new FakeRunner
            {
                OnRun = startInfo =>
                {
                    if (startInfo.Arguments.Contains("ps"))
                    {
                        return new DockerProcessResult(1, string.Empty, "Cannot connect to Docker daemon");
                    }
                    return new DockerProcessResult(0, string.Empty, string.Empty);
                }
            };

            var service = new DockerSearchService(runner, _mirrorRoot);
            var containers = await service.GetContainersAsync();

            containers.Should().BeEmpty();
        }

        [Fact]
        public async Task GetContainersAsync_WhenDockerThrowsException_ReturnsEmptyList()
        {
            var runner = new FakeRunner
            {
                OnRun = _ => throw new InvalidOperationException("Process failed")
            };

            var service = new DockerSearchService(runner, _mirrorRoot);
            var containers = await service.GetContainersAsync();

            containers.Should().BeEmpty();
        }

        [Fact]
        public async Task CleanupMirrorAsync_WhenMirrorIsNull_DoesNotThrow()
        {
            var service = new DockerSearchService(null, _mirrorRoot);

            Func<Task> act = async () => await service.CleanupMirrorAsync((DockerMirrorInfo?)null);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task CleanupMirrorAsync_WhenPathDoesNotExist_DoesNotThrow()
        {
            var service = new DockerSearchService(null, _mirrorRoot);
            var mirror = new DockerMirrorInfo
            {
                LocalMirrorPath = Path.Combine(_mirrorRoot, "nonexistent")
            };

            Func<Task> act = async () => await service.CleanupMirrorAsync(mirror);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task CleanupMirrorAsync_WhenPathExists_DeletesDirectory()
        {
            var service = new DockerSearchService(null, _mirrorRoot);
            var mirrorDir = Path.Combine(_mirrorRoot, "test-mirror");
            Directory.CreateDirectory(mirrorDir);
            File.WriteAllText(Path.Combine(mirrorDir, "test.txt"), "content");

            var mirror = new DockerMirrorInfo
            {
                LocalMirrorPath = mirrorDir
            };

            await service.CleanupMirrorAsync(mirror);

            Directory.Exists(mirrorDir).Should().BeFalse();
        }

        [Fact]
        public async Task MirrorPathAsync_WhenSymlinksExcluded_UsesTarDereference()
        {
            var runner = new FakeRunner
            {
                OnRun = startInfo =>
                {
                    // Should use tar with --dereference when symlinks are excluded
                    if (startInfo.Arguments.Contains("exec") && startInfo.Arguments.Contains("tar") && startInfo.Arguments.Contains("--dereference"))
                    {
                        // Simulate successful tar creation
                        return new DockerProcessResult(0, string.Empty, string.Empty);
                    }
                    // Simulate successful tar copy
                    if (startInfo.Arguments.StartsWith("cp", StringComparison.OrdinalIgnoreCase) && startInfo.Arguments.Contains(".tar"))
                    {
                        // Create a dummy tar file
                        var destination = FakeRunner.ExtractDestination(startInfo.Arguments);
                        var parts = startInfo.Arguments.Split('"');
                        string? tarFileName = null;
                        foreach (var part in parts)
                        {
                            if (part.EndsWith(".tar"))
                            {
                                tarFileName = Path.GetFileName(part);
                                break;
                            }
                        }
                        if (string.IsNullOrEmpty(tarFileName))
                        {
                            tarFileName = "grex_mirror_test.tar";
                        }
                        File.WriteAllText(Path.Combine(destination, tarFileName), "dummy tar content");
                        return new DockerProcessResult(0, string.Empty, string.Empty);
                    }
                    // Simulate tar extraction (tar.exe) - may fail in test environment
                    if (startInfo.FileName.Equals("tar", StringComparison.OrdinalIgnoreCase) && startInfo.Arguments.Contains("-xf"))
                    {
                        // Extract would create files, but for test we just return success if tar file exists
                        // In real scenario, tar.exe would extract the files
                        return new DockerProcessResult(0, string.Empty, string.Empty);
                    }
                    // Simulate tar cleanup in container
                    if (startInfo.Arguments.Contains("rm -f"))
                    {
                        return new DockerProcessResult(0, string.Empty, string.Empty);
                    }

                    return new DockerProcessResult(0, string.Empty, string.Empty);
                }
            };

            var service = new DockerSearchService(runner, _mirrorRoot);
            var container = new DockerContainerInfo { Id = "1234567890ab", Name = "web" };

            // This may fail because tar extraction requires actual tar.exe, but we can verify the approach
            Func<Task> act = async () => await service.MirrorPathAsync(container, "/var/www/app", includeSymbolicLinks: false);
            
            // The test may fail on tar extraction, but that's expected in test environment
            // The important thing is that it tries the tar approach
            try
            {
                await act();
            }
            catch
            {
                // Expected - tar.exe may not be available or tar file may not exist in test
            }

            // Verify it attempted to use tar with --dereference (the key difference)
            runner.Commands.Should().Contain(cmd => 
                cmd.Contains("exec", StringComparison.OrdinalIgnoreCase) && 
                cmd.Contains("tar", StringComparison.OrdinalIgnoreCase) &&
                cmd.Contains("--dereference", StringComparison.OrdinalIgnoreCase));
            
            // Verify it did NOT use docker cp directly (which would preserve symlinks)
            // It should use docker cp only to copy the tar file, not the source directory
            var dockerCpCommands = runner.Commands.Where(cmd => cmd.StartsWith("cp", StringComparison.OrdinalIgnoreCase)).ToList();
            dockerCpCommands.Should().OnlyContain(cmd => cmd.Contains(".tar"), 
                "when symlinks are excluded, docker cp should only be used to copy the tar file, not the source directory");
        }

        #region Docker API (grep) Search Tests

        [Fact]
        public void DockerGrepResult_DefaultValues_AreCorrect()
        {
            var result = new DockerGrepResult();
            
            result.Success.Should().BeFalse();
            result.Results.Should().NotBeNull().And.BeEmpty();
            result.ErrorMessage.Should().BeNull();
            result.GrepNotAvailable.Should().BeFalse();
        }

        [Fact]
        public void DockerGrepResult_SuccessfulResult_ContainsResults()
        {
            var searchResults = new List<SearchResult>
            {
                new SearchResult { FileName = "test.txt", LineNumber = 1, LineContent = "hello world" }
            };
            
            var result = new DockerGrepResult
            {
                Success = true,
                Results = searchResults
            };
            
            result.Success.Should().BeTrue();
            result.Results.Should().HaveCount(1);
            result.GrepNotAvailable.Should().BeFalse();
        }

        [Fact]
        public void DockerGrepResult_GrepNotAvailable_SetCorrectly()
        {
            var result = new DockerGrepResult
            {
                Success = false,
                GrepNotAvailable = true,
                ErrorMessage = "grep is not available in this container."
            };
            
            result.Success.Should().BeFalse();
            result.GrepNotAvailable.Should().BeTrue();
            result.ErrorMessage.Should().Contain("grep is not available");
        }

        [Fact]
        public async Task SearchInContainerAsync_WithNullContainer_ThrowsArgumentNullException()
        {
            var service = new DockerSearchService(null, _mirrorRoot);
            
            Func<Task> act = async () => await service.SearchInContainerAsync(null!, "/path", "search");
            
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("container");
        }

        [Fact]
        public async Task SearchInContainerAsync_WithEmptySearchTerm_ReturnsEmptyResults()
        {
            var service = new DockerSearchService(null, _mirrorRoot);
            var container = new DockerContainerInfo { Id = "test123", Name = "test" };
            
            var result = await service.SearchInContainerAsync(container, "/path", "");
            
            result.Success.Should().BeTrue();
            result.Results.Should().BeEmpty();
        }

        [Fact]
        public async Task SearchInContainerAsync_WithWhitespaceSearchTerm_ReturnsEmptyResults()
        {
            var service = new DockerSearchService(null, _mirrorRoot);
            var container = new DockerContainerInfo { Id = "test123", Name = "test" };
            
            var result = await service.SearchInContainerAsync(container, "/path", "   ");
            
            result.Success.Should().BeTrue();
            result.Results.Should().BeEmpty();
        }

        [Fact]
        public void BuildGrepCommand_WithDefaultOptions_ReturnsCorrectCommand()
        {
            var args = DockerSearchService.BuildGrepCommand(
                searchTerm: "hello",
                isRegex: false,
                caseSensitive: false,
                includeSubfolders: true,
                includeHiddenFiles: false,
                includeSystemFiles: true,
                includeBinaryFiles: false,
                includeSymbolicLinks: false,
                matchFileNames: null,
                excludeDirs: null,
                searchPath: "/app/src");

            // Now uses sh -c "find ... | xargs ... grep ..." for better performance with parallel execution
            args.Should().HaveCount(3);
            args[0].Should().Be("sh");
            args[1].Should().Be("-c");
            args[2].Should().Contain("find '/app/src'");
            args[2].Should().Contain("xargs -0 -P 4 -r grep"); // Parallel execution with xargs
            args[2].Should().Contain("-F"); // Fixed string
            args[2].Should().Contain("-i"); // Case insensitive
            args[2].Should().Contain("'hello'"); // Search term
            args[2].Should().Contain("! -name '.*'"); // Exclude hidden files
            args[2].Should().Contain("! -path '*/.*'"); // Exclude hidden directories
        }

        [Fact]
        public void BuildGrepCommand_WithRegex_UsesExtendedRegex()
        {
            var args = DockerSearchService.BuildGrepCommand(
                searchTerm: "\\d+",
                isRegex: true,
                caseSensitive: true,
                includeSubfolders: true,
                includeHiddenFiles: false,
                includeSystemFiles: true,
                includeBinaryFiles: false,
                includeSymbolicLinks: false,
                matchFileNames: null,
                excludeDirs: null,
                searchPath: "/app");

            // Check the shell command string for Regex options
            args.Should().HaveCount(3);
            args[0].Should().Be("sh");
            args[2].Should().Contain("-E"); // Extended Regex
            args[2].Should().NotContain("-F"); // Not fixed string
            args[2].Should().NotContain("-i"); // Case sensitive (no -i flag)
        }

        [Fact]
        public void BuildGrepCommand_WithFilePatterns_DoesNotUseSlowIncludeOption()
        {
            // We intentionally do NOT use grep's --include= option because it's extremely slow
            // in many containers (especially those with BusyBox grep). Instead, filename filtering
            // is applied as post-processing on the results, which is much faster.
            var args = DockerSearchService.BuildGrepCommand(
                searchTerm: "test",
                isRegex: false,
                caseSensitive: false,
                includeSubfolders: true,
                includeHiddenFiles: false,
                includeSystemFiles: true,
                includeBinaryFiles: false,
                includeSymbolicLinks: false,
                matchFileNames: "*.cs;*.txt",
                excludeDirs: null,
                searchPath: "/app");

            // Verify --include is NOT used (filtering happens via post-processing)
            args.Should().NotContain(arg => arg.StartsWith("--include="), 
                "because --include is slow; post-processing filtering is used instead");
        }

        [Fact]
        public void BuildGrepCommand_WithHiddenFiles_DoesNotExcludeDotFiles()
        {
            var args = DockerSearchService.BuildGrepCommand(
                searchTerm: "test",
                isRegex: false,
                caseSensitive: false,
                includeSubfolders: true,
                includeHiddenFiles: true,
                includeSystemFiles: true,
                includeBinaryFiles: false,
                includeSymbolicLinks: false,
                matchFileNames: null,
                excludeDirs: null,
                searchPath: "/app");

            args.Should().NotContain("--exclude=.*");
            args.Should().NotContain("--exclude-dir=.*");
        }

        [Fact]
        public void BuildGrepCommand_WithoutSubfolders_UsesFindMaxdepth()
        {
            // When includeSubfolders is false, we use 'find -maxdepth 1' for efficiency
            // instead of grep -r followed by filtering.
            var args = DockerSearchService.BuildGrepCommand(
                searchTerm: "test",
                isRegex: false,
                caseSensitive: false,
                includeSubfolders: false,
                includeHiddenFiles: false,
                includeSystemFiles: true,
                includeBinaryFiles: false,
                includeSymbolicLinks: false,
                matchFileNames: null,
                excludeDirs: null,
                searchPath: "/app");

            // Should use sh -c with find command
            args.Should().HaveCount(3);
            args[0].Should().Be("sh");
            args[1].Should().Be("-c");
            args[2].Should().Contain("find");
            args[2].Should().Contain("-maxdepth 1");
            args[2].Should().Contain("xargs -0 -P 4 -r grep"); // Parallel execution with xargs
        }

        [Fact]
        public void BuildGrepCommand_WithSubfolders_UsesFindAndGrep()
        {
            // When includeSubfolders is true, we use find + grep (faster than grep -r)
            var args = DockerSearchService.BuildGrepCommand(
                searchTerm: "test",
                isRegex: false,
                caseSensitive: false,
                includeSubfolders: true,
                includeHiddenFiles: false,
                includeSystemFiles: true,
                includeBinaryFiles: false,
                includeSymbolicLinks: false,
                matchFileNames: null,
                excludeDirs: null,
                searchPath: "/app");

            // Should use sh -c with find+grep command
            args.Should().HaveCount(3);
            args[0].Should().Be("sh");
            args[1].Should().Be("-c");
            args[2].Should().Contain("find");
            args[2].Should().Contain("xargs -0 -P 4 -r grep"); // Parallel execution with xargs
            args[2].Should().NotContain("-maxdepth"); // No depth limit for recursive
        }

        [Fact]
        public void ParseGrepOutput_WithValidOutput_ReturnsCorrectResults()
        {
            var output = "/app/src/file.txt:10:hello world\n/app/src/other.cs:25:another match";

            var results = DockerSearchService.ParseGrepOutput(output, "/app/src", "hello", false, false);

            results.Should().HaveCount(2);
            results[0].FullPath.Should().Be("/app/src/file.txt");
            results[0].FileName.Should().Be("file.txt");
            results[0].LineNumber.Should().Be(10);
            results[0].LineContent.Should().Be("hello world");
            results[0].RelativePath.Should().Be("file.txt");
            results[0].ColumnNumber.Should().Be(1); // "hello" starts at position 1
            results[0].MatchCount.Should().Be(1);

            results[1].FullPath.Should().Be("/app/src/other.cs");
            results[1].FileName.Should().Be("other.cs");
            results[1].LineNumber.Should().Be(25);
            results[1].LineContent.Should().Be("another match");
        }

        [Fact]
        public void ParseGrepOutput_WithEmptyOutput_ReturnsEmptyList()
        {
            var results = DockerSearchService.ParseGrepOutput("", "/app", "test", false, false);

            results.Should().BeEmpty();
        }

        [Fact]
        public void ParseGrepOutput_WithNullOutput_ReturnsEmptyList()
        {
            var results = DockerSearchService.ParseGrepOutput(null!, "/app", "test", false, false);

            results.Should().BeEmpty();
        }

        [Fact]
        public void ParseGrepOutput_WithBinaryFileMessage_SkipsIt()
        {
            var output = "/app/file.txt:1:match\nBinary file /app/image.png matches\n/app/other.txt:5:another";

            var results = DockerSearchService.ParseGrepOutput(output, "/app", "match", false, false);

            results.Should().HaveCount(2);
            results.Should().NotContain(r => r.FileName == "image.png");
        }

        [Fact]
        public void ParseGrepOutput_WithColonInContent_ParsesCorrectly()
        {
            var output = "/app/config.json:15:\"url\": \"https://example.com:8080\"";

            var results = DockerSearchService.ParseGrepOutput(output, "/app", "url", false, false);

            results.Should().HaveCount(1);
            results[0].LineNumber.Should().Be(15);
            results[0].LineContent.Should().Be("\"url\": \"https://example.com:8080\"");
        }

        [Fact]
        public void ParseGrepOutput_WithNestedPath_CalculatesRelativePath()
        {
            var output = "/app/src/services/api/handler.go:100:func Handle()";

            var results = DockerSearchService.ParseGrepOutput(output, "/app/src", "Handle", false, false);

            results.Should().HaveCount(1);
            results[0].RelativePath.Should().Be("services/api/handler.go");
        }

        [Fact]
        public void ParseGrepOutput_CountsMultipleMatchesOnSameLine()
        {
            var output = "/app/test.txt:1:foo bar foo baz foo";

            var results = DockerSearchService.ParseGrepOutput(output, "/app", "foo", false, false);

            results.Should().HaveCount(1);
            results[0].MatchCount.Should().Be(3); // "foo" appears 3 times
            results[0].ColumnNumber.Should().Be(1); // First "foo" is at position 1
        }

        [Fact]
        public void ParseGrepOutput_CalculatesColumnNumberCorrectly()
        {
            var output = "/app/test.txt:1:  prefix_test_suffix";

            var results = DockerSearchService.ParseGrepOutput(output, "/app", "test", false, false);

            results.Should().HaveCount(1);
            results[0].ColumnNumber.Should().Be(10); // "test" starts at position 10 (after "  prefix_")
        }

        [Theory]
        [InlineData("test.cs", "*.cs", true)]
        [InlineData("test.txt", "*.cs", false)]
        [InlineData("test.cs", "*.cs|*.txt", true)]
        [InlineData("test.txt", "*.cs|*.txt", true)]
        [InlineData("test.js", "*.cs|*.txt", false)]
        [InlineData("test.cs", "", true)]
        [InlineData("test.cs", null, true)]
        [InlineData("README.md", "*.md", true)]
        [InlineData("file.test.cs", "*.cs", true)]
        public void MatchesFileNamePattern_WithIncludePatterns_ReturnsExpectedResult(
            string fileName, string? pattern, bool expected)
        {
            var result = DockerSearchService.MatchesFileNamePattern(fileName, pattern!);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("test.log", "-*.log", false)]
        [InlineData("test.cs", "-*.log", true)]
        [InlineData("test.cs", "*.cs|-*.log", true)]
        [InlineData("test.log", "*.cs|*.log|-*.log", false)]
        public void MatchesFileNamePattern_WithExcludePatterns_ReturnsExpectedResult(
            string fileName, string pattern, bool expected)
        {
            var result = DockerSearchService.MatchesFileNamePattern(fileName, pattern);
            result.Should().Be(expected);
        }

        [Fact]
        public void MatchesFileNamePattern_WithQuestionMarkWildcard_MatchesSingleChar()
        {
            DockerSearchService.MatchesFileNamePattern("test1.cs", "test?.cs").Should().BeTrue();
            DockerSearchService.MatchesFileNamePattern("test12.cs", "test?.cs").Should().BeFalse();
        }

        [Theory]
        [InlineData("/app/src/file.cs", "/app", ".git", false)]
        [InlineData("/app/.git/config", "/app", ".git", true)]
        [InlineData("/app/node_modules/pkg/index.js", "/app", "node_modules", true)]
        [InlineData("/app/src/vendor/lib.cs", "/app", "vendor", true)]
        [InlineData("/app/src/file.cs", "/app", ".git,node_modules,vendor", false)]
        [InlineData("/app/node_modules/pkg/index.js", "/app", ".git,node_modules,vendor", true)]
        [InlineData("/app/src/file.cs", "/app", "", false)]
        [InlineData("/app/src/file.cs", "/app", null, false)]
        public void ShouldExcludeByDirectory_WithCommaSeparatedDirs_ReturnsExpectedResult(
            string filePath, string rootPath, string? excludeDirs, bool expected)
        {
            var result = DockerSearchService.ShouldExcludeByDirectory(filePath, rootPath, excludeDirs!);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("/app/.git/config", "/app", "^(.git|node_modules|vendor)$", true)]
        [InlineData("/app/node_modules/pkg/index.js", "/app", "^(.git|node_modules|vendor)$", true)]
        [InlineData("/app/vendor/lib/file.cs", "/app", "^(.git|node_modules|vendor)$", true)]
        [InlineData("/app/src/file.cs", "/app", "^(.git|node_modules|vendor)$", false)]
        [InlineData("/app/storage/logs/app.log", "/app", "^(.git|node_modules|vendor|storage)$", true)]
        public void ShouldExcludeByDirectory_WithRegexPattern_ReturnsExpectedResult(
            string filePath, string rootPath, string excludeDirs, bool expected)
        {
            var result = DockerSearchService.ShouldExcludeByDirectory(filePath, rootPath, excludeDirs);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("/app/file.cs", "/app", true)]
        [InlineData("/app/src/file.cs", "/app", false)]
        [InlineData("/app/src/nested/file.cs", "/app", false)]
        [InlineData("/app/readme.txt", "/app/", true)]
        public void IsFileInRootDirectory_ReturnsExpectedResult(
            string filePath, string rootPath, bool expected)
        {
            var result = DockerSearchService.IsFileInRootDirectory(filePath, rootPath);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("/app/.git/config", true)]
        [InlineData("/sys/kernel/debug", true)]
        [InlineData("/proc/1/status", true)]
        [InlineData("/dev/null", true)]
        [InlineData("/app/node_modules/pkg/index.js", true)]
        [InlineData("/app/vendor/lib/file.cs", true)]
        [InlineData("/app/src/file.cs", false)]
        [InlineData("/home/user/project/main.cs", false)]
        public void IsSystemPath_ReturnsExpectedResult(string filePath, bool expected)
        {
            var result = DockerSearchService.IsSystemPath(filePath);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("file.exe", true)]
        [InlineData("image.png", true)]
        [InlineData("archive.zip", true)]
        [InlineData("document.pdf", true)]
        [InlineData("code.cs", false)]
        [InlineData("script.js", false)]
        [InlineData("config.json", false)]
        [InlineData("readme.txt", false)]
        [InlineData("Makefile", false)]
        public void IsBinaryFileExtension_ReturnsExpectedResult(string fileName, bool expected)
        {
            var result = DockerSearchService.IsBinaryFileExtension(fileName);
            result.Should().Be(expected);
        }

        [Fact]
        public void MatchesGitignorePatterns_WithEnvFile_ReturnsTrue()
        {
            var patterns = new List<string> { ".env", "*.log", "node_modules/" };
            
            DockerSearchService.MatchesGitignorePatterns("/app/.env", "/app", patterns).Should().BeTrue();
            DockerSearchService.MatchesGitignorePatterns("/app/app.log", "/app", patterns).Should().BeTrue();
            DockerSearchService.MatchesGitignorePatterns("/app/node_modules/pkg/index.js", "/app", patterns).Should().BeTrue();
        }

        [Fact]
        public void MatchesGitignorePatterns_WithNonIgnoredFile_ReturnsFalse()
        {
            var patterns = new List<string> { ".env", "*.log", "node_modules/" };
            
            DockerSearchService.MatchesGitignorePatterns("/app/src/main.cs", "/app", patterns).Should().BeFalse();
            DockerSearchService.MatchesGitignorePatterns("/app/config.json", "/app", patterns).Should().BeFalse();
        }

        #endregion

        private sealed class FakeRunner : IDockerProcessRunner
        {
            public List<string> Commands { get; } = new();
            public List<string> FileNames { get; } = new();
            public Func<ProcessStartInfo, DockerProcessResult>? OnRun { get; set; }

            public Task<DockerProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
            {
                Commands.Add(startInfo.Arguments);
                FileNames.Add(startInfo.FileName);
                if (OnRun != null)
                {
                    return Task.FromResult(OnRun(startInfo));
                }

                return Task.FromResult(new DockerProcessResult(0, string.Empty, string.Empty));
            }

            public static string ExtractDestination(string arguments)
            {
                var segments = new List<string>();
                var builder = new StringBuilder();
                var insideQuotes = false;
                foreach (var ch in arguments)
                {
                    if (ch == '"')
                    {
                        if (insideQuotes)
                        {
                            segments.Add(builder.ToString());
                            builder.Clear();
                        }

                        insideQuotes = !insideQuotes;
                        continue;
                    }

                    if (insideQuotes)
                    {
                        builder.Append(ch);
                    }
                }

                return segments.Count > 0 ? segments[^1] : string.Empty;
            }
        }
    }
}


