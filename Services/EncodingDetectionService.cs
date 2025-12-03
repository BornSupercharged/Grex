using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Grex.Services
{
    /// <summary>
    /// Comprehensive encoding detection service supporting multiple character encodings
    /// </summary>
    public class EncodingDetectionService : IEncodingDetectionService
    {
        // Common encodings to check
        private static readonly Encoding[] CommonEncodings = GetSupportedEncodings();

        private static Encoding[] GetSupportedEncodings()
        {
            var encodings = new List<Encoding>
            {
                Encoding.UTF8,
                Encoding.Unicode,      // UTF-16 Little Endian
                Encoding.BigEndianUnicode, // UTF-16 Big Endian
                Encoding.UTF32,       // UTF-32 Little Endian
            };

            // Try to add additional encodings, but skip ones that aren't supported
            var encodingNames = new[]
            {
                "UTF-32BE", // UTF-32 Big Endian
                "ISO-8859-1", // Latin-1
                "ISO-8859-2", // Latin-2
                "ISO-8859-3", // Latin-3
                "ISO-8859-4", // Latin-4
                "ISO-8859-5", // Cyrillic
                "ISO-8859-6", // Arabic
                "ISO-8859-7", // Greek
                "ISO-8859-8", // Hebrew
                "ISO-8859-9", // Latin-5 (Turkish)
                "ISO-8859-10", // Latin-6 (Nordic)
                "ISO-8859-11", // Thai
                "ISO-8859-13", // Latin-7 (Baltic)
                "ISO-8859-14", // Latin-8 (Celtic)
                "ISO-8859-15", // Latin-9 (Western European with Euro)
                "ISO-8859-16", // Latin-10 (South-Eastern European)
                "Windows-1252", // Western European (Windows)
                "Windows-1250", // Central European (Windows)
                "Windows-1251", // Cyrillic (Windows)
                "Windows-1253", // Greek (Windows)
                "Windows-1254", // Turkish (Windows)
                "Windows-1255", // Hebrew (Windows)
                "Windows-1256", // Arabic (Windows)
                "Windows-1257", // Baltic (Windows)
                "Windows-1258", // Vietnamese (Windows)
                "Shift-JIS", // Japanese
                "GB2312", // Simplified Chinese
                "GBK", // Simplified Chinese (extended)
                "Big5", // Traditional Chinese
                "EUC-KR", // Korean
                "KOI8-R", // Russian
                "KOI8-U" // Ukrainian
            };

            foreach (var name in encodingNames)
            {
                try
                {
                    var encoding = Encoding.GetEncoding(name);
                    if (encoding != null)
                    {
                        encodings.Add(encoding);
                    }
                }
                catch
                {
                    // Skip encodings that aren't supported
                }
            }

            return encodings.ToArray();
        }

        // BOM signatures for common encodings
        private static readonly Dictionary<byte[], (Encoding encoding, string name)> BomSignatures = 
            new Dictionary<byte[], (Encoding, string)>(new ByteArrayComparer())
            {
                { new byte[] { 0xEF, 0xBB, 0xBF }, (Encoding.UTF8, "UTF-8 with BOM") },
                { new byte[] { 0xFF, 0xFE }, (Encoding.Unicode, "UTF-16 Little Endian with BOM") },
                { new byte[] { 0xFE, 0xFF }, (Encoding.BigEndianUnicode, "UTF-16 Big Endian with BOM") },
                { new byte[] { 0xFF, 0xFE, 0x00, 0x00 }, (Encoding.UTF32, "UTF-32 Little Endian with BOM") },
                { new byte[] { 0x00, 0x00, 0xFE, 0xFF }, (Encoding.GetEncoding("UTF-32BE"), "UTF-32 Big Endian with BOM") }
            };

        // Character frequency tables for statistical analysis
        private static readonly Dictionary<string, Dictionary<byte, double>> CharacterFrequencyTables = 
            new Dictionary<string, Dictionary<byte, double>>();

        static EncodingDetectionService()
        {
            InitializeFrequencyTables();
        }

        public EncodingDetectionResult DetectFileEncoding(string filePath)
        {
            try
            {
                var bytes = File.ReadAllBytes(filePath);
                return DetectEncoding(bytes, Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                return new EncodingDetectionResult(Encoding.UTF8, 0.1, false, $"Error reading file: {ex.Message}");
            }
        }

        public EncodingDetectionResult DetectEncoding(byte[] bytes)
        {
            return DetectEncoding(bytes, string.Empty);
        }

        public EncodingDetectionResult DetectEncoding(byte[] bytes, string fileName)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return new EncodingDetectionResult(Encoding.UTF8, 0.1, false, "Empty file");
            }

            // First, check for BOM
            var bomResult = DetectByBom(bytes);
            if (bomResult != null)
            {
                return bomResult;
            }

            // If no BOM, use statistical analysis
            var statisticalResult = DetectByStatisticalAnalysis(bytes, fileName);
            
            // If statistical analysis fails, fall back to heuristics
            if (statisticalResult.Confidence < 0.3)
            {
                var heuristicResult = DetectByHeuristics(bytes, fileName);
                return heuristicResult.Confidence > statisticalResult.Confidence ? heuristicResult : statisticalResult;
            }

            return statisticalResult;
        }

        private EncodingDetectionResult DetectByBom(byte[] bytes)
        {
            foreach (var bomSignature in BomSignatures)
            {
                var signature = bomSignature.Key;
                if (bytes.Length >= signature.Length && 
                    bytes.Take(signature.Length).SequenceEqual(signature))
                {
                    var (encoding, name) = bomSignature.Value;
                    return new EncodingDetectionResult(encoding, 0.95, true, $"BOM detected: {name}");
                }
            }

            return null!;
        }

        private EncodingDetectionResult DetectByStatisticalAnalysis(byte[] bytes, string fileName)
        {
            var results = new List<EncodingDetectionResult>();

            foreach (var encoding in CommonEncodings)
            {
                if (encoding == null) continue;

                try
                {
                    var confidence = CalculateEncodingConfidence(bytes, encoding, fileName);
                    if (confidence > 0.1) // Only consider encodings with reasonable confidence
                    {
                        results.Add(new EncodingDetectionResult(encoding, confidence, false, $"Statistical analysis: {encoding.EncodingName}"));
                    }
                }
                catch
                {
                    // Skip encodings that can't be analyzed
                    continue;
                }
            }

            // Sort by confidence and return the best match
            var bestResult = results.OrderByDescending(r => r.Confidence).FirstOrDefault();
            return bestResult ?? new EncodingDetectionResult(Encoding.UTF8, 0.2, false, "Statistical analysis failed, using UTF-8 fallback");
        }

        private double CalculateEncodingConfidence(byte[] bytes, Encoding encoding, string fileName)
        {
            try
            {
                // Decode the bytes using the candidate encoding
                var decoded = encoding.GetString(bytes);
                
                // Calculate confidence based on multiple factors
                double confidence = 0.0;

                // Factor 1: Valid character sequences (no invalid sequences)
                confidence += CheckValidCharacterSequences(decoded) * 0.4;

                // Factor 2: Character frequency analysis
                confidence += CheckCharacterFrequency(bytes, encoding) * 0.3;

                // Factor 3: File name hints
                confidence += CheckFileNameHints(fileName, encoding) * 0.1;

                // Factor 4: Common text patterns
                confidence += CheckCommonTextPatterns(decoded) * 0.2;

                return Math.Min(1.0, confidence);
            }
            catch
            {
                return 0.0;
            }
        }

        private double CheckValidCharacterSequences(string decoded)
        {
            if (string.IsNullOrEmpty(decoded))
                return 0.0;

            int validChars = 0;
            int totalChars = decoded.Length;

            foreach (char c in decoded)
            {
                if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                {
                    // Invalid control character
                    continue;
                }
                validChars++;
            }

            return (double)validChars / totalChars;
        }

        private double CheckCharacterFrequency(byte[] bytes, Encoding encoding)
        {
            if (!CharacterFrequencyTables.TryGetValue(encoding.EncodingName, out var frequencyTable))
                return 0.5; // Neutral score if no frequency table available

            if (bytes.Length == 0)
                return 0.0;

            int matchingBytes = 0;
            int totalBytes = Math.Min(bytes.Length, 10000); // Sample first 10KB for performance

            for (int i = 0; i < totalBytes; i++)
            {
                if (frequencyTable.ContainsKey(bytes[i]))
                    matchingBytes++;
            }

            return (double)matchingBytes / totalBytes;
        }

        private double CheckFileNameHints(string fileName, Encoding encoding)
        {
            if (string.IsNullOrEmpty(fileName))
                return 0.5;

            // File name hints based on common conventions
            var lowerFileName = fileName.ToLowerInvariant();

            // Japanese files often contain these patterns
            if ((lowerFileName.Contains("shift_jis") || lowerFileName.Contains("sjis")) && 
                encoding.EncodingName.Contains("Shift"))
                return 0.8;

            // Chinese files
            if ((lowerFileName.Contains("gb2312") || lowerFileName.Contains("gbk") || lowerFileName.Contains("chinese")) && 
                (encoding.EncodingName.Contains("GB") || encoding.EncodingName.Contains("Chinese")))
                return 0.8;

            // Korean files
            if ((lowerFileName.Contains("euc-kr") || lowerFileName.Contains("korean")) && 
                encoding.EncodingName.Contains("EUC-KR"))
                return 0.8;

            // Russian/Cyrillic files
            if ((lowerFileName.Contains("koi8") || lowerFileName.Contains("cyrillic") || lowerFileName.Contains("russian")) && 
                (encoding.EncodingName.Contains("KOI8") || encoding.EncodingName.Contains("Cyrillic")))
                return 0.8;

            return 0.5; // Neutral score
        }

        private double CheckCommonTextPatterns(string decoded)
        {
            if (string.IsNullOrEmpty(decoded))
                return 0.0;

            double score = 0.0;
            int sampleLength = Math.Min(decoded.Length, 1000);
            var sample = decoded.Substring(0, sampleLength);

            // Check for common text patterns
            if (sample.Contains(" ") || sample.Contains("\t") || sample.Contains("\n"))
                score += 0.2; // Contains whitespace

            if (sample.Any(char.IsLetter))
                score += 0.3; // Contains letters

            if (sample.Any(char.IsDigit))
                score += 0.1; // Contains digits

            // Check for common programming keywords
            var commonKeywords = new[] { "function", "class", "import", "export", "public", "private", "var", "let", "const" };
            if (commonKeywords.Any(keyword => sample.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                score += 0.2; // Contains programming keywords

            // Check for balanced parentheses/brackets (indicates structured text)
            int openParen = sample.Count(c => c == '(');
            int closeParen = sample.Count(c => c == ')');
            int openBrace = sample.Count(c => c == '{');
            int closeBrace = sample.Count(c => c == '}');
            int openBracket = sample.Count(c => c == '[');
            int closeBracket = sample.Count(c => c == ']');

            if (Math.Abs(openParen - closeParen) <= 2 && 
                Math.Abs(openBrace - closeBrace) <= 2 && 
                Math.Abs(openBracket - closeBracket) <= 2)
                score += 0.2; // Balanced brackets

            return Math.Min(1.0, score);
        }

        private EncodingDetectionResult DetectByHeuristics(byte[] bytes, string fileName)
        {
            // Simple heuristics as a fallback
            if (bytes.Length >= 2)
            {
                // Check for null bytes (common in UTF-16)
                bool hasNullBytes = bytes.Any(b => b == 0);
                if (hasNullBytes)
                {
                    // Likely UTF-16
                    if (bytes.Length >= 4 && bytes[1] == 0 && bytes[3] == 0)
                    {
                        return new EncodingDetectionResult(Encoding.Unicode, 0.6, false, "Heuristic: UTF-16 LE detected by null byte pattern");
                    }
                    else
                    {
                        return new EncodingDetectionResult(Encoding.BigEndianUnicode, 0.6, false, "Heuristic: UTF-16 BE detected by null byte pattern");
                    }
                }

                // Check for high ASCII values (> 127) which might indicate specific encodings
                int highAsciiCount = bytes.Count(b => b > 127);
                double highAsciiRatio = (double)highAsciiCount / bytes.Length;

                if (highAsciiRatio > 0.3)
                {
                    // Likely non-ASCII encoding
                    // Check for specific patterns
                    if (IsLikelyShiftJIS(bytes))
                    {
                        return new EncodingDetectionResult(Encoding.GetEncoding("Shift-JIS"), 0.5, false, "Heuristic: Shift-JIS detected by byte patterns");
                    }
                    else if (IsLikelyChinese(bytes))
                    {
                        return new EncodingDetectionResult(Encoding.GetEncoding("GB2312"), 0.5, false, "Heuristic: Chinese encoding detected by byte patterns");
                    }
                    else if (IsLikelyKorean(bytes))
                    {
                        return new EncodingDetectionResult(Encoding.GetEncoding("EUC-KR"), 0.5, false, "Heuristic: Korean encoding detected by byte patterns");
                    }
                }
            }

            // Default to UTF-8
            return new EncodingDetectionResult(Encoding.UTF8, 0.4, false, "Heuristic: Defaulting to UTF-8");
        }

        private bool IsLikelyShiftJIS(byte[] bytes)
        {
            // Shift-JIS has specific byte patterns
            for (int i = 0; i < bytes.Length - 1; i++)
            {
                byte b1 = bytes[i];
                byte b2 = bytes[i + 1];

                // Check for Shift-JIS lead byte ranges
                if ((b1 >= 0x81 && b1 <= 0x9F) || (b1 >= 0xE0 && b1 <= 0xEF))
                {
                    // Lead byte should be followed by trail byte
                    if (b2 < 0x40 || (b2 >= 0x7F && b2 <= 0x9F) || b2 > 0xFC)
                        return false;
                }
            }
            return true;
        }

        private bool IsLikelyChinese(byte[] bytes)
        {
            // GB2312/GBK have specific byte patterns
            for (int i = 0; i < bytes.Length - 1; i++)
            {
                byte b1 = bytes[i];
                byte b2 = bytes[i + 1];

                // Check for GB2312 lead byte ranges
                if (b1 >= 0xA1 && b1 <= 0xF7)
                {
                    // Lead byte should be followed by trail byte
                    if (b2 < 0xA1 || b2 > 0xFE)
                        return false;
                }
            }
            return true;
        }

        private bool IsLikelyKorean(byte[] bytes)
        {
            // EUC-KR has specific byte patterns
            for (int i = 0; i < bytes.Length - 1; i++)
            {
                byte b1 = bytes[i];
                byte b2 = bytes[i + 1];

                // Check for EUC-KR lead byte ranges
                if (b1 >= 0x81 && b1 <= 0xFE)
                {
                    // Lead byte should be followed by trail byte
                    if (b2 < 0x41 || (b2 >= 0x5B && b2 <= 0x5F) || 
                        (b2 >= 0x61 && b2 <= 0x7A) || (b2 >= 0x7B && b2 <= 0x7E) || 
                        b2 == 0x80 || (b2 >= 0xFF && b2 <= 0xFF))
                        return false;
                }
            }
            return true;
        }

        private static void InitializeFrequencyTables()
        {
            // Initialize basic frequency tables for common encodings
            // This is a simplified version - in a real implementation, 
            // you would use more comprehensive frequency data

            // UTF-8 frequency table (simplified)
            var utf8Freq = new Dictionary<byte, double>();
            for (int i = 0; i < 128; i++)
            {
                utf8Freq[(byte)i] = 0.1; // ASCII characters are common
            }
            CharacterFrequencyTables["UTF-8"] = utf8Freq;

            // Windows-1252 frequency table (simplified)
            var win1252Freq = new Dictionary<byte, double>();
            for (int i = 0; i < 256; i++)
            {
                win1252Freq[(byte)i] = i < 128 ? 0.1 : 0.01; // ASCII more common than extended
            }
            CharacterFrequencyTables["Western European (Windows)"] = win1252Freq;
        }

        // Helper class for byte array comparison in dictionary keys
        private class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[]? x, byte[]? y)
            {
                if (x == y) return true;
                if (x == null || y == null) return false;
                if (x.Length != y.Length) return false;
    
                for (int i = 0; i < x.Length; i++)
                {
                    if (x[i] != y[i]) return false;
                }
                return true;
            }
    
            public int GetHashCode(byte[]? obj)
            {
                if (obj == null) throw new ArgumentNullException(nameof(obj));
                
                int hash = 17;
                for (int i = 0; i < Math.Min(obj.Length, 4); i++)
                {
                    hash = hash * 31 + obj[i];
                }
                return hash;
            }
        }
    }
}