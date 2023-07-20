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
        protected MapPoiCategory category_;
        protected ArkWikiJsonGroup group_;
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
                    map_.UpdateVisible(this);
                }
            }
        }

        public ResourceItem(MapScrollViewer map, string groupName, ArkWikiJsonGroup group, MapPoiCategory category)
        {
            category_ = category;
            group_ = group;
            GroupName = groupName;
            Label = group?.name;
            isVisible_ = true;
            // icon de ressource
            var assembly = Assembly.GetExecutingAssembly();
            var app_res_list = assembly.GetManifestResourceNames();
            var iconRes = MapPoiDef.getIconResname(group?.icon);
            if (iconRes != null && app_res_list.Contains(iconRes))
            {
                BitmapImage src = new BitmapImage();
                src.BeginInit();
                src.StreamSource = assembly.GetManifestResourceStream(iconRes);
                src.EndInit();
                IconRes = src;
            }
            else if (group != null)
            {
                Console.WriteLine($"Ne trouve pas l'icon '{group.icon}'");
            }
            if (map != null)
            {
                FinalizeInit(map);
            }
        }

        public ResourceItem(MapScrollViewer map, string groupName, string label)
        {
            map_ = map;
            GroupName = groupName;
            Label = label;
            isVisible_ = true;
        }

        public void FinalizeInit(MapScrollViewer map)
        {
            map_ = map;
            // icon de la carte
            IconMap = map?.GetMapIcon(GroupName, group_, category_, 20);
        }

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
