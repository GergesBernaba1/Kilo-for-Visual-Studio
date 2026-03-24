using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Kilo.VisualStudio.App.Services;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.Extension
{
    internal class KiloCompletionSource : ICompletionSource
    {
        private readonly ITextBuffer _textBuffer;
        private readonly AutocompleteService _autocompleteService;
        private bool _isDisposed;

        public KiloCompletionSource(ITextBuffer textBuffer, AutocompleteService autocompleteService)
        {
            _textBuffer = textBuffer;
            _autocompleteService = autocompleteService;
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            if (_isDisposed)
                return;

            var snapshot = _textBuffer.CurrentSnapshot;
            var triggerPoint = session.GetTriggerPoint(snapshot);
            if (triggerPoint == null)
                return;

            var line = snapshot.GetLineFromPosition(triggerPoint.Value.Position);
            var lineText = line.GetText();
            var position = triggerPoint.Value.Position - line.Start.Position;

            // Get the prefix (text before cursor on the line)
            var prefix = position > 0 ? lineText.Substring(0, position) : string.Empty;

            // Find the start of the current word
            var wordStart = prefix.LastIndexOfAny(new[] { ' ', '\t', '.', '(', '[', '{', ';', '=', '+', '-', '*', '/', '%', '&', '|', '^', '!', '~', '<', '>', '?', ':', ',' });
            wordStart = wordStart >= 0 ? wordStart + 1 : 0;
            var currentPrefix = prefix.Substring(wordStart);

            if (string.IsNullOrWhiteSpace(currentPrefix))
                return;

            // Get file path
            var filePath = GetFilePath(snapshot);

            // Get completions asynchronously
            Task.Run(async () =>
            {
                try
                {
                    var completions = await _autocompleteService.GetCompletionsAsync(
                        filePath,
                        line.LineNumber,
                        position,
                        currentPrefix);

                    if (completions.Any())
                    {
                        var completionItems = new List<Completion>();
                        foreach (var c in completions)
                        {
                            var description = c.Detail;
                            if (!string.IsNullOrEmpty(c.Documentation))
                            {
                                description += "\n" + c.Documentation;
                            }
                            var completion = new Completion(c.Label, c.InsertText ?? c.Label, description, null, "");
                            completionItems.Add(completion);
                        }

                        var trackingSpan = snapshot.CreateTrackingSpan(
                            triggerPoint.Value.Position - currentPrefix.Length,
                            currentPrefix.Length,
                            SpanTrackingMode.EdgeInclusive);

                        var completionSet = new CompletionSet(
                            "Kilo Completions",
                            "Kilo AI Completions",
                            trackingSpan,
                            completionItems,
                            null);

                        // Add to completion sets on UI thread
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        completionSets.Add(completionSet);
                    }
                }
                catch
                {
                    // Ignore errors in completion
                }
            });
        }

        private string GetFilePath(ITextSnapshot snapshot)
        {
            var textDocument = snapshot.TextBuffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument));
            return textDocument?.FilePath ?? string.Empty;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
            }
        }
    }
}