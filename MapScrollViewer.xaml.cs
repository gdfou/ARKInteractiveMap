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
using System.ComponentModel;
using System.Threading;
using System.Windows.Media.Effects;

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
        Dictionary<string, ResourceItem> contentDict_ = new Dictionary<string, ResourceItem>();
        Dictionary<string, bool> visibleDict_ = null;
        Rect viewArea = new Rect();
        double lastScale_;
        int mouseCaptureMove_;
        Point pingLastPoint;
        int mapBorderWidth_;
        Brush mapBorderColor_;
        MapPopup mapPopup_;
        Point contextMenuMousePos_;
        Timer timer_;

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
                    mapImage.Source = BitmapFrame.Create(new Uri(ResFiles.Get(value)));
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

        public int MapBorderWidth
        {
            get => mapBorderWidth_;
            set
            {
                if (value != mapBorderWidth_)
                {
                    mapBorderWidth_ = value;
                    mapSize_.pX = mapBorderWidth_;
                    mapSize_.pY = mapBorderWidth_;
                    NotifyPropertyChanged("MapBorderWidth");
                    //Console.WriteLine($"Map size = ({mapSize_.pX};{mapSize_.pY})-({mapSize_.pMaxWidth};{mapSize_.pMaxHeight})");
                }
            }
        }

        public Brush MapBorderColor
        {
            get => mapBorderColor_;
            set
            {
                if (value != mapBorderColor_)
                {
                    mapBorderColor_ = value;
                    NotifyPropertyChanged("MapBorderColor");
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

            menuItemSurface.IsChecked = true;
            menuItemCave.IsChecked = true;
            menuItemUser.IsChecked = true;
            menuItemFogOfWar.IsChecked = false;
            menuItemRessources.IsChecked = true;
            menuItemRessources.IsEnabled = false;
            menuItemExploration.IsChecked = true;
            menuItemExploration.IsEnabled = false;
        }

        private void MenuCommand_Click(object sender, RoutedEventArgs e)
        {
            // Récupérer la position courante sur la carte en [lat,lon] => MapPos
            // Attention prendre la position de la souris à l'ouverture du menu popup !
            //var mgc = Mouse.GetPosition(gridMap);
            var mpt = mapSize_.ConvertPixelPointToMap(contextMenuMousePos_.X, contextMenuMousePos_.Y);
            var menuItem = (sender as MenuItem);
            var cmd = menuItem.Tag as string;
            if (menuItem.IsCheckable)
            {
                cmd += $":{menuItem.IsChecked}";
            }
            Command(cmd, null, mpt);
        }

        public void ClearPoi()
        {
            canvasUser.Children.Clear();
            canvasPoi.Children.Clear();
            canvasPoiCave.Children.Clear();
            canvasCommon.Children.Clear();
            poiDict_.Clear();
        }

        public void LoadPoi(Dictionary<string, MapPoiDef> dict)
        {
            AddPoi(dict);
        }

        public void LoadContentList(List<string> list)
        {
            menuItemContents.Items.Clear();
            contentDict_.Clear();
            foreach (var group_name in list)
            {
                var poi_def = poiDict_.FirstOrDefault(x => x.Value.GroupName == group_name);
                if (poi_def.Key != null)
                {
                    var contentItem = new ResourceItem(group_name);
                    var poi = poiDict_[poi_def.Value.Id];
                    contentItem.UpdateIcons(this, poi, 20);
                    menuItemContents.Items.Add(contentItem);
                    contentDict_[group_name] = contentItem;
                }
            }
        }

        public void LoadLayersVisibility(Dictionary<string, bool> dict)
        {
            foreach (var item in dict)
            {
                UpdateVisible(item.Key, item.Value, false);
            }
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
                    //Console.WriteLine($"{poiMapItem.Id} => {poiDef.Layer}");
                    if (poiDef.userPoi)
                        canvasUser.Children.Add(poiMapItem.BuildForMap(Scale));
                    else if (poiDef.inCave)
                        canvasPoiCave.Children.Add(poiMapItem.BuildForMap(Scale));
                    else if (poiDef.Layer == null)
                        canvasCommon.Children.Add(poiMapItem.BuildForMap(Scale));
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

        class RectFog
        {
            public Rectangle shape;
            public Rect rect;
            public bool fog;
        }

        public void LoadFogOfWars(List<int> fow)
        {
            // Fow
            canvasFow.BeginInit();
            canvasFow.Children.Clear();
            var blurEffect = new BlurEffect
            {
                Radius = 10
            };
            var rlist = new List<RectFog>();
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    bool fog = (fow[y * 8 + x] == 0);
                    var frect = new Rectangle()
                    {
                        Width = 128,
                        Height = 128,
                        Fill = new SolidColorBrush(Colors.Gray),
                        Opacity = fog ? 0.6 : 0,
                        Effect = blurEffect
                    };
                    int px = x * 128;
                    int py = y * 128;
                    rlist.Add(new RectFog 
                    { 
                        fog = fog,
                        shape = frect,
                        rect = new Rect(px, py, frect.Width, frect.Height)
                    });
                    Canvas.SetLeft(frect, px);
                    Canvas.SetTop(frect, py);
                    canvasFow.Children.Add(frect);
                }
            }
            /*foreach (var src in rlist)
            {
                foreach (var dst in rlist)
                {
                    if (src != dst && src.rect.IntersectsWith(dst.rect) && src.fog && dst.fog)
                    {
                        var ir = Rect.Intersect(src.rect, dst.rect);
                        var line = new Line()
                        {
                            X1 = ir.Left,
                            Y1 = ir.Top,
                            X2 = ir.Right,
                            Y2 = ir.Bottom,
                            Stroke = new SolidColorBrush(Colors.Red)
                        };
                        canvasFow.Children.Add(line);
                    }
                }
            }*/
            canvasFow.EndInit();
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
                    (var pos, var size) = (pingEllipse.Tag as MapPoi).CurrentPosAndSize;
                    RescalePing(size, pos);
                }
            }
        }

        public void UpdateVisible(string groupName, bool isVisible, bool save = true)
        {
            if (groupName.Contains("layers-"))
            {
                switch (groupName)
                {
                    case "layers-surface":
                        canvasPoi.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                        menuItemSurface.IsChecked = isVisible;
                        break;
                    case "layers-cave":
                        canvasPoiCave.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                        menuItemCave.IsChecked = isVisible;
                        break;
                    case "layers-user":
                        canvasUser.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                        menuItemUser.IsChecked = isVisible;
                        break;
                    case "layers-fow":
                        canvasFow.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                        menuItemFogOfWar.IsChecked = isVisible;
                        break;
                    case "layers-resources":
                    case "layers-exploration":
                        if (groupName == "layers-resources")
                            menuItemRessources.IsChecked = isVisible;
                        else
                            menuItemExploration.IsChecked = isVisible;
                        foreach (var item in poiDict_.Values)
                        {
                            if (item.poiDef.Layer == groupName)
                            {
                                item.LayerVisible = isVisible;
                            }
                        }
                        break;
                }
            }
            else
            {
                // Check if there is a menu associated with the group
                if (contentDict_.ContainsKey(groupName))
                {
                    contentDict_[groupName].IsVisible = isVisible;
                }
                foreach (var item in poiDict_.Values)
                {
                    if (item.GroupName == groupName)
                    {
                        item.ContentVisible = isVisible;
                    }
                }
            }
            if (save)
                Save();
        }

        public void EnabledLayer(string layer, bool enabled)
        {
            if (layer.Contains("layers-"))
            {
                switch (layer)
                {
                    case "layers-surface":
                        menuItemSurface.IsEnabled = enabled;
                        break;
                    case "layers-cave":
                        menuItemCave.IsEnabled = enabled;
                        break;
                    case "layers-user":
                        menuItemUser.IsEnabled = enabled;
                        break;
                    case "layers-fow":
                        menuItemFogOfWar.IsEnabled = enabled;
                        break;
                    case "layers-resources":
                        menuItemRessources.IsEnabled = enabled;
                        break;
                    case "layers-exploration":
                        menuItemExploration.IsEnabled = enabled;
                        break;
                }
            }
        }

        public Dictionary<string, bool> GetLayersVisibility()
        {
            Dictionary<string, bool> dict = new Dictionary<string, bool>();
            dict["layers-surface"] = menuItemSurface.IsChecked;
            dict["layers-cave"] = menuItemCave.IsChecked;
            dict["layers-user"] = menuItemUser.IsChecked;
            dict["layers-fow"] = menuItemFogOfWar.IsChecked;
            if (menuItemRessources.IsEnabled)
            {
                dict["layers-resources"] = menuItemRessources.IsChecked;
            }
            if (menuItemExploration.IsEnabled)
            {
                dict["layers-exploration"] = menuItemExploration.IsChecked;
            }
            return dict;
        }

        public Dictionary<string, bool> GetContentDict()
        {
            Dictionary<string, bool> dict = new Dictionary<string, bool>();
            foreach (var item in contentDict_)
            {
                dict[item.Key] = item.Value.IsVisible;
            }
            return dict;
        }

        public void UpdateVisible(ResourceItem visibleItem, bool save=true)
        {
            UpdateVisible(visibleItem.GroupName, visibleItem.IsVisible, save);
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
        } = 10;

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

        // Fit => use a timer and a dispatcher because gridMap is not initialised when the this fonction is called
        void TimerCallback(Object state)
        {
            timer_ = null;
            gridMap.Dispatcher.BeginInvoke(new Action(() => { ViewArea = new Rect(0, 0, gridMap.ActualWidth, gridMap.ActualHeight); }));
        }
        public void ZoomInFull(bool timer=false)
        {
            if (timer)
            {
                timer_ = new Timer(TimerCallback, null, 50, 0);
            }
            else
            {
                ViewArea = new Rect(0, 0, gridMap.ActualWidth, gridMap.ActualHeight);
            }
        }

        public void ZoomTo(double left, double top)
        {
            if (left != double.NaN && top != double.NaN)
            {
                scrollViewer.ScrollToHorizontalOffset(left - (scrollViewer.ActualWidth / 2));
                scrollViewer.ScrollToVerticalOffset(top - (scrollViewer.ActualHeight / 2));
            }
        }

        public void ZoomTo(string id)
        {
            pingStoryboard.Storyboard.Stop();
            var poi = poiDict_[id];
            if (poi != null)
            {
                (var pos, var size) = poi.CurrentPosAndSize;
                if (pos.X != double.NaN)
                {
                    ZoomTo(pos.X, pos.Y);
                    Ping(size, pos, poi);
                }
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
            // Command interne ou commande externe ?
            // <cmd>:<params>
            var split = cmd.Split(':');
            switch (split[0])
            {
                case "UpdateVisible":
                    {
                        // Cas particulier : UpdateVisible:all, none et restore
                        if (split[1] == "all")
                        {
                            if (visibleDict_ == null)
                            {
                                visibleDict_ = new Dictionary<string, bool>();
                            }
                            menuItemRestoreVisibility.IsEnabled = true;
                            foreach (var item in contentDict_)
                            {
                                if (visibleDict_ != null)
                                {
                                    visibleDict_[item.Key] = item.Value.IsVisible;
                                }
                                item.Value.IsVisible = true;
                            }
                        }
                        else if (split[1] == "none")
                        {
                            if (visibleDict_ == null)
                            {
                                visibleDict_ = new Dictionary<string, bool>();
                            }
                            menuItemRestoreVisibility.IsEnabled = true;
                            foreach (var item in contentDict_)
                            {
                                if (visibleDict_ != null)
                                {
                                    visibleDict_[item.Key] = item.Value.IsVisible;
                                }
                                item.Value.IsVisible = false;
                            }
                        }
                        else if (split[1] == "restore")
                        {
                            menuItemRestoreVisibility.IsEnabled = false;
                            if (visibleDict_ != null)
                            {
                                foreach (var item in visibleDict_)
                                {
                                    contentDict_[item.Key].IsVisible = item.Value;
                                }
                                visibleDict_ = null;
                            }
                        }
                        else
                        {
                            // UpdateVisible:layers-xxx
                            UpdateVisible(split[1], (split.Length >= 2) ? (split[2] == "True") : false);
                        }
                        break;
                    }

                default:
                    CommandEvent?.Invoke(this, new CommandEventArgs(cmd, id, param));
                    break;
            }
        }

        public int CaveLayerCount 
        { 
            get { return canvasPoiCave.Children.Count; }
        }

        private void gridMap_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            contextMenuMousePos_ = lastMousePosition_;
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

        public new bool Equals(object obj)
        {
            if (obj == null || !(obj is MapSize))
                return false;

            var objet2 = (MapSize)obj;
            return this.cLon == objet2.cLon && this.cLat == objet2.cLat && this.cWidth == objet2.cWidth && this.cHeight == objet2.cHeight;
        }

        public override string ToString()
        {
            return $"({cLon:F2};{cLat:F2};{cWidth:F2};{cHeight:F2})";
        }
    }
}