using System;
using System.Threading.Tasks;
using Kilo.VisualStudio.App.Services;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.Extension.UI
{
    public class KiloAssistantToolWindow
    {
        private readonly AssistantService _assistantService;

        public KiloAssistantToolWindow(AssistantService assistantService)
        {
            _assistantService = assistantService ?? throw new ArgumentNullException(nameof(assistantService));
        }

        public async Task<AssistantResponse> ExecuteQueryAsync(string activeFilePath, string languageId, string selectedText, string prompt)
        {
            var request = new AssistantRequest
            {
                ActiveFilePath = activeFilePath,
                LanguageId = languageId,
                SelectedText = selectedText,
                Prompt = prompt,
                SessionId = Guid.NewGuid().ToString("N")
            };

            return await _assistantService.AskAssistantAsync(request);
        }
    }
}
