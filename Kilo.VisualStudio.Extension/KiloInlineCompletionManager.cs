using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Kilo.VisualStudio.App.Services;

namespace Kilo.VisualStudio.Extension
{
    /// <summary>
    /// Manager for inline completions that displays ghost text suggestions.
    /// Integrates with the AutocompleteService to show Copilot-style inline suggestions.
    /// </summary>
    internal sealed class KiloInlineCompletionManager : IDisposable
    {
        private readonly IWpfTextView _textView;
        private readonly AutocompleteService _autocompleteService;
        private readonly KiloInlineGhostTextAdornment _adornment;
        private CancellationTokenSource? _currentRequestCts;
        private bool _disposed;
        private const int DebounceDelayMs = 500;

        public KiloInlineCompletionManager(
            IWpfTextView textView,
            AutocompleteService autocompleteService,
            KiloInlineGhostTextAdornment adornment)
        {
            _textView = textView ?? throw new ArgumentNullException(nameof(textView));
            _autocompleteService = autocompleteService ?? throw new ArgumentNullException(nameof(autocompleteService));
            _adornment = adornment ?? throw new ArgumentNullException(nameof(adornment));

            _textView.TextBuffer.Changed += OnTextBufferChanged;
            _textView.Caret.PositionChanged += OnCaretPositionChanged;
            _textView.Closed += OnTextViewClosed;
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (_disposed) return;

            // Cancel any pending request and dispose the old CTS
            CancelAndDisposeCurrentRequest();

            // Start new debounced request
            var cts = new CancellationTokenSource();
            _currentRequestCts = cts;
            var cancellationToken = cts.Token;

            // Capture position at the time of the change
            var snapshotPosition = _textView.Caret.Position.BufferPosition;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceDelayMs, cancellationToken).ConfigureAwait(false);

                    if (!_autocompleteService.InlineCompletionEnabled)
                        return;

                    // Must read editor state on the UI thread
                    SnapshotPoint position = default;
                    ITextSnapshot snapshot = null!;
                    string filePath = string.Empty;
                    string prefix = string.Empty;
                    string contextBefore = string.Empty;
                    string contextAfter = string.Empty;
                    int lineNumber = 0;
                    int columnPosition = 0;

                    await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                    if (_disposed || cancellationToken.IsCancellationRequested) return;

                    position = _textView.Caret.Position.BufferPosition;
                    snapshot = _textView.TextSnapshot;
                    var line = snapshot.GetLineFromPosition(position);
                    var lineText = line.GetText();
                    columnPosition = position.Position - line.Start.Position;
                    lineNumber = line.LineNumber;

                    filePath = GetFilePath(snapshot);
                    if (string.IsNullOrEmpty(filePath)) return;

                    prefix = columnPosition > 0 ? lineText.Substring(0, columnPosition) : string.Empty;

                    if (string.IsNullOrWhiteSpace(prefix) || prefix.TrimEnd().Length < 2) return;

                    var contextLinesBefore = Math.Min(20, line.LineNumber);
                    var contextLinesAfter = Math.Min(5, snapshot.LineCount - line.LineNumber - 1);
                    var startLine = line.LineNumber - contextLinesBefore;
                    var endLine = line.LineNumber + contextLinesAfter;

                    contextBefore = GetLinesText(snapshot, startLine, line.LineNumber - 1);
                    contextAfter = GetLinesText(snapshot, line.LineNumber + 1, endLine);

                    var textBeforeCursor = contextBefore + prefix;

                    // Switch back to background for the API call
                    await Task.Run(async () => { }).ConfigureAwait(false);

                    var result = await _autocompleteService.GetInlineCompletionAsync(
                        filePath,
                        lineNumber,
                        columnPosition,
                        textBeforeCursor,
                        contextAfter,
                        cancellationToken).ConfigureAwait(false);

                    if (cancellationToken.IsCancellationRequested || result == null || string.IsNullOrEmpty(result.Text))
                        return;

                    // Switch to UI thread to show the suggestion
                    await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                    if (_disposed || cancellationToken.IsCancellationRequested) return;

                    // Verify position hasn't changed
                    var currentPosition = _textView.Caret.Position.BufferPosition;
                    if (currentPosition == position)
                    {
                        _adornment.ShowSuggestion(result.Text, currentPosition);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when request is cancelled
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in inline completion: {ex.Message}");
                }
            }, cancellationToken);
        }

        private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            if (_adornment.IsVisible)
            {
                _adornment.HideSuggestion();
            }
            CancelAndDisposeCurrentRequest();
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            Dispose();
        }

        private void CancelAndDisposeCurrentRequest()
        {
            var old = _currentRequestCts;
            _currentRequestCts = null;
            if (old != null)
            {
                try
                {
                    if (!old.IsCancellationRequested)
                        old.Cancel();
                    old.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, safe to ignore
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CancelAndDisposeCurrentRequest();
            _textView.TextBuffer.Changed -= OnTextBufferChanged;
            _textView.Caret.PositionChanged -= OnCaretPositionChanged;
            _textView.Closed -= OnTextViewClosed;
        }

        private string GetFilePath(ITextSnapshot snapshot)
        {
            if (snapshot.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDocument))
            {
                return textDocument.FilePath;
            }
            return string.Empty;
        }

        private string GetLinesText(ITextSnapshot snapshot, int startLine, int endLine)
        {
            if (startLine < 0 || endLine >= snapshot.LineCount || startLine > endLine)
            {
                return string.Empty;
            }

            var lines = new System.Text.StringBuilder();
            for (int i = startLine; i <= endLine; i++)
            {
                lines.AppendLine(snapshot.GetLineFromLineNumber(i).GetText());
            }
            return lines.ToString();
        }
    }
}
