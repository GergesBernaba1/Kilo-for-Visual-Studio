namespace Kilo.VisualStudio.Contracts.Models
{
    public class KiloChatRequest
    {
        public string SessionId { get; set; } = string.Empty;

        public string WorkspaceDirectory { get; set; } = string.Empty;

        public string ActiveFilePath { get; set; } = string.Empty;

        public string SelectedText { get; set; } = string.Empty;

        public string LanguageId { get; set; } = string.Empty;

        public string Prompt { get; set; } = string.Empty;

        public string ProviderId { get; set; } = string.Empty;

        public string ModelId { get; set; } = string.Empty;

        public string Agent { get; set; } = string.Empty;

        public string Variant { get; set; } = string.Empty;

        public bool NoReply { get; set; }
    }
}
