namespace Kilo.VisualStudio.Contracts.Models
{
    public class KiloToolPermissionReply
    {
        public string PermissionId { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        public bool Approved { get; set; }

        public bool ApproveAlways { get; set; }

        public bool DenyAlways { get; set; }
    }
}
