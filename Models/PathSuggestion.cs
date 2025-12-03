namespace Grex.Models
{
    public class PathSuggestion
    {
        public string FullPath { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;

        public PathSuggestion(string fullPath, string displayText)
        {
            FullPath = fullPath;
            DisplayText = displayText;
        }

        public override string ToString() => DisplayText;
    }
}

