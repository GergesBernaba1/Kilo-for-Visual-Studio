using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Kilo.VisualStudio.Extension.UI
{
    [Guid(PackageGuids.SettingsWindowGuidString)]
    public class KiloSettingsWindowPane : ToolWindowPane
    {
        public KiloSettingsWindowPane() : base(null)
        {
            this.Caption = "Kilo Settings";
            this.Content = new KiloSettingsControl();
        }
    }
}