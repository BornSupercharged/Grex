using System;
using System.Text;

namespace Grex.Services
{
    /// <summary>
    /// Interface for detecting character encodings in files
    /// </summary>
    public interface IEncodingDetectionService
    {
        /// <summary>
        /// Detects the encoding of a file from its byte array
        /// </summary>
        /// <param name="filePath">Path to the file to analyze</param>
        /// <returns>Encoding detection result with confidence score</returns>
        EncodingDetectionResult DetectFileEncoding(string filePath);

        /// <summary>
        /// Detects the encoding from a byte array
        /// </summary>
        /// <param name="bytes">Byte array to analyze</param>
        /// <returns>Encoding detection result with confidence score</returns>
        EncodingDetectionResult DetectEncoding(byte[] bytes);

        /// <summary>
        /// Detects the encoding from a byte array with file name hint
        /// </summary>
        /// <param name="bytes">Byte array to analyze</param>
        /// <param name="fileName">File name for context (helps with detection)</param>
        /// <returns>Encoding detection result with confidence score</returns>
        EncodingDetectionResult DetectEncoding(byte[] bytes, string fileName);
    }

    /// <summary>
    /// Result of encoding detection with confidence scoring
    /// </summary>
    public class EncodingDetectionResult
    {
        /// <summary>
        /// The detected encoding
        /// </summary>
        public Encoding Encoding { get; set; }

        /// <summary>
        /// Confidence score (0.0 to 1.0, where 1.0 is highest confidence)
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Whether the detection was based on a BOM (Byte Order Mark)
        /// </summary>
        public bool HasBom { get; set; }

        /// <summary>
        /// Additional information about the detection process
        /// </summary>
        public string DetectionMethod { get; set; }

        /// <summary>
        /// Creates a new encoding detection result
        /// </summary>
        public EncodingDetectionResult(Encoding encoding, double confidence, bool hasBom = false, string detectionMethod = "")
        {
            Encoding = encoding;
            Confidence = Math.Max(0.0, Math.Min(1.0, confidence)); // Clamp between 0 and 1
            HasBom = hasBom;
            DetectionMethod = detectionMethod;
        }
    }
}