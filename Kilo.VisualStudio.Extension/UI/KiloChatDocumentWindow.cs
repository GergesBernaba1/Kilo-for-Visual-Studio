using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Kilo.VisualStudio.App.Services;

namespace Kilo.VisualStudio.Extension.UI
{
    [Guid(PackageGuids.ChatDocumentGuidString)]
    public class KiloChatDocumentWindow : WindowPane
    {
        private readonly KiloChatDocumentControl _content;

        public KiloChatDocumentWindow() : base(null)
        {
            _content = new KiloChatDocumentControl();
            this.Content = _content;
        }

        public void Initialize(AssistantService assistantService, Models.ExtensionSettings settings)
        {
            _content.Initialize(assistantService, settings);
        }

        public void SetContext(string activeFilePath, string selectedText, string languageId)
        {
            _content.SetContext(activeFilePath, selectedText, languageId);
        }

        public void SetPrompt(string prompt)
        {
            _content.SetPrompt(prompt);
        }
    }
}