using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Kilo.VisualStudio.Extension
{
    [Guid(PackageGuids.AutomationWindowGuidString)]
    public class KiloAutomationToolWindowPane : ToolWindowPane
    {
        public KiloAutomationToolWindowPane() : base(null)
        {
            this.Caption = "Kilo Automation";
            this.Content = new UI.KiloAutomationToolWindowControl();
        }
    }
}