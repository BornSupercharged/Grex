using System;
using System.IO;

namespace Grex.Models
{
    public class FileSearchResult
    {
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
        public int MatchCount { get; set; }
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string Encoding { get; set; } = "Unknown";
        public DateTime DateModified { get; set; }
        
        public string DirectoryPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RelativePath))
                    return string.Empty;
                
                try
                {
                    var directory = Path.GetDirectoryName(RelativePath);
                    if (string.IsNullOrWhiteSpace(directory) || directory == ".")
                        return string.Empty;
                    
                    return directory.Replace('\\', '/');
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
        
        public string FormattedSize
        {
            get
            {
                if (Size < 1024)
                    return $"{Size} B";
                if (Size < 1024 * 1024)
                    return $"{Size / 1024.0:F2} KB";
                if (Size < 1024 * 1024 * 1024)
                    return $"{Size / (1024.0 * 1024.0):F2} MB";
                return $"{Size / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }
        
        public string FormattedDateModified
        {
            get
            {
                return DateModified.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
    }
}

