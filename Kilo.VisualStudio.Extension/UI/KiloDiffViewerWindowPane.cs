using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Kilo.VisualStudio.Extension.UI
{
    [Guid(PackageGuids.DiffViewerWindowGuidString)]
    public class KiloDiffViewerWindowPane : ToolWindowPane
    {
        public KiloDiffViewerWindowPane() : base(null)
        {
            this.Caption = "Kilo Diff Viewer";
            this.Content = new KiloDiffViewerControl();
        }
    }
}