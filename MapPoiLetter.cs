using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;
using static System.Net.Mime.MediaTypeNames;
using System.Globalization;
using System.Windows.Documents;

namespace ARKInteractiveMap
{
    public class MapPoiLetter : MapPoi
    {
        protected ContextMenu menu_;
        protected Canvas canvas_;
        protected TextBlock textBlock_;
        protected TextBlock charDraw_;
        protected Viewbox viewbox_;
        protected Size size_;
        protected Point npos_;
        protected Point center_;

        public MapPoiLetter(MapPoiDef poi, MapScrollViewer map) : base(poi, map)
        {
        }

        public MapPoiLetter(ArkWikiJsonGroup group) : base(group)
        {
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
            charDraw_ = new TextBlock();
            charDraw_.Text = "?";
            charDraw_.FontSize = 40;
            charDraw_.FontWeight = FontWeights.UltraBold;
            charDraw_.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(poiDef.fillColor);
            viewbox_ = new Viewbox();
            viewbox_.Child = charDraw_;
            viewbox_.Stretch = Stretch.Uniform;
            viewbox_.Width = size.Width;
            viewbox_.Height = size.Height;
        }

        override public FrameworkElement BuildForContents(int new_size)
        {
            scale_ = 1;
            BuildBase(new Size(new_size - 6, new_size - 6));
            Canvas.SetLeft(charDraw_, 0);
            Canvas.SetTop(charDraw_, 0);
            return charDraw_;
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
            charDraw_.ContextMenu = menu_;
            canvas_.Children.Add(viewbox_);
            // Text
            textBlock_ = new TextBlock();
            textBlock_.Text = Label;
            textBlock_.FontSize = 20 * ComputeRatio();
            textBlock_.FontWeight = FontWeights.Bold;
            textBlock_.Foreground = charDraw_.Foreground;
            textBlock_.Background = new SolidColorBrush(Colors.Gray) { Opacity = 0.6 };
            canvas_.Children.Add(textBlock_);
            Rescale(scale);
            return canvas_;
        }

        protected (string, Brush) GetGroupInfo()
        {
            if (GroupName == "ingame-map-poi")
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
            map.Command((sender as MenuItem).Tag as string, this.Id);
        }

        override public FrameworkElement GetFrameworkElement() { return viewbox_; }
        override public void Update()
        {
            charDraw_.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(poiDef.fillColor);
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
            center_.X = size_.Width / 2;
            center_.Y = size_.Height / 2;
            ComputePos();
            viewbox_.Width = size_.Width;
            viewbox_.Height = size_.Height;
            Canvas.SetLeft(viewbox_, npos_.X);
            Canvas.SetTop(viewbox_, npos_.Y);
            // Texte centrer en haut du 'caractère'
            textBlock_.FontSize = 20 * ComputeRatio();
            textBlock_.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(textBlock_, pos.X * scale_ - textBlock_.DesiredSize.Width / 2);
            Canvas.SetTop(textBlock_, npos_.Y - textBlock_.DesiredSize.Height);
        }

        override public void RescalePopup()
        {
            RescalePopup(npos_.X + size_.Width / 2, npos_.Y);
        }

        override public bool GetVisible()
        {
            return canvas_.IsVisible;
        }
        override public void SetVisible(bool value)
        {
            canvas_.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            if (value == false)
            {
                HidePopup();
            }
        }

        override public void Ping()
        {
            map.Ping(size_.Width, npos_, this);
        }

        override public void RescalePing()
        {
            map.RescalePing(size_.Width, npos_);
        }
    }
}
