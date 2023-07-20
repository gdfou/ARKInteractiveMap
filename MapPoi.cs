using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Controls.Primitives;

namespace ARKInteractiveMap
{
    public enum PoiOrientation
    {
        Up,
        Down,
        Left,
        Right
    }

    public abstract class MapPoi
    {
        protected MapScrollViewer map;
        public MapPoiDef poiDef;
        public Point pos;   // pos en pixel
        protected double scale_;

        public string GroupName
        {
            get { return poiDef.groupName; }
        }

        public string Label
        {
            get { return poiDef.Label; }
            set { poiDef.Label = value; }
        }

        public string Id
        {
            get { return poiDef.Id; }
        }

        public bool Visible
        {
            get { return GetVisible(); }
            set { SetVisible(value); }
        }

        public bool Collected
        {
            get { return GetCollected(); }
            set { SetCollected(value); }
        }

        public bool Editable
        {
            get { return GetEditable(); }
            set { SetEditable(value); }
        }

        public MapPos MapPos
        {
            get { return poiDef.pos; }
            set
            {
                poiDef.pos = value;
                this.pos = map.MapSize.ConvertMapPointToPixel(poiDef.pos);
            }
        }

        public string FillColor
        {
            get { return poiDef.fillColor; }
            set
            {
                poiDef.fillColor = value;
            }
        }

        public MapPoi()
        {
            scale_ = 1;
        }

        public MapPoi(ArkWikiJsonGroup group) : this()
        {
            poiDef = new MapPoiDef();
            poiDef.size = group.size;
            poiDef.borderColor = group.borderColor;
            poiDef.fillColor = group.fillColor;
            poiDef.icon = MapPoiDef.getIconResname(group.icon);
            poiDef.iconCollected = MapPoiDef.getIconResname(group.iconCollected);
        }

        public MapPoi(MapPoiDef poi, MapScrollViewer map) : this()
        {
            poiDef = poi;
            this.map = map;
            this.pos = map.MapSize.ConvertMapPointToPixel(poi.pos);
        }

        virtual public FrameworkElement BuildToolTipInfo()
        {
            // Formatage étendu possible avec TextBlock voir
            // https://wpf-tutorial.com/fr/15/les-controles-de-base/le-controle-textblock-formatage-inline/
            var stackPanel = new StackPanel();
            stackPanel.Orientation = Orientation.Vertical;
            //stackPanel.Margin = new Thickness(10);
            stackPanel.Children.Add(new TextBlock() { Text = Label, FontWeight = FontWeights.Bold });
            stackPanel.Children.Add(new TextBlock() { Text = $"lat:{poiDef.pos.lat}, lon:{poiDef.pos.lon}" });
            if (poiDef.isCollectible)
            {
                stackPanel.Children.Add(new TextBlock() { Text = "clic droit pour récolte rapide", FontStyle = FontStyles.Italic });
            }
            return stackPanel;
        }

        virtual public FrameworkElement BuildPopup()
        {
            /*map.popup.Children.Clear();
            map.popup.Children.Add(new Border()
            {
                Background = Brushes.LightGray,
                Width = 290,
                Height = 70,
                CornerRadius = new CornerRadius(10),
                Effect = new DropShadowEffect() { ShadowDepth = 6 }
            });*/
            return null;
        }

        virtual public void ViewPopup(double x, double y, string label, string info)
        {
            map.ViewPopup(this, x, y, label, info);
        }
        virtual public void HidePopup()
        {
            map.HidePopup(this);
        }
        virtual public void RescalePopup(double x, double y)
        {
            map.RescalePopup(x, y);
        }

        abstract public FrameworkElement BuildForMap(double scale);
        abstract public FrameworkElement GetFrameworkElement();
        abstract public void Rescale(double scale);
        abstract public bool GetVisible();
        abstract public void SetVisible(bool value);
        virtual public bool GetCollected() { return false; }
        virtual public void SetCollected(bool value) { }
        virtual public bool GetEditable() { return false; }
        virtual public void SetEditable(bool value) { }
        virtual public void RescalePopup() { }
        virtual public void Ping() { }
        virtual public void RescalePing() { }
        virtual public FrameworkElement BuildForContents(int size) { return null; }
        virtual public void Update() { }
    }
}
