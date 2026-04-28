using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ManagerFEUI.Controls
{
    public class MetricCard : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(MetricCard));

        public static readonly DependencyProperty PercentageProperty =
            DependencyProperty.Register(nameof(Percentage), typeof(double), typeof(MetricCard),
                new PropertyMetadata(0.0, OnPercentageChanged));

        public static readonly DependencyProperty DisplayValueProperty =
            DependencyProperty.Register(nameof(DisplayValue), typeof(string), typeof(MetricCard));

        public static readonly DependencyProperty SparkDataProperty =
            DependencyProperty.Register(nameof(SparkData), typeof(IEnumerable), typeof(MetricCard),
                new PropertyMetadata(null, OnSparkDataChanged));

        private Path? _bgArc;
        private Path? _fgArc;

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public double Percentage
        {
            get => (double)GetValue(PercentageProperty);
            set => SetValue(PercentageProperty, value);
        }

        public string DisplayValue
        {
            get => (string)GetValue(DisplayValueProperty);
            set => SetValue(DisplayValueProperty, value);
        }

        public IEnumerable SparkData
        {
            get => (IEnumerable)GetValue(SparkDataProperty);
            set => SetValue(SparkDataProperty, value);
        }

        private static void OnPercentageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MetricCard card) card.UpdateArc();
        }

        private static void OnSparkDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Sparkline update placeholder
        }

        public MetricCard()
        {
            var root = new Grid();

            // Outer card border with padding
            var card = new Border
            {
                Background = BrushExtensions.FromHex("#121a16"),
                BorderBrush = BrushExtensions.FromHex("#1e2e26"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
            };

            // Inner grid
            var innerGrid = new Grid();

            // Row 0: title (auto height)
            innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            // Row 1: gauge + value (star, fills remaining space)
            innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            // Row 2: sparkline area (fixed height)
            innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30, GridUnitType.Pixel) });

            // --- Title ---
            var titleText = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushExtensions.FromHex("#7a9488"),
                Margin = new Thickness(0, 0, 0, 12),
            };
            titleText.SetBinding(TextBlock.TextProperty, new Binding(nameof(Title)) { Source = this });
            Grid.SetRow(titleText, 0);
            innerGrid.Children.Add(titleText);

            // --- Gauge row (gauge left, value right) ---
            var gaugeGrid = new Grid();
            gaugeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gaugeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gaugeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            gaugeGrid.VerticalAlignment = VerticalAlignment.Stretch;
            gaugeGrid.HorizontalAlignment = HorizontalAlignment.Stretch;

            // Left column: gauge canvas centered
            var gaugeCanvas = new Canvas
            {
                Width = 80,
                Height = 80,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // Center the semicircle in the 80x80 canvas
            double cx = 40, cy = 65, r = 30;

            // Background semicircle (100%)
            _bgArc = CreateSemiArc(cx, cy, r, 1.0);
            _bgArc.Stroke = BrushExtensions.FromHex("#1e2e26");
            _bgArc.StrokeThickness = 6;
            _bgArc.StrokeStartLineCap = PenLineCap.Round;
            _bgArc.StrokeEndLineCap = PenLineCap.Round;
            gaugeCanvas.Children.Add(_bgArc);

            // Foreground arc (current percentage)
            _fgArc = CreateSemiArc(cx, cy, r, 0.0);
            _fgArc.Stroke = BrushExtensions.FromHex("#2dd4a8");
            _fgArc.StrokeThickness = 6;
            _fgArc.StrokeStartLineCap = PenLineCap.Round;
            _fgArc.StrokeEndLineCap = PenLineCap.Round;
            gaugeCanvas.Children.Add(_fgArc);

            Grid.SetColumn(gaugeCanvas, 0);
            Grid.SetRow(gaugeCanvas, 0);
            gaugeGrid.Children.Add(gaugeCanvas);

            // Right column: value text centered in its column
            var valueText = new TextBlock
            {
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = BrushExtensions.FromHex("#e8f0ec"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
            };
            valueText.SetBinding(TextBlock.TextProperty, new Binding(nameof(DisplayValue)) { Source = this });
            Grid.SetColumn(valueText, 1);
            Grid.SetRow(valueText, 0);
            gaugeGrid.Children.Add(valueText);

            Grid.SetRow(gaugeGrid, 1);
            innerGrid.Children.Add(gaugeGrid);

            // --- Sparkline placeholder ---
            var sparkSpacer = new Border
            {
                Background = Brushes.Transparent,
                Height = 30,
                Margin = new Thickness(0, 8, 0, 0),
            };
            Grid.SetRow(sparkSpacer, 2);
            innerGrid.Children.Add(sparkSpacer);

            card.Child = innerGrid;
            root.Children.Add(card);
            Content = root;
        }

        private void UpdateArc()
        {
            if (_fgArc == null) return;
            var pct = Math.Clamp(Percentage / 100.0, 0.0, 1.0);
            
            // Hide arc entirely at 0% to avoid showing a dot at the start point
            if (pct < 0.005)
            {
                _fgArc.Visibility = Visibility.Hidden;
                return;
            }
            
            _fgArc.Visibility = Visibility.Visible;
            _fgArc.Data = CreateSemiGeometry(40, 65, 30, pct);
        }

        private static Path CreateSemiArc(double cx, double cy, double r, double pct)
        {
            var path = new Path();
            path.Data = CreateSemiGeometry(cx, cy, r, pct);
            return path;
        }

        private static Geometry CreateSemiGeometry(double cx, double cy, double r, double pct)
        {
            // Start at left end of semicircle (180 degrees)
            var startPt = new Point(cx - r, cy);
            // Sweep counterclockwise upward by pct * 180 degrees
            var sweepAngle = 180.0 * Math.Clamp(pct, 0.0, 1.0);
            // End angle in standard math coords (0 = right, increasing CCW)
            var endAngleDeg = 180.0 - sweepAngle;
            var endRad = endAngleDeg * Math.PI / 180.0;
            var endPt = new Point(cx + r * Math.Cos(endRad), cy - r * Math.Sin(endRad));
            var largeArc = sweepAngle > 180.0;

            var seg = new ArcSegment(endPt, new Size(r, r), 0.0, largeArc, SweepDirection.Counterclockwise, true);
            var fig = new PathFigure { StartPoint = startPt };
            fig.Segments.Add(seg);
            fig.IsClosed = false;
            var geom = new PathGeometry { Figures = { fig } };
            return geom;
        }
    }

    public static class BrushExtensions
    {
        public static SolidColorBrush FromHex(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        }
    }
}