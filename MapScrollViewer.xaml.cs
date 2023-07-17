using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Reflection;
using System.Linq;
using System.Windows.Shapes;
using System.Windows.Media;
using System.IO;
using System.ComponentModel;
using System.Windows.Media.Effects;
using System.Windows.Controls.Primitives;

namespace ARKInteractiveMap
{
    /// <summary>
    /// Interaction logic for MapScrollViewer.xaml
    /// </summary>
    public partial class MapScrollViewer : UserControl, INotifyPropertyChanged
    {
        MapSize mapSize_;
        Point? lastCenterPositionOnTarget_;
        Point lastMousePosition_;
        Point? lastDragPoint_;
        Dictionary<string, MapPoi> poiDict_ = new Dictionary<string, MapPoi>();
        Rect viewArea = new Rect();
        double lastScale_;
        int mouseCaptureMove_;
        Point pingLastPoint;
        ContextMenu menu_;
        int mapBorderWidth_;
        Brush mapBorderColor_;
        MapPopup mapPopup_;

        public MapPopup mapPopup => mapPopup_;

        public new FrameworkElement Content
        {
            get => (FrameworkElement)gridMap.Children[0];
            set
            {
                gridMap.Children.Clear();
                gridMap.Children.Add(value);
                Rect view = new Rect(0, 0, value.ActualWidth, value.ActualHeight);
                ViewArea = view;
            }
        }

        public MapSize MapSize
        {
            get { return mapSize_; } 
            set { mapSize_ = value; }
        }

        public string MapImage
        {
            set 
            {
                try
                {
                    mapImage.Source = BitmapFrame.Create(Assembly.GetExecutingAssembly().GetManifestResourceStream(value));
                }
                catch
                {
                    MessageBox.Show($"la carte {value} n'a pas été trouvé dans les ressources !");
                }
                if (mapSize_ == null)
                {
                    mapSize_ = new MapSize();
                }
                mapSize_.pX = mapBorderWidth_;
                mapSize_.pY = mapBorderWidth_;
                mapSize_.pWidth = (int)mapImage.Source.Width;
                mapSize_.pHeight = (int)mapImage.Source.Height;
                //Console.WriteLine($"Map size = ({mapSize_.pX};{mapSize_.pY})-({mapSize_.pMaxWidth};{mapSize_.pMaxHeight})");
            }
        }

        public int mapBorderWidth
        {
            get => mapBorderWidth_;
            set
            {
                if (value != mapBorderWidth_)
                {
                    mapBorderWidth_ = value;
                    mapSize_.pX = mapBorderWidth_;
                    mapSize_.pY = mapBorderWidth_;
                    NotifyPropertyChanged("mapBorderWidth");
                    //Console.WriteLine($"Map size = ({mapSize_.pX};{mapSize_.pY})-({mapSize_.pMaxWidth};{mapSize_.pMaxHeight})");
                }
            }
        }

        public Brush mapBorderColor
        {
            get => mapBorderColor_;
            set
            {
                if (value != mapBorderColor_)
                {
                    mapBorderColor_ = value;
                    NotifyPropertyChanged("mapBorderColor");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public MapScrollViewer()
        {
            InitializeComponent();
            DataContext = this;

            mapPopup_ = new MapPopup(popup);
            mapSize_ = new MapSize(0, 0, 100, 100);

            scrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
            scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
            // In the button-down state, the move.. is processed by capturing the event only in the scroll viewer.
            scrollViewer.MouseMove += ScrollViewer_MouseMove;
            // Button up for capture mode is only handled by the scroll view.
            scrollViewer.PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;

            // Button down from any object : Needed to drag map with mouse button
            gridMap.MouseLeftButtonDown += OnMouseLeftButtonDown;
            // just a move that decides the mode
            gridMap.MouseMove += OnMouseMove;

            // Poi grid
            canvasPoi.MouseLeftButtonDown += OnMouseLeftButtonDown;
            canvasPoi.MouseMove += OnMouseMoveCanvas;
            canvasUser.MouseLeftButtonDown += OnMouseLeftButtonDown;
            canvasUser.MouseMove += OnMouseMoveCanvas;
            ZoomInFull();

            // Context menu
            menu_ = new ContextMenu();
            menu_.Items.Add(new MenuItem()
            {
                Header = "Ajouter un repère ici",
                Tag = "IngameMarkerAdd",
            });
            foreach (MenuItem item in menu_.Items)
            {
                item.Click += Menu_Click;
            }
            gridMap.ContextMenu = menu_;
        }

        private void Menu_Click(object sender, RoutedEventArgs e)
        {
            // Récupérer la position courante sur la carte en [lat,lon] => MapPos
            var mgc = Mouse.GetPosition(gridMap);
            var mpt = mapSize_.ConvertPixelPointToMap(mgc.X, mgc.Y);
            Command((sender as MenuItem).Tag as string, null, mpt);
        }

        public void ClearPoi()
        {
            canvasUser.Children.Clear();
            canvasPoi.Children.Clear();
            canvasPoiCave.Children.Clear();
            poiDict_.Clear();
        }

        public void LoadPoi(Dictionary<string, MapPoiDef> dict)
        {
            AddPoi(dict);
        }

        public void AddPoi(string id, MapPoiDef poiDef)
        {
            try
            {
                MapPoi poiMapItem = poiDef.BuildMapPoi(this);
                if (poiMapItem != null)
                {
                    if (poiMapItem.pos.X < 0 || poiMapItem.pos.Y < 0 || poiMapItem.pos.X > mapSize_.pMaxWidth || poiMapItem.pos.Y > mapSize_.pMaxHeight)
                    {
                        Console.WriteLine($"Poi({poiDef.pos.lat:F5};{poiDef.pos.lon:F5}) hors map ({poiMapItem.pos.X:F5};{poiMapItem.pos.Y:F5}) !");
                    }

                    poiDict_[poiMapItem.Id] = poiMapItem;
                    // Selection de la layer
                    if (poiDef.userPoi)
                        canvasUser.Children.Add(poiMapItem.BuildForMap(Scale));
                    else if (poiDef.inCave)
                        canvasPoiCave.Children.Add(poiMapItem.BuildForMap(Scale));
                    else
                        canvasPoi.Children.Add(poiMapItem.BuildForMap(Scale));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error accessing resources ! ({ex.Message})");
            }
        }

        public void AddPoi(Dictionary<string, MapPoiDef> dict)
        {
            foreach (var item in dict)
            {
                AddPoi(item.Key, item.Value);
            }
        }

        public void RemovePoi(string poiId)
        {
            var item = poiDict_.FirstOrDefault(p => p.Key == poiId);
            if (item.Key != null)
            {
                // On ne peux supprimer qu'un poi user
                if (item.Value.poiDef.userPoi)
                {
                    poiDict_.Remove(item.Key);
                    canvasUser.BeginInit();
                    // Recherche l'élément dans le canvas
                    foreach (FrameworkElement itemElt in canvasUser.Children)
                    {
                        var mapPoi = itemElt.Tag as MapPoi;
                        if (mapPoi != null)
                        {
                            if (mapPoi.Id == poiId)
                            {
                                //itemElt.Clear();
                                canvasUser.Children.Remove(itemElt);
                                break;
                            }
                        }
                    }
                    canvasUser.EndInit();
                }
            }
        }

        public void ClearFogOfWars()
        {
            canvasFow.BeginInit();
            canvasFow.Children.Clear();
            canvasFow.EndInit();
        }

        public void LoadFogOfWars(List<int> fow, bool visible)
        {
            // Fow
            canvasFow.BeginInit();
            canvasFow.Children.Clear();
            for (int y=0; y<8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    var rect = new Rectangle()
                    {
                        Width = 128,
                        Height = 128,
                        Fill = new SolidColorBrush(Colors.Gray),
                        Opacity = (fow[x*8+y] == 1) ? 0 : 0.5,
                    };
                    Canvas.SetLeft(rect, x * 128);
                    Canvas.SetTop(rect, y * 128);
                    canvasFow.Children.Add(rect);
                }
            }
            canvasFow.EndInit();
            FogOfWarsVisible(visible);
        }

        public void FogOfWarsVisible(bool visible)
        {
            canvasFow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        protected void RescaleCanvas(double scale)
        {
            foreach (var item in poiDict_.Values) { item.Rescale(scale); }
            mapPopup_.RescalePopup();
            if (pingEllipse.Tag != null)
            {
                if (pingEllipse.Tag == this && pingLastPoint != null)
                {
                    RescalePing(20, pingLastPoint);
                }
                else
                {
                    (pingEllipse.Tag as MapPoi).RescalePing();
                }
            }
        }

        public void UpdateVisible(ResourceItem visibleItem)
        {
            if (visibleItem.GroupName.Contains("layers-"))
            {
                switch (visibleItem.GroupName)
                {
                    case "layers-surface": canvasPoi.Visibility = visibleItem.IsVisible ? Visibility.Visible : Visibility.Collapsed; break;
                    case "layers-cave": canvasPoiCave.Visibility = visibleItem.IsVisible ? Visibility.Visible : Visibility.Collapsed; break;
                    case "layers-user": canvasUser.Visibility = visibleItem.IsVisible ? Visibility.Visible : Visibility.Collapsed; break;
                }
            }
            else
            {
                foreach (var item in poiDict_.Values)
                {
                    if (item.GroupName == visibleItem.GroupName)
                    {
                        item.Visible = visibleItem.IsVisible;
                    }
                }
            }
            Save();
        }

        public void UpdateCollected(string id, bool collected)
        {
            poiDict_[id].Collected = collected;
            Save();
        }

        public void UpdateCollected(Dictionary<string, List<string>> dict)
        {
            if (dict != null)
            {
                foreach (var group in dict)
                {
                    foreach (var id in group.Value)
                    {
                        if (poiDict_.TryGetValue(id, out var poi))
                        {
                            poi.Collected = true;
                        }
                    }
                }
            }
        }

        protected void PrintInfo(MouseEventArgs e)
        {
            var va = new Point(ViewArea.X, ViewArea.Y);
            var p0 = new Point(scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset);
            var mgc = e?.GetPosition(gridMap) ?? new Point(0, 0);
            var mpt = mapSize_.ConvertPixelPointToMap(mgc.X, mgc.Y);
#if DEBUG
            labelInfo.Text = $"P=({(int)mgc.X};{(int)mgc.Y}) M=({mpt.lat:0.00};{mpt.lon:0.00}) VA=({(int)va.X};{(int)va.Y}) ({(int)p0.X};{(int)p0.Y}) S={Scale:0.0000}";
#else
            labelInfo.Text = $"lat {mpt.lat:00.00}, lon {mpt.lon:00.00}";
#endif
        }

        public double MaxScale
        {
            get; set;
        }

        public double Scale
        {
            get => scaleTransform.ScaleX;
            set
            {
                if (value == double.NaN || value < 0)
                {
                    value = 1;
                }
                scaleTransform.ScaleX = value;
                scaleTransform.ScaleY = value;
                if (lastScale_ != value)
                {
                    lastScale_ = value;
                    RescaleCanvas(value);
                }
            }
        }

        public Point Origin
        {
            set
            {
                scrollViewer.ScrollToHorizontalOffset(value.X * Scale);
                scrollViewer.ScrollToVerticalOffset(value.Y * Scale);
            }
            get
            {
                return new Point(ViewArea.X, ViewArea.Y);
            }
        }

        public Rect ViewArea
        {
            set
            {
                double windowWidth = scrollViewer.ViewportWidth;
                double windowHeight = scrollViewer.ViewportHeight;
                double windowRate = windowWidth / windowHeight;

                if (windowWidth == 0)
                {
                    windowWidth = scrollViewer.ActualWidth;
                    windowHeight = scrollViewer.ActualHeight;
                }

                double a = gridMap.Width;

                double contentWidth = gridMap.ActualWidth; // grid
                double contentHeight = gridMap.ActualHeight; // grid
                double contentRate = contentWidth / contentHeight;

                //oriented in content.
                Rect rect = value;

                if (rect.Width == 0 || contentWidth == 0 || windowWidth == 0)
                {
                    viewArea = rect;
                    return;
                }

                //--decide scale
                //allowed by scrollViewer
                double minScale = Math.Min(windowWidth / contentWidth, windowHeight / contentHeight);

                double scaleX = Math.Max(windowWidth / rect.Width, minScale);
                double scaleY = Math.Max(windowHeight / rect.Height, minScale);

                double scale;
                //(x or y) axis should be extended.
                if (scaleX > scaleY)
                {
                    scale = scaleY;
                    double oldWidth = rect.Width;
                    rect.Width = windowWidth / scale;
                    rect.X -= (rect.Width - oldWidth) / 2;//extend from center
                }
                else
                {
                    scale = scaleX;
                    double oldHeight = rect.Height;
                    rect.Height = windowHeight / scale;
                    rect.Y -= (rect.Height - oldHeight) / 2;
                }
                Scale = scale;

                scrollViewer.ScrollToHorizontalOffset(rect.X * scale);
                scrollViewer.ScrollToVerticalOffset(rect.Y * scale);

                PrintInfo(null);
            }

            get
            {
                return viewArea;
            }
        }

        public MapPoi this[string id] => poiDict_[id];

        // Fit
        public void ZoomInFull()
        {
            ViewArea = new Rect(0, 0, gridMap.ActualWidth, gridMap.ActualHeight);
        }

        public void ZoomTo(double left, double top)
        {
            scrollViewer.ScrollToHorizontalOffset(left - (scrollViewer.ActualWidth / 2));
            scrollViewer.ScrollToVerticalOffset(top - (scrollViewer.ActualHeight / 2));
        }

        public void ZoomTo(string id)
        {
            var poi = poiDict_[id];
            var poi_framework_item = poi?.GetFrameworkElement();
            if (poi_framework_item != null)
            {
                ZoomTo(Canvas.GetLeft(poi_framework_item), Canvas.GetTop(poi_framework_item));
                poi.Ping();
            }
        }

        public void Ping(double width, Point pos, object tag)
        {
            // Scale 1 => 50
            // Scale 8 => 100
            pingEllipse.Width = 42.86 + 7.14 * Scale;
            pingEllipse.Height = pingEllipse.Width;
            Canvas.SetLeft(pingEllipse, pos.X + width / 2 - pingEllipse.Width / 2);
            Canvas.SetTop(pingEllipse, pos.Y + width / 2 - pingEllipse.Height / 2);
            pingEllipse.Tag = tag;
            pingEllipse.Visibility = Visibility.Visible;
            pingStoryboard.Storyboard.Stop();
            pingStoryboard.Storyboard.Begin();
        }

        public void RescalePing(double width, Point pos)
        {
            pingEllipse.Width = 42.86 + 7.14 * Scale;
            pingEllipse.Height = pingEllipse.Width;
            Canvas.SetLeft(pingEllipse, pos.X + width / 2 - pingEllipse.Width / 2);
            Canvas.SetTop(pingEllipse, pos.Y + width / 2 - pingEllipse.Height / 2);
        }

        public void ZoomToMapPos(float lat, float lon)
        {
            var pos = MapSize.ConvertMapPointToPixel(new MapPos(lat, lon));
            pingLastPoint.X = pos.X * Scale - 10;
            pingLastPoint.Y = pos.Y * Scale - 10;
            ZoomTo(pingLastPoint.X, pingLastPoint.Y);
            Ping(20, pingLastPoint, this);
        }

        private void ComputeMouseMove(object sender, MouseEventArgs e)
        {
            if (lastDragPoint_.HasValue)
            {
                Point posNow = e.GetPosition(scrollViewer);

                double dX = posNow.X - lastDragPoint_.Value.X;
                double dY = posNow.Y - lastDragPoint_.Value.Y;

                lastDragPoint_ = posNow;

                Rect rect = ViewArea;
                rect.X -= dX / Scale;
                rect.Y -= dY / Scale;
                ViewArea = rect;
                mouseCaptureMove_++;
            }
        }

        private void ScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            ComputeMouseMove(sender, e);
            PrintInfo(e);
        }

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            ComputeMouseMove(sender, e);
            lastMousePosition_ = e.GetPosition(gridMap);
        }
        void OnMouseMoveCanvas(object sender, MouseEventArgs e)
        {
            ComputeMouseMove(sender, e);
            lastMousePosition_ = e.GetPosition(gridMap);
        }
        void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var mousePos = e.GetPosition(scrollViewer);
            if (mousePos.X <= scrollViewer.ViewportWidth && mousePos.Y <
                scrollViewer.ViewportHeight) //make sure we still can use the scrollbars
            {
                lastDragPoint_ = mousePos;
                Mouse.Capture(scrollViewer);
            }
            mouseCaptureMove_ = 0;
#if false
            // Process clic
            var src = e.Source as FrameworkElement;
            if (src?.Tag == null) // Si pas de Tag alors clic dans une zone vide
            {
            }
#endif
        }
        void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            scrollViewer.ReleaseMouseCapture();
            lastDragPoint_ = null;
            if (mouseCaptureMove_ < 10)
            {
                mapPopup_.ControlPopupVisiblity();
            }
            mouseCaptureMove_ = 0;
        }

        public void ViewPopup(MapPoi poi, double x, double y, string label, string info)
        {
            mapPopup_.ViewPopup(poi, x, y, label, info);
        }

        public void HidePopup(MapPoi poi)
        {
            mapPopup_.HidePopup(poi);
        }

        public void RescalePopup(double x, double y)
        {
            mapPopup_.RescalePopup(x, y);
        }

        void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double delta_scale = 1;
            if (e.Delta > 0)
            {
                delta_scale /= 2;
            }
            if (e.Delta < 0)
            {
                delta_scale *= 2;
            }
            if (delta_scale > MaxScale)
            {
                delta_scale = MaxScale;
            }

            Point pos = e.GetPosition(gridMap);
            Rect view = ViewArea;

            double nuWidth = view.Width * delta_scale;
            double nuHeight = view.Height * delta_scale;

            // check scaling max
            double scale = scrollViewer.ViewportWidth / nuWidth;
            if (scale > MaxScale)
            {
                nuWidth = scrollViewer.ViewportWidth / MaxScale;
                nuHeight = scrollViewer.ViewportHeight / MaxScale;
            }

            // leftSide / total width
            double rateX = (pos.X - view.X) / view.Width;
            view.X -= (nuWidth - view.Width) * rateX;

            //topSide / total height
            double rateY = (pos.Y - view.Y) / view.Height;
            view.Y -= (nuHeight - view.Height) * rateY;

            view.Width = nuWidth;
            view.Height = nuHeight;

            ViewArea = view;
        }

        void OnSliderValueChanged(object sender,
             RoutedPropertyChangedEventArgs<double> e)
        {
            Scale = e.NewValue;

            var centerOfViewport = new Point(scrollViewer.ViewportWidth / 2,
                                             scrollViewer.ViewportHeight / 2);
            lastCenterPositionOnTarget_ = scrollViewer.TranslatePoint(centerOfViewport, gridMap); // grid
        }

        void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            double scale = Scale;
            if (scale != 0)
            {
                viewArea.X = scrollViewer.HorizontalOffset / scale;
                viewArea.Y = scrollViewer.VerticalOffset / scale;
                viewArea.Width = scrollViewer.ViewportWidth / scale;
                viewArea.Height = scrollViewer.ViewportHeight / scale;

                double contentWidth = gridMap.ActualWidth;
                double contentHeight = gridMap.ActualHeight;

                if (viewArea.Width > contentWidth)
                {
                    viewArea.X -= (viewArea.Width - contentWidth) / 2;
                }

                if (viewArea.Height > contentHeight)
                {
                    viewArea.Y -= (viewArea.Height - contentHeight) / 2;
                }
            }
        }

        private void Storyboard_Completed(object sender, EventArgs e)
        {
            pingEllipse.Visibility = Visibility.Collapsed;
            pingEllipse.Tag = null;
        }

        // Update collected event
        public delegate void UpdateCollectedEventHandler(object sender, UpdateCollectedEventArgs e);
        public event UpdateCollectedEventHandler UpdateCollectedEvent;
        public void CallUpdateCollectedEvent(object sender, string id, bool collected)
        {
            UpdateCollectedEvent?.Invoke(sender, new UpdateCollectedEventArgs(id, collected));
        }

        // Save event handler
        public int LockSave { get; set; }
        public delegate void SaveEventHandler(object sender, EventArgs e);
        public event SaveEventHandler SaveEvent;
        public void Save()
        {
            if (LockSave == 0)
            {
                SaveEvent?.Invoke(this, null);
            }
        }

        // Command event handler
        public delegate void CommandEventHandler(object sender, CommandEventArgs e);
        public event CommandEventHandler CommandEvent;
        public void Command(string cmd, string id, object param = null)
        {
            CommandEvent?.Invoke(this, new CommandEventArgs(cmd, id, param));
        }

        public int CaveLayerCount 
        { 
            get { return canvasPoiCave.Children.Count; }
        }
    }

    public class UpdateCollectedEventArgs : EventArgs
    {
        private string id_;
        private bool collected_;

        public string Id
        {
            get { return id_; }
        }

        public bool Collected
        {
            get { return collected_; }
        }

        public UpdateCollectedEventArgs(string id, bool collected)
        {
            this.id_ = id;
            this.collected_ = collected;
        }
    }

    public class CommandEventArgs : EventArgs
    {
        private string cmd_;
        private string id_;
        private object param_;

        public string Cmd
        {
            get { return cmd_; }
        }

        public string Id
        {
            get { return id_; }
        }

        public object Param
        {
            get { return param_; }
        }

        public CommandEventArgs(string cmd, string id, object param)
        {
            this.cmd_ = cmd;
            this.id_ = id;
            this.param_ = param;
        }
    }

    public class MapBorder
    {
        public double Lon;
        public double Lat;
    }

    public class MapSize
    {
        public double cLon; // c => coordonnées
        public double cLat;
        public double cWidth;
        public double cHeight;
        public int pX;
        public int pY;
        public int pWidth; // pixel width
        public int pHeight; // pixel width

        public double cMaxLat => cHeight + cLat;
        public double cMaxLon => cWidth + cLon;
        public int pMaxWidth => pWidth + (2*pX);
        public int pMaxHeight => pHeight + (2*pY);

        public MapSize()
        {
            pX = 0;
            pY = 0;
            cLon = 0;
            cLat = 0;
            cWidth = 100 - cLon;
            cHeight = 100 - cLat;
        }

        public MapSize(double x1, double y1, double x2, double y2)
        {
            pX = 0;
            pY = 0;
            cLon = x1;
            cLat = y1;
            cWidth = x2 - cLon;
            cHeight = y2 - cLat;
        }

        public Point ConvertMapPointToPixel(MapPos pos)
        {
            if (pos != null && pWidth != 0 && pHeight != 0 && cWidth != 0 && cHeight != 0)
            {
                double o_x = pX + ((pos.lon - cLon) * pWidth) / cWidth;
                double o_y = pY + ((pos.lat - cLat) * pHeight) / cHeight;
                return new Point(o_x, o_y);
            }
            return new Point(0, 0);
        }

        public MapPos ConvertPixelPointToMap(double x, double y)
        {
            if (pWidth != 0 && pHeight != 0)
            {
                double o_lon = ((cWidth * (x - pX)) / pWidth) + cLon;
                double o_lat = ((cHeight * (y - pY)) / pHeight) + cLat;
                return new MapPos(o_lat, o_lon);
            }
            return new MapPos(0, 0);
        }
    }
}