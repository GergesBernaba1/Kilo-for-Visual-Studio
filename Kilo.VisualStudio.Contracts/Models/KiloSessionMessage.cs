using System;

namespace Kilo.VisualStudio.Contracts.Models
{
    public class KiloSessionMessage
    {
        public string MessageId { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
