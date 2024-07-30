using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace ARKInteractiveMap
{
    public class CollectibleTreeViewItem : INotifyPropertyChanged
    {
        protected bool isExpanded_;
        protected bool isCollected_;
        protected bool isHitTestVisible_;
        protected MapScrollViewer map_;
        protected CollectibleTreeViewItem parent_;
        protected MapPoiDef poiDef_;

        public string Name { get; set; }
        public string SortedLabel { get; set; }
        public string Label { get; set; }
        public BitmapImage IconRes { get; set; }

        // Interface TreeView
        public ObservableCollection<CollectibleTreeViewItem> Childrens { get; set; }

        public bool getCollected()
        {
            if (poiDef_ != null)
            {
                return isCollected_;
            }
            else
            {
                return (Childrens.Count(x => x.isCollected_ == true) == Childrens.Count);
            }
        }
        public bool IsCollected // Binding
        {
            get => getCollected();
            set
            {
                if (value != isCollected_ && poiDef_ != null)
                {
                    isCollected_ = value;
                    map_.UpdateCollected(poiDef_.Id, value);
                    if (parent_ != null)
                    {
                        parent_.NotifyPropertyChanged("IsCollected");
                    }
                }
            }
        }
        public void SetCollected(bool value)
        {
            if (value != isCollected_)
            {
                isCollected_ = value;
                NotifyPropertyChanged("IsCollected");
                map_.Save();
                if (parent_ != null)
                {
                    parent_.NotifyPropertyChanged("IsCollected");
                }
            }
        }

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

        public bool IsHitTestVisible // to disabled clic on CheckBox
        {
            get => isHitTestVisible_;
            set
            {
                if (value != isHitTestVisible_)
                {
                    isHitTestVisible_ = value;
                    NotifyPropertyChanged("IsHitTestVisible");
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

        public CollectibleTreeViewItem(string name, ArkWikiJsonGroup group, MapPoiDef poi = null, CollectibleTreeViewItem parent = null) : base()
        {
            parent_ = parent;
            Name = name;
            this.poiDef_ = poi;
            // icon or no icon ? if poi if defined no icon
            if (poi == null)
            {
                IsHitTestVisible = false;
                SortedLabel = group.name;
                Label = group.name;
                var check = ResFiles.Contains(group.icon);
                if (check)
                {
                    BitmapImage src = new BitmapImage();
                    src.BeginInit();
                    src.StreamSource = new FileStream(ResFiles.Get(group.icon), FileMode.Open, FileAccess.Read, FileShare.Read);
                    src.EndInit();
                    IconRes = src;
                }
                Childrens = new ObservableCollection<CollectibleTreeViewItem>();
            }
            else
            {
                IsHitTestVisible = true;
                SortedLabel = poi.Label;
                Label = poi.CollectibleLabel;
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

        public void UpdateCollected(List<string> idList)
        {
            if (Childrens != null)
            {
                Childrens.All(x => x.isCollected_ = false);
                foreach (var id in idList)
                {
                    var item = Childrens.FirstOrDefault(x => x.Id == id);
                    if (item != null)
                    {
                        item.IsCollected = true;
                    }
                    else if (id.Contains("#Expanded"))
                    {
                        SetExpanded(true);
                    }
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
