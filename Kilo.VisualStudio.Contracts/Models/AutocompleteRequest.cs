namespace Kilo.VisualStudio.Contracts.Models
{
    public class AutocompleteRequest
    {
        public string FilePath { get; set; } = string.Empty;
        public string LanguageId { get; set; } = string.Empty;
        public int Line { get; set; }
        public int Column { get; set; }
        public string Prefix { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty; // surrounding code context
    }
}