using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows;

namespace ARKInteractiveMap
{
    public class MapPoiIcon : MapPoi
    {
        protected Image imagePoi_;
        protected bool collected_;

        public MapPoiIcon(MapPoiDef poi, MapScrollViewer map) : base(poi, map)
        {
        }

        public MapPoiIcon(ArkWikiJsonGroup group) : base(group)
        {
        }

        protected double computeIconRatio()
        {
            // Scale 1 => icon  18 x  15 => consigne = 28 => ratio = 0.64 = 18/28
            // Scale 6 => icon  33 x  29 =>                  ratio = 1.17 = 33/28
            //          source 476 x 370 et icon size = 512 x 512
            return 0.534 + 0.106 * scale_;
        }

        protected (double, Point) ComputeIconPosAndWidth()
        {
            var size = (collected_ && poiDef.sizeCollected != null ? poiDef.sizeCollected.width : poiDef.size.width) * computeIconRatio();
            var npos = new Point(pos.X * scale_ - size / 2, pos.Y * scale_ - size / 2);
            return (size, npos);
        }

        override public FrameworkElement BuildForContents(int size)
        {
            if (poiDef.iconCollected != null)
            {
                scale_ = 1;
                imagePoi_ = new Image();
                BitmapImage bitmapPoi = new BitmapImage();
                bitmapPoi.BeginInit();
                bitmapPoi.StreamSource = Assembly.GetExecutingAssembly().GetManifestResourceStream(poiDef.iconCollected);
                bitmapPoi.EndInit();
                imagePoi_.Source = bitmapPoi;
                imagePoi_.Width = size;
                imagePoi_.Height = size;
                Canvas.SetLeft(imagePoi_, 0);
                Canvas.SetTop(imagePoi_, 0);
                return imagePoi_;
            }
            return null;
        }

        override public FrameworkElement BuildForMap(double scale)
        {
            scale_ = scale;
            imagePoi_ = new Image();
            setImage();
            imagePoi_.Cursor = Cursors.Hand;
            imagePoi_.Tag = this;
            imagePoi_.ToolTip = BuildToolTipInfo();
            imagePoi_.MouseRightButtonDown += MouseRightButtonDown;
            imagePoi_.MouseLeftButtonDown += MouseLeftButtonDown;
            return imagePoi_;
        }
        override public FrameworkElement GetFrameworkElement() { return imagePoi_; }

        protected void setImage()
        {
            BitmapImage bitmapPoi = new BitmapImage();
            bitmapPoi.BeginInit();
            if (collected_ && poiDef.iconCollected != null)
            {
                bitmapPoi.StreamSource = Assembly.GetExecutingAssembly().GetManifestResourceStream(poiDef.iconCollected);
            }
            else
            {
                bitmapPoi.StreamSource = Assembly.GetExecutingAssembly().GetManifestResourceStream(poiDef.icon);
            }
            bitmapPoi.EndInit();
            imagePoi_.Source = bitmapPoi;
            if (poiDef.iconCollected == null)
                imagePoi_.Opacity = collected_ ? 0.4 : 0.85;
            else
                imagePoi_.Opacity = collected_ ? 1 : 0.85;
            (var width, var pos) = ComputeIconPosAndWidth();
            imagePoi_.Width = width;
            imagePoi_.Height = width;
            Canvas.SetLeft(imagePoi_, pos.X);
            Canvas.SetTop(imagePoi_, pos.Y);
        }

        protected void MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (poiDef.isCollectible)
            {
                collected_ = !collected_;
                setImage();
                map.CallUpdateCollectedEvent(this, Id, collected_);
            }
        }

        protected void MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RescalePopup();
        }

        override public void RescalePopup()
        {
            (var width, var pos) = ComputeIconPosAndWidth();
            map.ViewPopup(this, pos.X + width / 2, pos.Y, Label, $"lat:{poiDef.pos.lat}, lon:{poiDef.pos.lon}");
        }

        override public void Rescale(double scale)
        {
            scale_ = scale;
            (var width, var pos) = ComputeIconPosAndWidth();
            Canvas.SetLeft(imagePoi_, pos.X);
            Canvas.SetTop(imagePoi_, pos.Y);
            imagePoi_.Width = width;
            imagePoi_.Height = width;
        }

        override public bool GetVisible()
        {
            return imagePoi_.IsVisible;
        }
        override public void SetVisible(bool value)
        {
            imagePoi_.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        override public bool GetCollected() { return collected_; }
        override public void SetCollected(bool value)
        {
            collected_ = value;
            setImage();
        }

        override public void Ping()
        {
            (var width, var pos) = ComputeIconPosAndWidth();
            map.Ping(width, pos, this);
        }

        override public void RescalePing()
        {
            (var width, var pos) = ComputeIconPosAndWidth();
            map.RescalePing(width, pos);
        }
    }
}
