namespace Kilo.VisualStudio.Contracts.Models
{
    public class AssistantResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? SuggestedCode { get; set; }
        public string? PatchDiff { get; set; }
        public string? Error { get; set; }

        // Cost and usage information
        public double UsageCostUsd { get; set; }
        public int? UsageTokens { get; set; }
        public string? ProviderId { get; set; }
        public string? ModelId { get; set; }
    }
}
