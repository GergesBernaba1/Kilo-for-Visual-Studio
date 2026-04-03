using System;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;

namespace Kilo.VisualStudio.Extension
{
    /// <summary>
    /// Controller for handling keyboard interactions with inline ghost text.
    /// Handles Tab to accept and Esc to dismiss, similar to GitHub Copilot.
    /// </summary>
    internal sealed class KiloInlineGhostTextController
    {
        private readonly IWpfTextView _textView;
        private readonly KiloInlineGhostTextAdornment _adornment;

        public KiloInlineGhostTextController(IWpfTextView textView, KiloInlineGhostTextAdornment adornment)
        {
            _textView = textView ?? throw new ArgumentNullException(nameof(textView));
            _adornment = adornment ?? throw new ArgumentNullException(nameof(adornment));

            // Hook into keyboard events
            _textView.VisualElement.PreviewKeyDown += OnPreviewKeyDown;
            _textView.Closed += OnTextViewClosed;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_adornment.IsVisible)
            {
                return;
            }

            // Tab key - Accept suggestion (like Copilot)
            if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (_adornment.AcceptSuggestion())
                {
                    e.Handled = true;
                }
            }
            // Escape key - Dismiss suggestion
            else if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                _adornment.HideSuggestion();
                e.Handled = true;
            }
            // Arrow keys, typing, or other keys - Dismiss suggestion
            else if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down ||
                     e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Enter || e.Key == Key.Space ||
                     IsTypingKey(e.Key))
            {
                _adornment.HideSuggestion();
            }
        }

        private bool IsTypingKey(Key key)
        {
            // Check if the key is a character key (A-Z, 0-9, punctuation, etc.)
            return (key >= Key.A && key <= Key.Z) ||
                   (key >= Key.D0 && key <= Key.D9) ||
                   (key >= Key.NumPad0 && key <= Key.NumPad9) ||
                   key == Key.OemPeriod || key == Key.OemComma || key == Key.OemSemicolon ||
                   key == Key.OemQuestion || key == Key.OemPlus || key == Key.OemMinus ||
                   key == Key.OemOpenBrackets || key == Key.OemCloseBrackets ||
                   key == Key.OemQuotes || key == Key.OemBackslash || key == Key.OemPipe;
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            _textView.VisualElement.PreviewKeyDown -= OnPreviewKeyDown;
            _textView.Closed -= OnTextViewClosed;
        }
    }
}
