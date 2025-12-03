using System;
using System.IO;
using System.Text;
using FluentAssertions;
using Grex.Services;
using Xunit;

namespace Grex.Tests.Services
{
    public class EncodingDetectionServiceTests
    {
        private readonly EncodingDetectionService _service;

        public EncodingDetectionServiceTests()
        {
            _service = new EncodingDetectionService();
        }

        [Fact]
        public void DetectEncoding_WithUTF8BOM_ReturnsUTF8WithHighConfidence()
        {
            // Arrange
            var utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
            var text = Encoding.UTF8.GetBytes("Hello World");
            var bytes = new byte[utf8Bom.Length + text.Length];
            Array.Copy(utf8Bom, 0, bytes, 0, utf8Bom.Length);
            Array.Copy(text, 0, bytes, utf8Bom.Length, text.Length);

            // Act
            var result = _service.DetectEncoding(bytes);

            // Assert
            result.Should().NotBeNull();
            result.Encoding.Should().Be(Encoding.UTF8);
            result.HasBom.Should().BeTrue();
            result.Confidence.Should().BeGreaterThan(0.9);
            result.DetectionMethod.Should().Contain("BOM");
        }

        [Fact]
        public void DetectEncoding_WithUTF16LEBOM_ReturnsUTF16LEWithHighConfidence()
        {
            // Arrange
            var utf16Bom = new byte[] { 0xFF, 0xFE };
            var text = Encoding.Unicode.GetBytes("Hello");
            var bytes = new byte[utf16Bom.Length + text.Length];
            Array.Copy(utf16Bom, 0, bytes, 0, utf16Bom.Length);
            Array.Copy(text, 0, bytes, utf16Bom.Length, text.Length);

            // Act
            var result = _service.DetectEncoding(bytes);

            // Assert
            result.Should().NotBeNull();
            result.Encoding.Should().Be(Encoding.Unicode);
            result.HasBom.Should().BeTrue();
            result.Confidence.Should().BeGreaterThan(0.9);
        }

        [Fact]
        public void DetectEncoding_WithUTF8WithoutBOM_ReturnsUTF8WithReasonableConfidence()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes("Hello World\nThis is a test file with some text.");

            // Act
            var result = _service.DetectEncoding(bytes);

            // Assert
            result.Should().NotBeNull();
            result.Encoding.Should().Be(Encoding.UTF8);
            result.HasBom.Should().BeFalse();
            result.Confidence.Should().BeGreaterThan(0.0);
        }

        [Fact]
        public void DetectEncoding_WithEmptyBytes_ReturnsUTF8WithLowConfidence()
        {
            // Arrange
            var bytes = Array.Empty<byte>();

            // Act
            var result = _service.DetectEncoding(bytes);

            // Assert
            result.Should().NotBeNull();
            result.Encoding.Should().Be(Encoding.UTF8);
            result.Confidence.Should().BeLessThan(0.2);
            result.DetectionMethod.Should().Contain("Empty");
        }

        [Fact]
        public void DetectEncoding_WithNullBytes_ReturnsUTF8WithLowConfidence()
        {
            // Arrange
            byte[]? bytes = null;

            // Act
            var result = _service.DetectEncoding(bytes!);

            // Assert
            result.Should().NotBeNull();
            result.Encoding.Should().Be(Encoding.UTF8);
            result.Confidence.Should().BeLessThan(0.2);
        }

        [Fact]
        public void DetectFileEncoding_WithValidFile_ReturnsEncoding()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                var content = "Hello World\nTest content";
                File.WriteAllText(tempFile, content, Encoding.UTF8);

                // Act
                var result = _service.DetectFileEncoding(tempFile);

                // Assert
                result.Should().NotBeNull();
                result.Encoding.Should().NotBeNull();
                result.Confidence.Should().BeGreaterThan(0.0);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void DetectFileEncoding_WithNonExistentFile_ReturnsUTF8Fallback()
        {
            // Arrange
            var nonExistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.txt");

            // Act
            var result = _service.DetectFileEncoding(nonExistentFile);

            // Assert
            result.Should().NotBeNull();
            result.Encoding.Should().Be(Encoding.UTF8);
            result.Confidence.Should().BeLessThan(0.2);
            result.DetectionMethod.Should().Contain("Error");
        }

        [Fact]
        public void DetectEncoding_WithFileNameHint_CanUseHintForDetection()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes("Test content");

            // Act
            var result = _service.DetectEncoding(bytes, "test_shift_jis.txt");

            // Assert
            result.Should().NotBeNull();
            result.Encoding.Should().NotBeNull();
        }

        [Fact]
        public void DetectEncoding_WithASCIIOnlyText_ReturnsUTF8()
        {
            // Arrange
            var bytes = Encoding.ASCII.GetBytes("This is pure ASCII text with no special characters.");

            // Act
            var result = _service.DetectEncoding(bytes);

            // Assert
            result.Should().NotBeNull();
            result.Encoding.Should().Be(Encoding.UTF8); // UTF-8 is backward compatible with ASCII
            result.Confidence.Should().BeGreaterThan(0.0);
        }

        [Fact]
        public void DetectEncoding_WithTextContainingNullBytes_MayDetectUTF16()
        {
            // Arrange
            // Create bytes that look like UTF-16 (alternating null bytes)
            var text = "Hello";
            var utf16Bytes = Encoding.Unicode.GetBytes(text);
            
            // Act
            var result = _service.DetectEncoding(utf16Bytes);

            // Assert
            result.Should().NotBeNull();
            result.Encoding.Should().NotBeNull();
            // May detect UTF-16 or UTF-8 depending on heuristics
        }

        [Fact]
        public void EncodingDetectionResult_Constructor_ClampsConfidenceBetween0And1()
        {
            // Arrange & Act
            var result1 = new EncodingDetectionResult(Encoding.UTF8, -0.5, false, "test");
            var result2 = new EncodingDetectionResult(Encoding.UTF8, 1.5, false, "test");

            // Assert
            result1.Confidence.Should().Be(0.0);
            result2.Confidence.Should().Be(1.0);
        }

        [Fact]
        public void DetectEncoding_WithWindows1252Text_CanDetectEncoding()
        {
            // Arrange
            Encoding? windows1252 = null;
            try
            {
                windows1252 = Encoding.GetEncoding("Windows-1252");
            }
            catch
            {
                // Windows-1252 may not be available on all platforms
            }

            if (windows1252 != null)
            {
                var bytes = windows1252.GetBytes("Test with special chars: é, ñ, ü");

                // Act
                var result = _service.DetectEncoding(bytes);

                // Assert
                result.Should().NotBeNull();
                result.Encoding.Should().NotBeNull();
            }
        }
    }
}

