using System;
using System.ComponentModel;

namespace ARKInteractiveMap
{
    public class IngameMarker : INotifyPropertyChanged, ICloneable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
        protected string name_;
        protected string color_;
        protected string lat_;
        protected string lon_;
        protected string shape_;

        public string Id { get; set; }

        public object Clone()
        {
            return new IngameMarker()
            {
                name_ = name_,
                color_ = color_,
                lat_ = lat_,
                lon_ = lon_,
                Id = Id
            };
        }

        public string Name
        {
            get => name_;
            set
            {
                if (value != name_)
                {
                    name_ = value;
                    NotifyPropertyChanged("Name");
                }
            }
        }

        public string Color
        {
            get => color_;
            set
            {
                if (value != color_)
                {
                    color_ = value;
                    NotifyPropertyChanged("Color");
                }
            }
        }

        public string Lat
        {
            get => lat_;
            set
            {
                if (value != lat_)
                {
                    lat_ = value;
                    NotifyPropertyChanged("Lat");
                }
            }
        }
        public float floatLat
        { 
            get => Convert.ToSingle(lat_);
            set => lat_ = value.ToString();
        }

        public string Lon
        {
            get => lon_;
            set
            {
                if (value != lon_)
                {
                    lon_ = value;
                    NotifyPropertyChanged("Lon");
                }
            }
        }
        public float floatLon
        {
            get => Convert.ToSingle(lon_);
            set => lon_ = value.ToString();
        }

        public MapPos MapPos => new MapPos(floatLat, floatLon);

        public string Shape
        {
            get => shape_;
            set => shape_ = value;
        }

        public IngameMarker()
        {
            Name = "";
            Color = "#ffffffff";
            Lat = "0";
            Lon = "0";
            shape_ = "triangle";
        }
        
        public IngameMarker(JsonIngamePoi poi, string id)
        {
            Id = id;
            Name = poi.name;
            floatLat = poi.lat;
            floatLon = poi.lon;
            Color = poi.color;
            Shape = poi.shape;
        }
    }
}
