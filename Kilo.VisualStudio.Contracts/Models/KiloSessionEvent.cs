using System;
using System.Collections.Generic;

namespace Kilo.VisualStudio.Contracts.Models
{
    public class KiloSessionEvent
    {
        public string EventType { get; set; } = string.Empty;

        public KiloSessionEventKind Kind { get; set; } = KiloSessionEventKind.Unknown;

        public string SessionId { get; set; } = string.Empty;

        public string MessageId { get; set; } = string.Empty;

        public string PartId { get; set; } = string.Empty;

        public string Delta { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string SuggestedCode { get; set; } = string.Empty;

        public string PatchDiff { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;

        public KiloConnectionState ConnectionState { get; set; } = KiloConnectionState.Disconnected;

        public KiloSessionSummary? Session { get; set; }

        public KiloToolExecution? ToolExecution { get; set; }

        public IReadOnlyList<KiloFileDiff> FileDiffs { get; set; } = new KiloFileDiff[0];

        public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
