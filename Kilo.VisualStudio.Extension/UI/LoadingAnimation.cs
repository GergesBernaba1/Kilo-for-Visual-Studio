using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Kilo.VisualStudio.Extension.UI
{
    /// <summary>
    /// Copilot-style loading animation with three pulsing dots.
    /// Uses direct object references instead of RegisterName to avoid issues
    /// in VS2022 tool window name scope contexts.
    /// </summary>
    public class LoadingAnimation : UserControl
    {
        private Storyboard? _animationStoryboard;
        private readonly Ellipse[] _dots = new Ellipse[3];

        public LoadingAnimation()
        {
            InitializeComponent();
            Loaded += OnLoadedAnimation;
            Unloaded += OnUnloaded;
        }

        private void InitializeComponent()
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            for (int i = 0; i < 3; i++)
            {
                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(Color.FromRgb(110, 64, 201)), // Copilot purple
                    Margin = new Thickness(3),
                    RenderTransform = new ScaleTransform(1.0, 1.0),
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                _dots[i] = dot;
                stack.Children.Add(dot);
            }

            Content = stack;
        }

        private void OnLoadedAnimation(object sender, RoutedEventArgs e)
        {
            StartAnimation();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();
        }

        private void StartAnimation()
        {
            _animationStoryboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            for (int i = 0; i < 3; i++)
            {
                var dot = _dots[i];
                var stagger = TimeSpan.FromMilliseconds(i * 200);

                // Opacity pulse
                var opacityAnimation = new DoubleAnimation
                {
                    From = 0.3,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(600),
                    AutoReverse = true,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                    BeginTime = stagger
                };
                Storyboard.SetTarget(opacityAnimation, dot);
                Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
                _animationStoryboard.Children.Add(opacityAnimation);

                // Scale X pulse
                var scaleXAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 1.2,
                    Duration = TimeSpan.FromMilliseconds(600),
                    AutoReverse = true,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                    BeginTime = stagger
                };
                Storyboard.SetTarget(scaleXAnimation, dot);
                Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.ScaleX"));
                _animationStoryboard.Children.Add(scaleXAnimation);

                // Scale Y pulse
                var scaleYAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 1.2,
                    Duration = TimeSpan.FromMilliseconds(600),
                    AutoReverse = true,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                    BeginTime = stagger
                };
                Storyboard.SetTarget(scaleYAnimation, dot);
                Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.ScaleY"));
                _animationStoryboard.Children.Add(scaleYAnimation);
            }

            _animationStoryboard.Begin();
        }

        public void StopAnimation()
        {
            _animationStoryboard?.Stop();
            _animationStoryboard = null;
        }
    }
}
