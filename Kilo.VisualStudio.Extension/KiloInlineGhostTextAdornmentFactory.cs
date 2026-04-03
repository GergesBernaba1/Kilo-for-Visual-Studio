using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Kilo.VisualStudio.Extension
{
    /// <summary>
    /// Factory for creating KiloInlineGhostTextAdornment instances.
    /// This is the MEF component that creates adornments for each text view.
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class KiloInlineGhostTextAdornmentFactory : IWpfTextViewCreationListener
    {
        private const string AdornmentLayerName = "KiloInlineGhostText";

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(AdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.Selection, Before = PredefinedAdornmentLayers.Text)]
        public AdornmentLayerDefinition? EditorAdornmentLayer = null;

        /// <summary>
        /// Called when a text view is created
        /// </summary>
        public void TextViewCreated(IWpfTextView textView)
        {
            if (textView == null)
            {
                return;
            }

            // Get or create the adornment layer
            var adornmentLayer = textView.GetAdornmentLayer(AdornmentLayerName);
            if (adornmentLayer == null)
            {
                return;
            }

            // Create the adornment and store it in the view's properties
            var adornment = new KiloInlineGhostTextAdornment(textView, adornmentLayer);
            textView.Properties.GetOrCreateSingletonProperty(() => adornment);

            // Also create and attach the controller for keyboard handling
            var controller = new KiloInlineGhostTextController(textView, adornment);
            textView.Properties.GetOrCreateSingletonProperty(() => controller);

            // Create the inline completion manager if AutocompleteService is available
            var autocompleteService = KiloPackage.AutocompleteServiceInstance;
            if (autocompleteService != null)
            {
                var completionManager = new KiloInlineCompletionManager(textView, autocompleteService, adornment);
                textView.Properties.GetOrCreateSingletonProperty(() => completionManager);
            }
        }
    }
}
