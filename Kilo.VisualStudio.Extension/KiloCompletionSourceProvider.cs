using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Kilo.VisualStudio.App.Services;

namespace Kilo.VisualStudio.Extension
{
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("code")]
    [Name("Kilo Completion Source Provider")]
    internal class KiloCompletionSourceProvider : ICompletionSourceProvider
    {
        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            var service = KiloPackage.AutocompleteServiceInstance;
            if (service == null)
                return null;

            return new KiloCompletionSource(textBuffer, service);
        }
    }
}