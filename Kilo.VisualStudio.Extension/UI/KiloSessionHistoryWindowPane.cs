using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Kilo.VisualStudio.Extension.UI
{
    [Guid(PackageGuids.SessionHistoryWindowGuidString)]
    public class KiloSessionHistoryWindowPane : ToolWindowPane
    {
        public KiloSessionHistoryWindowPane() : base(null)
        {
            this.Caption = "Kilo Session History";
            this.Content = new KiloSessionHistoryControl();
        }
    }
}