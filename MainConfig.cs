using System;
using System.Collections.Generic;

namespace ARKInteractiveMap
{
    public class JsonPoint
    {
        public int x { get; set; }
        public int y { get; set; }
    }
    public class JsonRect
    {
        public int left { get; set; }
        public int top { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }
    public class JsonIngamePoi
    {
        public string name { get; set; }
        public string color { get; set; }
        public float lat { get; set; }
        public float lon { get; set; }
    }
    public class JsonMapDef
    {
        public JsonPoint origin { get; set; }
        public double scale { get; set; }
        public Dictionary<string, bool> map_poi_visible { get; set; }
        public Dictionary<string, bool> layers_visible { get; set; }
        public Dictionary<string, List<string>> map_poi_collected { get; set; }
        public List<JsonIngamePoi> ingame_map_poi { get; set; }
        public List<JsonIngamePoi> user_map_poi { get; set; }
        public List<int> fog_of_wars { get; set; }

        public JsonMapDef()
        {
            scale = 1;
            origin = new JsonPoint();
            map_poi_visible = new Dictionary<string, bool>();
            layers_visible = new Dictionary<string, bool>();
        }
    }
    public class MainConfig
    {
        public JsonRect window { get; set; }
        public bool fog_of_wars { get; set; }
        public bool auto_import_local_data { get; set; }
        public string ark_save_folder { get; set; }
        public string splitter_pos { get; set; }
        public string map { get; set; }
        public Dictionary<string, JsonMapDef> map_def { get; set; }
    }
}
