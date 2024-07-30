using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ARKInteractiveMap
{
    public class MapCoord
    {
        public MapPos offset { get; set; }
        public MapPos mult { get; set; }
    }

    public class MapListJsonMapBorder
    {
        public int width { get; set; }
        public string color { get; set; }
    }

    public class MapListJsonMap
    {
        public string name { get; set; }
        public string resource { get; set; }
        public string exploration { get; set; }
        public MapListJsonMapBorder map_border { get; set; }
        public MapCoord map_coord { get; set; }
    }

    public class MapListJsonMaps
    {
        public string name { get; set; }
        public List<MapListJsonMap> maps { get; set; }
    }

    public class MapListJsonMapGames
    {
        public string game { get; set; }
        public List<MapListJsonMaps> maps { get; set; }
    }

    public class MapListJsonGames
    {
        public string game { get; set; }
        public string shortcut { get; set; }
        public List<string> list{ get; set; }
        override public string ToString() { return game; }
    }

    public class MapListJson
    {
        public List<MapListJsonGames> games { get; set; }
        public List<MapListJsonMapGames> maps { get; set; }

        static public MapListJson LoadFromResource(string jsonResName)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(jsonResName);
            using StreamReader reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<MapListJson>(reader.ReadToEnd(), new JsonSerializerOptions {ReadCommentHandling=JsonCommentHandling.Skip});
        }

        static public MapListJson LoadFromFile(string jsonFileName)
        {
            if (!File.Exists(jsonFileName))
            {
                string json = JsonSerializer.Serialize(new MapListJson(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonFileName, json);
            }
            string lines = File.ReadAllText(jsonFileName);
            return JsonSerializer.Deserialize<MapListJson>(lines);
        }
    }
}
