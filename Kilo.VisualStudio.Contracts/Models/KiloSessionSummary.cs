using System;

namespace Kilo.VisualStudio.Contracts.Models
{
    public class KiloSessionSummary
    {
        public string SessionId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string WorkspaceDirectory { get; set; } = string.Empty;

        public KiloSessionStatus Status { get; set; } = KiloSessionStatus.Unknown;

        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
