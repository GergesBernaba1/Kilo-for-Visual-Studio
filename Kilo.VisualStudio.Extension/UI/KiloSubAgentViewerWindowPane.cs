using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Kilo.VisualStudio.Extension.UI
{
    [Guid(PackageGuids.SubAgentViewerWindowGuidString)]
    public class KiloSubAgentViewerWindowPane : ToolWindowPane
    {
        public KiloSubAgentViewerWindowPane() : base(null)
        {
            this.Caption = "Kilo Sub-Agent Viewer";
            this.Content = new KiloSubAgentViewerControl();
        }
    }
}
