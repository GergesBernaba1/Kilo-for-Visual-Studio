using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Kilo.VisualStudio.Extension.UI
{
    [Guid(PackageGuids.AgentManagerWindowGuidString)]
    public class KiloAgentManagerWindowPane : ToolWindowPane
    {
        public KiloAgentManagerWindowPane() : base(null)
        {
            this.Caption = "Kilo Agent Manager";
            this.Content = new KiloAgentManagerControl();
        }
    }
}
