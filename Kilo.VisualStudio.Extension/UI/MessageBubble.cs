using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Kilo.VisualStudio.Extension.UI
{
    /// <summary>
    /// Enhanced message bubble control with Copilot-style animations and rendering
    /// </summary>
    public class MessageBubble : Border
    {
        private TextBlock? _textBlock;
        private LoadingAnimation? _loadingAnimation;
        private bool _isStreaming;

        public static readonly DependencyProperty MessageTextProperty =
            DependencyProperty.Register(nameof(MessageText), typeof(string), typeof(MessageBubble),
                new PropertyMetadata(string.Empty, OnMessageTextChanged));

        public static readonly DependencyProperty IsUserMessageProperty =
            DependencyProperty.Register(nameof(IsUserMessage), typeof(bool), typeof(MessageBubble),
                new PropertyMetadata(false, OnIsUserMessageChanged));

        public static readonly  DependencyProperty IsStreamingProperty =
            DependencyProperty.Register(nameof(IsStreaming), typeof(bool), typeof(MessageBubble),
                new PropertyMetadata(false, OnIsStreamingChanged));

        public string MessageText
        {
            get => (string)GetValue(MessageTextProperty);
            set => SetValue(MessageTextProperty, value);
        }

        public bool IsUserMessage
        {
            get => (bool)GetValue(IsUserMessageProperty);
            set => SetValue(IsUserMessageProperty, value);
        }

        public bool IsStreaming
        {
            get => (bool)GetValue(IsStreamingProperty);
            set => SetValue(IsStreamingProperty, value);
        }

        public MessageBubble()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void InitializeComponent()
        {
            // Default styling
            Padding = new Thickness(12, 8, 12, 8);
            Margin = new Thickness(12, 4, 12, 4);
            CornerRadius = new CornerRadius(12);
            MaxWidth = 380;
            
            // Create content
            var stack = new StackPanel();
            
            _textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                LineHeight = 20
            };
            stack.Children.Add(_textBlock);

            _loadingAnimation = new LoadingAnimation
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 4, 0, 0)
            };
            stack.Children.Add(_loadingAnimation);

            Child = stack;

            UpdateStyling();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Entrance animation
            PlayEntranceAnimation();
        }

        private void UpdateStyling()
        {
            if (IsUserMessage)
            {
                // User message styling (right-aligned, purple)
                Background = new SolidColorBrush(Color.FromRgb(110, 64, 201)); // Copilot purple
                HorizontalAlignment = HorizontalAlignment.Right;
                CornerRadius = new CornerRadius(12, 12, 2, 12);
                Margin = new Thickness(40, 4, 12, 4);

                if (_textBlock != null)
                {
                    _textBlock.Foreground = Brushes.White;
                }
            }
            else
            {
                // Assistant message styling (left-aligned, dark)
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                BorderThickness = new Thickness(1);
                HorizontalAlignment = HorizontalAlignment.Left;
                CornerRadius = new CornerRadius(12, 12, 12, 2);
                Margin = new Thickness(12, 4, 40, 4);

                if (_textBlock != null)
                {
                    _textBlock.Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238));
                }
            }
        }

        private void PlayEntranceAnimation()
        {
            // Fade in + slide up animation
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var slideUp = new ThicknessAnimation
            {
                From = new Thickness(Margin.Left, Margin.Top + 20, Margin.Right, Margin.Bottom),
                To = Margin,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(OpacityProperty, fadeIn);
            BeginAnimation(MarginProperty, slideUp);
        }

        /// <summary>
        /// Animate text appearance character by character (typewriter effect)
        /// </summary>
        public void AnimateTextTypewriter(string fullText, int delayMs = 10)
        {
            if (_textBlock == null) return;

            _textBlock.Text = string.Empty;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
            var currentIndex = 0;

            timer.Tick += (s, e) =>
            {
                if (currentIndex < fullText.Length)
                {
                    _textBlock.Text += fullText[currentIndex];
                    currentIndex++;
                }
                else
                {
                    timer.Stop();
                }
            };

            timer.Start();
        }

        private static void OnMessageTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MessageBubble bubble && bubble._textBlock != null)
            {
                bubble._textBlock.Text = e.NewValue as string ?? string.Empty;
            }
        }

        private static void OnIsUserMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MessageBubble bubble)
            {
                bubble.UpdateStyling();
            }
        }

        private static void OnIsStreamingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MessageBubble bubble && bubble._loadingAnimation != null)
            {
                bubble._isStreaming = (bool)e.NewValue;
                bubble._loadingAnimation.Visibility = bubble._isStreaming ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
