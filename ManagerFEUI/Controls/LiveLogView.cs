using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ManagerFEUI.Controls
{
    public class LiveLogView : UserControl
    {
        private TextBox? _logBox;
        private ScrollViewer? _scrollViewer;

        public static readonly DependencyProperty LogLinesProperty =
            DependencyProperty.Register(nameof(LogLines), typeof(System.Collections.IEnumerable), typeof(LiveLogView),
                new PropertyMetadata(null, OnLogLinesChanged));

        public static readonly DependencyProperty AutoScrollProperty =
            DependencyProperty.Register(nameof(AutoScroll), typeof(bool), typeof(LiveLogView),
                new PropertyMetadata(true));

        public System.Collections.IEnumerable LogLines
        {
            get => (System.Collections.IEnumerable)GetValue(LogLinesProperty);
            set => SetValue(LogLinesProperty, value);
        }

        public bool AutoScroll
        {
            get => (bool)GetValue(AutoScrollProperty);
            set => SetValue(AutoScrollProperty, value);
        }

        private static void OnLogLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LiveLogView v) v.UpdateLog();
        }

        public LiveLogView()
        {
            var border = new Border
            {
                Background = BrushExtensions.FromHex("#0d1512"),
                BorderBrush = BrushExtensions.FromHex("#1e2e26"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
            };

            _scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(12),
            };

            _logBox = new TextBox
            {
                IsReadOnly = true,
                Background = Brushes.Transparent,
                Foreground = BrushExtensions.FromHex("#7a9488"),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("JetBrains Mono"),
                FontSize = 11,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };

            _scrollViewer.Content = _logBox;
            border.Child = _scrollViewer;
            Content = border;
        }

        private void UpdateLog()
        {
            if (_logBox == null || LogLines == null) return;

            var lines = new System.Text.StringBuilder();
            foreach (var item in LogLines)
            {
                if (item is string s)
                {
                    lines.Append(s);
                    lines.AppendLine();
                }
            }

            _logBox.Text = lines.ToString();

            if (AutoScroll && _scrollViewer != null)
            {
                _scrollViewer.ScrollToEnd();
            }
        }
    }
}