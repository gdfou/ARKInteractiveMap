using System.Collections.Generic;
using System.Windows.Media;

namespace ARKInteractiveMap
{
    public class MapDefBorder
    {
        public int width;
        public Brush color;
    }

    public class MapDefItem
    {
        public string name;   // 'Midgard' sur 'Fjordur' sinon copie 
        public string folder; // copie
        public string resource;
        public string exploration;
        public MapDefBorder border;
        public string mapPicture;    // 'Scorched_Earth_Topographic_Map.jpg'
        public MapSize mapSize; // [ 7.2, 7.2 ],[ 92.8, 92.8 ], 1024 x 1024
        public MapCoord mapCoord;

        override public string ToString()
        {
            return name;
        }
    }

    public class MapDef
    {
        public string _name;   // 'Scorched Earth'
        public string folder; // 'Scorched_Earth'
        public List<MapDefItem> maps;
        public MapDefItem currentMap;

        public string Name
        {
            get 
            {
                if (maps.Count == 1)
                {
                    return _name;
                }
                else
                {
                    return $"{_name}.{currentMap.name}";
                }
            }
        }

        public MapDef(MapListJsonItem mapDef)
        {
            this._name = mapDef.name;
            this.folder = mapDef.folder;
            this.maps = new List<MapDefItem>();
            foreach (var map_def_item in mapDef.maps)
            {
                var maps_item = new MapDefItem()
                {
                    name = map_def_item.name ?? mapDef.name,
                    folder = mapDef.folder,
                    resource = map_def_item.resource,
                    exploration = map_def_item.exploration
                };
                if (map_def_item.map_border != null)
                {
                    maps_item.border = new MapDefBorder()
                    {
                        width = map_def_item.map_border.width,
                        color = (SolidColorBrush)new BrushConverter().ConvertFrom(map_def_item.map_border.color)
                    };
                }
                else
                {
                    maps_item.border = new MapDefBorder()
                    {
                        width = 0,
                        color = new SolidColorBrush(Colors.Black)
                    };
                }
                if (map_def_item.map_coord != null)
                {
                    maps_item.mapCoord = map_def_item.map_coord;
                }
                maps.Add(maps_item);
            }
            currentMap = maps[0];
        }

        public bool IsMainMap(string name)
        {
            if (name == null) 
                return false;
            if (name.Contains(".")) // with a sub-map ?
            {
                var main_map = name.Split('.')[0];
                return main_map == this._name;
            }
            else if (this._name == name)
            {
                return true;
            }
            return false;
        }

        public void SelectSubMap(string name)
        {
            if (name.Contains(".")) // with a sub-map ?
            {
                name = name.Split('.')[1];
            }
            var sub_map = maps.FindLast(map => map.name == name);
            if (sub_map != null)
            {
                currentMap = sub_map;
            }
        }

        override public string ToString()
        {
            return _name;
        }
    }
}
