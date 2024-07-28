using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ARKInteractiveMap
{
    public class MapPoiEllipse : MapPoi
    {
        protected EllipseGeometry ellipse_;
        protected Path geometryPoi_;

        public MapPoiEllipse(MapPoiDef poi, MapScrollViewer map) : base(poi, map)
        {
        }

        public MapPoiEllipse(ArkWikiJsonGroup group) : base(group)
        {
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
            // y = 6.55172413793 + 1.1724137931 * x
            // 20   => 30
            //  7   => 15
            //  5.5 => 13
            return 6.55172413793 + 1.1724137931 * poiDef.size.width;
        }

        protected double ComputeCircleRadius()
        {
            return ComputeCircleSize() * ComputeCircleRatio() / 2;
        }

        protected void BuildBase()
        {
            // Ellipse
            ellipse_ = new EllipseGeometry();
            // Path
            geometryPoi_ = new Path();
            geometryPoi_.Opacity = 0.75;
            geometryPoi_.Fill = (SolidColorBrush)new BrushConverter().ConvertFrom(poiDef.fillColor);
            geometryPoi_.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(poiDef.borderColor);
            geometryPoi_.StrokeThickness = 1;
            geometryPoi_.Data = ellipse_;
        }

        override public FrameworkElement BuildForContents(int size)
        {
            scale_ = 1;
            BuildBase();
            // Ellipse
            double radius = ComputeCircleRadius();
            if (radius > size / 2)
            {
                radius = size / 2 - 2;
            }
            ellipse_.RadiusX = radius;
            ellipse_.RadiusY = radius;
            ellipse_.Center = new Point(size / 2, size / 2);
            geometryPoi_.Stroke = Brushes.Black;
            return geometryPoi_;
        }

        override public FrameworkElement BuildForMap(double scale)
        {
            scale_ = scale;
            BuildBase();
            ellipse_.Center = ComputeCirclePos();
            double radius = ComputeCircleRadius();
            ellipse_.RadiusX = radius;
            ellipse_.RadiusY = radius;
            geometryPoi_.Cursor = Cursors.Hand;
            geometryPoi_.Focusable = false;
            //geometryPoi.IsHitTestVisible = false; // element sans aucune interraction possible
            geometryPoi_.Tag = this;
            geometryPoi_.ToolTip = BuildToolTipInfo();
            geometryPoi_.MouseLeftButtonDown += MouseLeftButtonDown;
            return geometryPoi_;
        }
        override public FrameworkElement GetFrameworkElement() { return geometryPoi_; }

        protected void MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RescalePopup();
        }

        override public void RescalePopup()
        {
            var pos = ComputeCirclePos();
            double radius = ComputeCircleRadius();
            map.ViewPopup(this, pos.X, pos.Y - radius, Label, $"lat:{poiDef.pos.lat}, lon:{poiDef.pos.lon}");
        }

        override public void Rescale(double scale)
        {
            scale_ = scale;
            ellipse_.Center = ComputeCirclePos();
            double radius = ComputeCircleRadius();
            ellipse_.RadiusX = radius;
            ellipse_.RadiusY = radius;
        }

        override public (Point, double) GetCurrentPosAndSize()
        {
            return (ComputeCirclePos(), 0);
        }

        override public void SetVisible(bool value)
        {
            geometryPoi_.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
