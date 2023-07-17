using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Collections.Generic;

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
                Console.WriteLine($"Ne trouve pas l'icon '{poi.poiDef.group.icon}'");
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

        // for ingame or user poi edit
        public ResourceItem(MapPoiShape shape, int size)
        {
            switch (shape)
            {
                //case MapPoiShape.Ellipse: return new MapPoiEllipse(poi.poiDef, null).BuildForContents(size);
                //case MapPoiShape.Icon: return new MapPoiIcon(poi.poiDef, null).BuildForContents(size);
                case MapPoiShape.Triangle: 
                    IconMap = new MapPoiTriangle(new MapPoiDef() { groupName="user-map-poi", fillColor="#ff0000"}, null).BuildForContents(size); 
                    break;
                case MapPoiShape.Pie: 
                    IconMap = new MapPoiPie(null, null).BuildForContents(size);
                    break;
                case MapPoiShape.Letter: 
                    IconMap = new MapPoiLetter(null, null).BuildForContents(size);
                    break;
            }
        }

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
