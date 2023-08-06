using System;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using Path = System.Windows.Shapes.Path;
using System.Windows.Shapes;
using System.Windows.Controls;

namespace ARKInteractiveMap
{
    public class MapPoiPie : MapPoi
    {
        protected Canvas canvas_;

        public MapPoiPie(MapPoiDef poi, MapScrollViewer map) : base(poi, map)
        {
            canvas_ = new Canvas();
            canvas_.Opacity = 0.85;
        }

        public MapPoiPie(ArkWikiJsonGroup group) : base(group)
        {
            canvas_ = new Canvas();
        }

        protected Point ComputeCirclePos()
        {
            return new Point(pos.X * scale_, pos.Y * scale_);
        }

        protected double ComputeCircleRatio()
        {
            // Equation résultant de la matrice [[1;1][6;(50/30)]] qui permet de changer le ratio d'un cercle
            // Scale = 1 => cercle = 30 => ratio 1
            // Scale = 6 => cercle = 50 => ratio 1.66667 = 50/30
            return 0.86667 + 0.13334 * scale_;
        }

        protected double ComputeCircleSize()
        {
            // Equation qui permet de convertir une taille de référence (voir json) en une taille en pixel
            return poiDef.size.width * 0.75;
        }

        protected (Point, double) ComputePosAndSize()
        {
            var npos = ComputeCirclePos();
            var size = ComputeCircleSize() * ComputeCircleRatio() / 2;
            return (npos, size);
        }

        protected Point GetPointOnCircle(Point center, double radius, double angle)
        {
            double radians = (angle - 90) * Math.PI / 180.0;
            double x = center.X + radius * Math.Cos(radians);
            double y = center.Y + radius * Math.Sin(radians);
            return new Point(x, y);
        }

        protected Path DrawPieSlice(Point center, double radius, double startAngle, double endAngle, Color color, SolidColorBrush stroke)
        {
            // Path(Fill) = PathGeometry => PathFigure => [LineSegment + ArcSegment]
            var path = new Path();
            path.Fill = new SolidColorBrush(color);
            path.Stroke = stroke;
            var pathGeometry = new PathGeometry();
            var pathFigure = new PathFigure();
            pathFigure.StartPoint = new Point(center.X, center.Y);
            pathFigure.Segments.Add(new LineSegment(GetPointOnCircle(center, radius, startAngle), true));
            var arcSegment = new ArcSegment();
            arcSegment.Point = GetPointOnCircle(center, radius, endAngle);
            arcSegment.Size = new Size(radius, radius);
            arcSegment.SweepDirection = SweepDirection.Clockwise;
            arcSegment.IsLargeArc = endAngle - startAngle > 180;
            pathFigure.Segments.Add(arcSegment);
            pathGeometry.Figures.Add(pathFigure);
            path.Data = pathGeometry;
            return path;
        }
        protected Color CodeToColor(Char c)
        {
            switch(c)
            {
                case 'b': return Colors.Blue;
                case 'g': return Colors.Green;
                case 'm': return Colors.Magenta;
                case 'p': return Colors.Purple;
                case 'r': return Colors.Red;
                case 'w': return Colors.White;
                case 'y': return Colors.Yellow;
            }
            return Colors.Black;
        }
        protected void DrawPie(Point center, double radius, SolidColorBrush stroke=null)
        {
            canvas_.Children.Clear();
            if (poiDef.fullGroupName == null)
            {
                // pré-def for contents list
                canvas_.Children.Add(DrawPieSlice(center, radius, 0, 120, Colors.White, stroke));
                canvas_.Children.Add(DrawPieSlice(center, radius, 120, 240, Colors.Green, stroke));
                canvas_.Children.Add(DrawPieSlice(center, radius, 240, 360, Colors.Blue, stroke));
            }
            else
            {
                // "surface-crate cg:27 cc:bgw" => cc:bgw => code couleur [blue-green-white]
                var split = poiDef.fullGroupName.Split(' ');
                if (split.Length == 3)
                {
                    var slice_colors = split[2].Substring(3);
                    int nb_slice = slice_colors.Length;
                    int angle_inc = 360 / nb_slice;
                    int angle = 0;
                    foreach (var c in slice_colors)
                    {
                        canvas_.Children.Add(DrawPieSlice(center, radius, angle, angle + angle_inc, CodeToColor(c), stroke));
                        angle += angle_inc;
                    }
                }
            }
        }

        override public FrameworkElement BuildForContents(int size)
        {
            canvas_.Opacity = 1;
            scale_ = 1;
            size -= 1;
            (var pos, var radius) = ComputePosAndSize();
            if (radius > size / 2)
            {
                radius = size / 2 - 2;
            }
            DrawPie(new Point(size / 2, size / 2), radius, Brushes.Black);
            return canvas_;
        }

        override public FrameworkElement BuildForMap(double scale)
        {
            scale_ = scale;
            (var pos, var radius) = ComputePosAndSize();
            DrawPie(pos, radius);
            canvas_.Cursor = Cursors.Hand;
            canvas_.Focusable = false;
            //canvas_.IsHitTestVisible = false; // element sans aucune interraction possible
            canvas_.Tag = this;
            canvas_.ToolTip = BuildToolTipInfo();
            canvas_.MouseLeftButtonDown += MouseLeftButtonDown;
            return canvas_;
        }
        override public FrameworkElement GetFrameworkElement() { return canvas_; }

        protected void MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RescalePopup();
        }

        override public void RescalePopup()
        {
            (var pos, var radius) = ComputePosAndSize();
            map.ViewPopup(this, pos.X, pos.Y - radius, Label, $"lat:{poiDef.pos.lat}, lon:{poiDef.pos.lon}");
        }

        override public void Rescale(double scale)
        {
            scale_ = scale;
            (var pos, var radius) = ComputePosAndSize();
            DrawPie(pos, radius);
        }

        override public (Point, double) GetCurrentPosAndSize()
        {
            (var pos, var radius) = ComputePosAndSize();
            return (pos, 0);
        }

        override public void SetVisible(bool value)
        {
            canvas_.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
