namespace Kilo.VisualStudio.Contracts.Models
{
    public class AssistantRequest
    {
        public string ActiveFilePath { get; set; } = string.Empty;
        public string LanguageId { get; set; } = string.Empty;
        public string SelectedText { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
    }
}
