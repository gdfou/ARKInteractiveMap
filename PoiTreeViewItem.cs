﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace ARKInteractiveMap
{
    public class PoiTreeViewItem : INotifyPropertyChanged
    {
        protected bool isExpanded_;
        protected MapScrollViewer map_;
        protected PoiTreeViewItem parent_;
        protected MapPoiDef poiDef_;

        public string Label { get; set; }
        public BitmapImage IconRes { get; set; }

        // Interface TreeView
        public ObservableCollection<PoiTreeViewItem> Childrens { get; set; }

        protected void SetExpanded(bool value)
        {
            if (value != isExpanded_ && Childrens != null)
            {
                isExpanded_ = value;
                NotifyPropertyChanged("IsExpanded");
            }
        }

        public bool IsExpanded
        {
            get => isExpanded_;
            set
            {
                if (value != isExpanded_ && Childrens != null)
                {
                    isExpanded_ = value;
                    NotifyPropertyChanged("IsExpanded");
                    //map_.Save();
                }
            }
        }

        public string Id
        {
            get
            {
                if (poiDef_ != null)
                {
                    return poiDef_.Id;
                }
                else
                {
                    return null;
                }
            }
        }

        public PoiTreeViewItem(MapPoiDef poi, MapScrollViewer map, PoiTreeViewItem parent = null) : base()
        {
            map_ = map;
            parent_ = parent;
            // group or item ?
            if (parent == null)
            {
                Label = poi.group.name;
                var icon = poi.group.icon;
                var check = ResFiles.Contains(icon);
                if (check)
                {
                    BitmapImage src = new BitmapImage();
                    src.BeginInit();
                    src.StreamSource = new FileStream(ResFiles.Get(icon), FileMode.Open, FileAccess.Read, FileShare.Read);
                    src.EndInit();
                    IconRes = src;
                }
                Childrens = new ObservableCollection<PoiTreeViewItem>();
            }
            else
            {
                this.poiDef_ = poi;
                Label = poi.Label;
            }
        }

        public void FinalizeInit(MapScrollViewer map)
        {
            map_ = map;
            if (Childrens != null)
            {
                foreach (var child in Childrens)
                {
                    child.FinalizeInit(map);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
