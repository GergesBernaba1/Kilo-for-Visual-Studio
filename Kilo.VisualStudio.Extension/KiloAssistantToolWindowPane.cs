using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Kilo.VisualStudio.Extension
{
    [Guid(PackageGuids.ToolWindowGuidString)]
    public class KiloAssistantToolWindowPane : ToolWindowPane
    {
        public KiloAssistantToolWindowPane() : base(null)
        {
            this.Caption = "Kilo Assistant";
            this.Content = new UI.KiloAssistantToolWindowControl();
        }
    }
}
