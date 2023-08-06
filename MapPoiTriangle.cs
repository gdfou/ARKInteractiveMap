using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;
using System.Windows.Interop;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;
using static System.Net.Mime.MediaTypeNames;
using System.Threading;
using System.Windows.Navigation;
using System.Reflection.Emit;

namespace ARKInteractiveMap
{
    public class MapPoiTriangle : MapPoi
    {
        protected PointCollection pointCollection_;
        protected Polygon triangle_;
        protected ContextMenu menu_;
        protected Canvas canvas_;
        protected TextBlock textBlock_;
        protected Size size_;
        protected Point npos_;
        protected Point center_;
        protected PoiOrientation orientation_;
        protected bool ingame_;

        public MapPoiTriangle(MapPoiDef poi, MapScrollViewer map, string param=null) : base(poi, map)
        {
            setOrientation(poi.groupName);
        }

        public MapPoiTriangle(ArkWikiJsonGroup group) : base(group)
        {
            setOrientation(group.groupName);
        }

        protected void setOrientation(string groupName)
        {
            ingame_ = (groupName == "ingame-map-poi");
            orientation_ = groupName == "ingame-map-poi" ? PoiOrientation.Up : PoiOrientation.Down;
        }

        protected double ComputeRatio()
        {
            // Scale 1 => icon  18 x  15 => consigne = 28 => ratio = 0.64 = 18/28
            // Scale 6 => icon  33 x  29 =>                  ratio = 1.17 = 33/28
            //          source 476 x 370 et icon size = 512 x 512
            return 0.534 + 0.106 * scale_;
        }

        protected void ComputeSize()
        {
            var ratio = ComputeRatio();
            size_ = new Size(poiDef.size.width * ratio, poiDef.size.width * ratio);
        }

        protected void ComputePos()
        {
            npos_ = new Point(pos.X * scale_ - center_.X, pos.Y * scale_ - center_.Y);
        }

        protected void BuildBase(Size size)
        {
            // Triangle => Polygone
            triangle_ = new Polygon();
            triangle_.Opacity = 0.85;
            triangle_.Fill = (SolidColorBrush)new BrushConverter().ConvertFrom(poiDef.fillColor);
            triangle_.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(poiDef.fillColor);
            pointCollection_ = new PointCollection();
            for(int i = 0; i < 3; i++)
            {
                pointCollection_.Add(new Point(0, 0));
            }
            ComputeTriangleSize(size);
            triangle_.Points = pointCollection_;
        }

        protected void ComputeTriangleCenter()
        {
            center_ = new Point();
            foreach (Point point in pointCollection_)
            {
                center_.X += point.X;
                center_.Y += point.Y;
            }
            center_.X /= 3;
            center_.Y /= 3;
        }

        protected void ComputeTriangleSize(Size size)
        {
            switch (orientation_)
            {
                case PoiOrientation.Up:
                    {
                        pointCollection_[0] = new Point(size.Width / 2, 0);
                        pointCollection_[1] = new Point(size.Width, size.Height);
                        pointCollection_[2] = new Point(0, size.Height);
                        break;
                    }

                case PoiOrientation.Down:
                    {
                        pointCollection_[0] = new Point(0, 0);
                        pointCollection_[1] = new Point(size.Width, 0);
                        pointCollection_[2] = new Point(size.Width / 2, size.Height);
                        break;
                    }

                case PoiOrientation.Left:
                    {
                        pointCollection_[0] = new Point(size.Width, 0);
                        pointCollection_[1] = new Point(size.Width, size.Height);
                        pointCollection_[2] = new Point(0, size.Height / 2);
                        break;
                    }

                case PoiOrientation.Right:
                    {
                        pointCollection_[0] = new Point(0, 0);
                        pointCollection_[1] = new Point(size.Width, size.Height / 2);
                        pointCollection_[2] = new Point(0, size.Height);
                        break;
                    }
            }
            ComputeTriangleCenter();
        }

        override public FrameworkElement BuildForContents(int new_size)
        {
            scale_ = 1;
            BuildBase(new Size(new_size - 6, new_size - 6));
            Canvas.SetLeft(triangle_, 0);
            Canvas.SetTop(triangle_, 0);
            triangle_.Stroke = Brushes.Black;
            return triangle_;
        }

        override public FrameworkElement BuildForMap(double scale)
        {
            scale_ = scale;
            ComputeSize();
            BuildBase(size_);
            canvas_ = new Canvas();
            canvas_.Opacity = 0.85;
            canvas_.Cursor = Cursors.Hand;
            canvas_.Focusable = false;
            //triangle_.IsHitTestVisible = false; // element sans aucune interraction possible
            canvas_.Tag = this;
            canvas_.ToolTip = BuildToolTipInfo();
            canvas_.MouseLeftButtonDown += MouseLeftButtonDown;
            // Menu
            menu_ = new ContextMenu();
            menu_.Items.Add(new MenuItem()
            {
                Header = "Editer",
                Tag = "IngameMarkerEdit",
            });
            menu_.Items.Add(new MenuItem()
            {
                Header = "Supprimer",
                Tag = "IngameMarkerDel",
            });
            menu_.Items.Add(new MenuItem()
            {
                Header = "Ajouter",
                Tag = "IngameMarkerAdd",
            });
            foreach (MenuItem item in menu_.Items)
            {
                item.Click += Menu_Click;
            }
            triangle_.ContextMenu = menu_;
            canvas_.Children.Add(triangle_);
            // Text
            textBlock_ = new TextBlock();
            textBlock_.Text = Label;
            textBlock_.FontSize = 20;
            textBlock_.FontWeight = FontWeights.Bold;
            textBlock_.Foreground = triangle_.Fill;
            textBlock_.Background = new SolidColorBrush(Colors.Gray) { Opacity = 0.6 };
            canvas_.Children.Add(textBlock_);
            Rescale(scale);
            return canvas_;
        }

        protected (string, Brush) GetGroupInfo()
        {
            if (ingame_)
                return ("Repère Ingame", Brushes.Red);
            else
                return ("Repère libre", Brushes.Blue);
        }

        override public FrameworkElement BuildToolTipInfo()
        {
            // Formatage étendu possible avec TextBlock voir
            // https://wpf-tutorial.com/fr/15/les-controles-de-base/le-controle-textblock-formatage-inline/
            var stackPanel = new StackPanel();
            stackPanel.Orientation = Orientation.Vertical;
            (var gpText, var gpBrush) = GetGroupInfo();
            stackPanel.Children.Add(new TextBlock() { Text = gpText, Foreground = gpBrush });
            stackPanel.Children.Add(new TextBlock() { Text = Label, FontWeight = FontWeights.Bold });
            stackPanel.Children.Add(new TextBlock() { Text = $"lat:{poiDef.pos.lat}, lon:{poiDef.pos.lon}" });
            return stackPanel;
        }

        private void Menu_Click(object sender, RoutedEventArgs e)
        {
            string cmd = (sender as MenuItem).Tag as string;
            if (!ingame_)
            {
                cmd += ":user";
            }
            map.Command(cmd, this.Id);
        }

        override public FrameworkElement GetFrameworkElement() { return triangle_; }
        override public void Update()
        {
            triangle_.Fill = (SolidColorBrush)new BrushConverter().ConvertFrom(poiDef.fillColor);
            triangle_.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(poiDef.fillColor);
            textBlock_.Foreground = triangle_.Fill;
            textBlock_.Text = Label;
            Rescale(scale_);
            RescalePopup();
        }

        protected void MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var list = new List<FrameworkElement>();
            (var gpText, var gpBrush) = GetGroupInfo();
            var popupGroup = new TextBlock()
            {
                FontSize = 16,
                FontFamily = new FontFamily("Verdana"),
                FontWeight = FontWeights.Black,
                FontStretch = FontStretches.Medium,
                Foreground = gpBrush,
                Text = gpText
            };
            list.Add(popupGroup);
            var popupLabel = new TextBlock()
            {
                FontSize = 16,
                FontFamily = new FontFamily("Verdana"),
                FontWeight = FontWeights.Black,
                FontStretch = FontStretches.Medium,
                Text = Label
            };
            list.Add(popupLabel);
            var popupInfo = new TextBlock()
            {
                FontSize = 14,
                FontFamily = new FontFamily("Verdana"),
                Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#757575"),
                Text = $"lat:{poiDef.pos.lat}, lon:{poiDef.pos.lon}"
            };
            list.Add(popupInfo);
            map.mapPopup.ViewPopup(this, npos_.X + size_.Width / 2, npos_.Y, list);
        }

        override public void Rescale(double scale)
        {
            scale_ = scale;
            ComputeSize();
            ComputeTriangleSize(size_);
            ComputePos();
            Canvas.SetLeft(triangle_, npos_.X);
            Canvas.SetTop(triangle_, npos_.Y);
            // Texte centrer en haut du triangle
            textBlock_.FontSize = 20 * ComputeRatio();
            textBlock_.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(textBlock_, pos.X * scale_ - textBlock_.DesiredSize.Width / 2);
            Canvas.SetTop(textBlock_, npos_.Y - textBlock_.DesiredSize.Height);
        }

        override public (Point, double) GetCurrentPosAndSize()
        {
            return (npos_, size_.Width);
        }

        override public void RescalePopup()
        {
            RescalePopup(npos_.X + size_.Width / 2, npos_.Y);
        }

        override public void SetVisible(bool value)
        {
            canvas_.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            if (value == false)
            {
                HidePopup();
            }
        }
    }
}
