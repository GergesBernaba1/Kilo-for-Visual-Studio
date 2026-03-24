using System.Collections.Generic;

namespace Kilo.VisualStudio.Contracts.Models
{
    public class KiloToolExecution
    {
        public string CallId { get; set; } = string.Empty;

        public string ToolName { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public KiloToolExecutionStatus Status { get; set; } = KiloToolExecutionStatus.Unknown;

        public string InputSummary { get; set; } = string.Empty;

        public string OutputSummary { get; set; } = string.Empty;

        public string SuggestedCode { get; set; } = string.Empty;

        public string PatchDiff { get; set; } = string.Empty;

        public IReadOnlyList<KiloFileDiff> FileDiffs { get; set; } = new KiloFileDiff[0];
    }
}
