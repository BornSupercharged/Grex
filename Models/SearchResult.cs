using System.IO;

namespace Grex.Models
{
    public class SearchResult
    {
        public string FileName { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
        public string LineContent { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        /// <summary>
        /// Number of occurrences of the search term on this line.
        /// Defaults to 1 for backward compatibility.
        /// </summary>
        public int MatchCount { get; set; } = 1;
        
        public string DirectoryPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RelativePath))
                    return string.Empty;
                
                try
                {
                    var directory = Path.GetDirectoryName(RelativePath);
                    // If the path is just a filename (no directory), return empty string
                    if (string.IsNullOrWhiteSpace(directory) || directory == ".")
                        return string.Empty;
                    
                    // Normalize path separators
                    return directory.Replace('\\', '/');
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
        
        public string TrimmedLineContent
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LineContent))
                    return string.Empty;
                
                return LineContent.Trim();
            }
        }
    }
}

