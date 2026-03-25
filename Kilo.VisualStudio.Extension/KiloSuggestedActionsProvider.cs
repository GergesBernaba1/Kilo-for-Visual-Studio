using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Kilo.VisualStudio.Extension
{
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("Kilo suggested actions provider")]
    [ContentType("code")]
    internal class KiloSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
    {
        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            return new KiloSuggestedActionsSource(textView, textBuffer);
        }
    }

    internal class KiloSuggestedActionsSource : ISuggestedActionsSource
    {
        private readonly ITextView _textView;
        private readonly ITextBuffer _textBuffer;

        public KiloSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            _textView = textView;
            _textBuffer = textBuffer;
            _textView.Selection.SelectionChanged += OnTextSelectionChanged;
        }

        public event EventHandler<EventArgs>? SuggestedActionsChanged;

        private void OnTextSelectionChanged(object? sender, EventArgs e)
        {
            SuggestedActionsChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool HasSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            return _textView.Selection.SelectedSpans.Any(s => !s.IsEmpty);
        }

        public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            return Task.FromResult(HasSuggestedActions(requestedActionCategories, range, cancellationToken));
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            if (_textView.Selection.SelectedSpans.Count == 0)
                return Enumerable.Empty<SuggestedActionSet>();

            var selectedSpan = _textView.Selection.SelectedSpans.First();
            var selectedText = selectedSpan.GetText();
            var displayText = selectedText.Length > 80 ? selectedText.Substring(0, 77) + "..." : selectedText;
            var action = new KiloSuggestedAction($"Kilo: Uppercase selection ({displayText})", selectedText);
            var set = new SuggestedActionSet(new[] { action }, SuggestedActionSetPriority.Medium, default, null);
            return new[] { set };
        }

        public Task<IEnumerable<SuggestedActionSet>> GetSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            // Async API is required by the contract, reusing the synchronous implementation.
            return Task.FromResult(GetSuggestedActions(requestedActionCategories, range, cancellationToken));
        }

        public void Dispose()
        {
            _textView.Selection.SelectionChanged -= OnTextSelectionChanged;
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }
    }

    internal class KiloSuggestedAction : ISuggestedAction
    {
        private readonly string _displayText;
        private readonly string _selectedText;

        public KiloSuggestedAction(string displayText, string selectedText)
        {
            _displayText = displayText;
            _selectedText = selectedText;
        }

        public string DisplayText => _displayText;

        public ImageMoniker IconMoniker => default;
        public string IconAutomationText => "Kilo AI action";
        public string InputGestureText => "";
        public bool HasActionSets => false;
        public bool HasPreview => false;

        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<SuggestedActionSet>>(Enumerable.Empty<SuggestedActionSet>());
        }

        public Task<object> GetPreviewAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<object>(null!);
        }

        public void Invoke(CancellationToken cancellationToken)
        {
            if (KiloPackage.Instance != null)
            {
                KiloPackage.Instance.ReplaceActiveSelection(_selectedText.ToUpperInvariant());
            }
        }

        public void Dispose() { }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }
    }
}
