using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Controls;
using static System.Net.Mime.MediaTypeNames;

namespace ARKInteractiveMap
{
    public class ResourceItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected MapScrollViewer map_;
        public BitmapImage IconRes { get; set; }
        public FrameworkElement IconMap { get; set; }
        public string GroupName { get; set; }
        public string Label { get; set; }
        public string Shape { get; set; }

        private bool isVisible_;
        public bool IsVisible // Binding
        {
            get => isVisible_;
            set
            {
                if (isVisible_ != value)
                {
                    isVisible_ = value;
                    map_?.UpdateVisible(this);
                    OnPropertyChanged("IsVisible");
                }
            }
        }

        // For content list
        public ResourceItem(string groupName)
        {
            GroupName = groupName;
            isVisible_ = true;
        }

        public void UpdateIcons(MapScrollViewer map, MapPoi poi, int size)
        {
            map_ = map;
            Label = poi.poiDef.group.name;
            // icon de ressource
            var assembly = Assembly.GetExecutingAssembly();
            var app_res_list = assembly.GetManifestResourceNames();
            var iconRes = MapPoiDef.getIconResname(poi.poiDef.group?.icon);
            if (iconRes != null && app_res_list.Contains(iconRes))
            {
                BitmapImage src = new BitmapImage();
                src.BeginInit();
                src.StreamSource = assembly.GetManifestResourceStream(iconRes);
                src.EndInit();
                IconRes = src;
            }
            else if (poi.poiDef.group != null)
            {
                Console.WriteLine($"Ne trouve pas l'icon '{poi.poiDef.group.icon}' pour le groupe '{poi.poiDef.group.groupName}'");
            }
            // Icon sur la map
            IconMap = MapPoiDef.BuildForContents(poi, size);
        }

        // for layer list
        public ResourceItem(MapScrollViewer map, string groupName, string label)
        {
            map_ = map;
            GroupName = groupName;
            Label = label;
            isVisible_ = true;
        }

        // Méthode pour dessiner un FrameworkElement sur un BitmapImage
        public BitmapImage DrawFrameworkElementOnBitmap(FrameworkElement element, int width, int height)
        {
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double ox = (width - element.DesiredSize.Width) / 2;
            double oy = (height - element.DesiredSize.Height) / 2;
            element.Arrange(new Rect(ox,oy,width,height));
            element.UpdateLayout();

            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);

            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            /*using (var stream = File.Create(@"D:\Temp\empty.png"))
            {
                encoder.Save(stream);
            }*/
            BitmapImage bitmapImage = new BitmapImage();
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                stream.Seek(0, SeekOrigin.Begin);

                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Gèle l'image pour améliorer les performances (nécessaire si vous utilisez cette image dans un thread différent de celui de l'interface utilisateur)
            }
            return bitmapImage;
        }

        // for ingame or user poi edit
        public ResourceItem(MapPoiShape shape, int size, string param=null)
        {
            Shape = shape.ToString().ToLower() + (param != null ? $"#{param}" : "");
            switch (shape)
            {
                //case MapPoiShape.Ellipse: return new MapPoiEllipse(poi.poiDef, null).BuildForContents(size);
                //case MapPoiShape.Icon: return new MapPoiIcon(poi.poiDef, null).BuildForContents(size);
                case MapPoiShape.Triangle:
                    IconMap = new MapPoiTriangle(new MapPoiDef() { groupName = "user-map-poi", fillColor = "#ff0000" }, null, param).BuildForContents(size);
                    break;
                case MapPoiShape.Letter:
                    IconMap = new MapPoiLetter(new MapPoiDef() { groupName = "user-map-poi", fillColor = "#ff0000" }, null, param).BuildForContents(size);
                    break;
            }
            IconRes = DrawFrameworkElementOnBitmap(IconMap, size, size);
        }

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
