using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ManagerFEUI.Models;

namespace ManagerFEUI.Controls
{
    public class SparklineView : Canvas
    {
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(nameof(Data), typeof(System.Collections.IEnumerable), typeof(SparklineView),
                new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register(nameof(Color), typeof(Brush), typeof(SparklineView),
                new PropertyMetadata(BrushExtensions.FromHex("#2dd4a8")));

        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register(nameof(FillArea), typeof(bool), typeof(SparklineView),
                new PropertyMetadata(false));

        private Polyline? _line;
        private Polygon? _fill;

        public System.Collections.IEnumerable Data
        {
            get => (System.Collections.IEnumerable)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public Brush Color
        {
            get => (Brush)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public bool FillArea
        {
            get => (bool)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SparklineView v) v.Render();
        }

        public SparklineView()
        {
            SizeChanged += (s, e) => Render();
        }

        private void Render()
        {
            if (Data == null) return;

            var list = new List<double>();
            foreach (var item in Data)
            {
                if (item is MetricPoint mp) list.Add(mp.Value);
                else if (item is double d) list.Add(d);
            }

            if (list.Count < 2) return;

            var width = ActualWidth > 0 ? ActualWidth : 200;
            var height = ActualHeight > 0 ? ActualHeight : 40;
            var padding = 2.0;
            var drawW = width - padding * 2;
            var drawH = height - padding * 2;

            var min = list.Min();
            var max = list.Max();
            var range = max - min;
            if (range == 0) range = 1;

            var points = new List<Point>();
            for (int i = 0; i < list.Count; i++)
            {
                var x = padding + (i / (list.Count - 1.0)) * drawW;
                var y = padding + drawH - ((list[i] - min) / range) * drawH;
                points.Add(new Point(x, y));
            }



            // Remove old shapes
            Children.Clear();

            _line = new Polyline
            {
                Points = new PointCollection(points),
                Stroke = Color,
                StrokeThickness = 1.5,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = null
            };

            if (FillArea)
            {
                var fillPoints = new List<Point>(points);
                fillPoints.Add(new Point(width - padding, height));
                fillPoints.Add(new Point(padding, height));

                Color fillStartColor = Colors.Transparent;
                if (Color is SolidColorBrush scb)
                    fillStartColor = scb.Color;

                _fill = new Polygon
                {
                    Points = new PointCollection(fillPoints),
                    Fill = new LinearGradientBrush(fillStartColor, Colors.Transparent, new Point(0, 0), new Point(0, 1))
                    { Opacity = 0.15 }
                };
            }

            if (_fill != null) Children.Add(_fill);
            Children.Add(_line);
        }

    }
}