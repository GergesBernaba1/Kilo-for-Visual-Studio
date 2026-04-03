using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Kilo.VisualStudio.Extension
{
    /// <summary>
    /// Renders inline ghost text suggestions in the editor, similar to GitHub Copilot.
    /// Supports both single-line and multi-line suggestions.
    /// Shows translucent gray text that can be accepted with Tab or dismissed with Esc.
    /// </summary>
    internal sealed class KiloInlineGhostTextAdornment
    {
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly IWpfTextView _view;
        private UIElement? _adornmentElement;
        private SnapshotPoint? _currentPosition;
        private string? _currentSuggestion;
        private bool _isVisible;

        // Copilot-style colors
        private static readonly Brush GhostTextBrush = new SolidColorBrush(Color.FromArgb(102, 128, 128, 128));
        private static readonly Brush GhostTextBrushLight = new SolidColorBrush(Color.FromArgb(102, 64, 64, 64));

        static KiloInlineGhostTextAdornment()
        {
            GhostTextBrush.Freeze();
            GhostTextBrushLight.Freeze();
        }

        public KiloInlineGhostTextAdornment(IWpfTextView view, IAdornmentLayer adornmentLayer)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _adornmentLayer = adornmentLayer ?? throw new ArgumentNullException(nameof(adornmentLayer));

            _view.LayoutChanged += OnLayoutChanged;
            _view.Closed += OnViewClosed;
        }

        /// <summary>
        /// Show ghost text suggestion at the current cursor position.
        /// Supports multi-line suggestions: first line renders inline at cursor,
        /// additional lines stack below.
        /// </summary>
        public void ShowSuggestion(string suggestionText, SnapshotPoint position)
        {
            if (string.IsNullOrEmpty(suggestionText))
            {
                HideSuggestion();
                return;
            }

            _currentSuggestion = suggestionText;
            _currentPosition = position;
            _isVisible = true;

            RemoveCurrentAdornment();

            var brush = GetGhostTextBrush();
            var fontFamily = _view.FormattedLineSource.DefaultTextProperties.Typeface.FontFamily;
            var fontSize = _view.FormattedLineSource.DefaultTextProperties.FontRenderingEmSize;

            // Split suggestion into lines
            var lines = suggestionText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            UIElement element;
            if (lines.Length == 1)
            {
                // Single-line: just a TextBlock inline at cursor
                element = CreateGhostTextBlock(lines[0], brush, fontFamily, fontSize);
            }
            else
            {
                // Multi-line: first line inline, remaining lines stacked below
                var stack = new StackPanel();

                // First line — inline at cursor position
                stack.Children.Add(CreateGhostTextBlock(lines[0], brush, fontFamily, fontSize));

                // Remaining lines — full lines below
                for (int i = 1; i < lines.Length; i++)
                {
                    var lineBlock = new TextBlock
                    {
                        Text = lines[i],
                        Foreground = brush,
                        FontFamily = fontFamily,
                        FontSize = fontSize,
                        FontStyle = FontStyles.Italic,
                    };
                    stack.Children.Add(lineBlock);
                }

                element = stack;
            }

            element.Opacity = 0; // Start invisible for fade-in
            _adornmentElement = element;

            PositionAdornment();

            // Copilot-style smooth fade-in
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        /// <summary>
        /// Hide the current ghost text suggestion with a fade-out animation.
        /// </summary>
        public void HideSuggestion()
        {
            if (_adornmentElement != null && _isVisible)
            {
                var element = _adornmentElement;
                var fadeOut = new DoubleAnimation
                {
                    From = element.Opacity,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                fadeOut.Completed += (s, e) =>
                {
                    _adornmentLayer.RemoveAdornment(element);
                    if (_adornmentElement == element)
                        _adornmentElement = null;
                };

                element.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }

            _currentSuggestion = null;
            _currentPosition = null;
            _isVisible = false;
        }

        /// <summary>
        /// Accept the current suggestion and insert it into the document.
        /// </summary>
        public bool AcceptSuggestion()
        {
            if (!_isVisible || _currentSuggestion == null || !_currentPosition.HasValue)
            {
                return false;
            }

            try
            {
                var edit = _view.TextBuffer.CreateEdit();
                edit.Insert(_currentPosition.Value.Position, _currentSuggestion);
                edit.Apply();

                HideSuggestion();
                return true;
            }
            catch
            {
                HideSuggestion();
                return false;
            }
        }

        public bool IsVisible => _isVisible;

        public string? CurrentSuggestion => _currentSuggestion;

        private TextBlock CreateGhostTextBlock(string text, Brush brush, FontFamily fontFamily, double fontSize)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontFamily = fontFamily,
                FontSize = fontSize,
                FontStyle = FontStyles.Italic,
            };
        }

        private void RemoveCurrentAdornment()
        {
            if (_adornmentElement != null)
            {
                _adornmentLayer.RemoveAdornment(_adornmentElement);
                _adornmentElement = null;
            }
        }

        private void PositionAdornment()
        {
            if (_adornmentElement == null || !_currentPosition.HasValue)
                return;

            var snapshot = _view.TextSnapshot;
            if (_currentPosition.Value.Snapshot != snapshot)
            {
                try
                {
                    _currentPosition = _currentPosition.Value.TranslateTo(snapshot, PointTrackingMode.Positive);
                }
                catch (ArgumentException)
                {
                    // Position translation failed — old snapshot is incompatible
                    HideSuggestion();
                    return;
                }
            }

            var position = _currentPosition.Value;

            ITextViewLine line;
            try
            {
                line = _view.GetTextViewLineContainingBufferPosition(position);
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            if (line == null)
                return;

            var bounds = line.GetCharacterBounds(position);
            Canvas.SetLeft(_adornmentElement, bounds.Right);
            Canvas.SetTop(_adornmentElement, bounds.Top);

            _adornmentLayer.AddAdornment(
                AdornmentPositioningBehavior.TextRelative,
                new SnapshotSpan(position, 0),
                null,
                _adornmentElement,
                null
            );
        }

        private Brush GetGhostTextBrush()
        {
            var backgroundColor = _view.Background as SolidColorBrush;
            if (backgroundColor != null)
            {
                var brightness = (backgroundColor.Color.R + backgroundColor.Color.G + backgroundColor.Color.B) / 3.0;
                return brightness > 128 ? GhostTextBrushLight : GhostTextBrush;
            }
            return GhostTextBrush;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (_isVisible && _adornmentElement != null)
            {
                PositionAdornment();
            }
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            HideSuggestion();
            _view.LayoutChanged -= OnLayoutChanged;
            _view.Closed -= OnViewClosed;
        }
    }
}
