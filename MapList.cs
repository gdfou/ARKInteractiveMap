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

    public class MapListJsonItem
    {
        public string name { get; set; }
        public string folder { get; set; }
        public List<MapListJsonMap> maps { get; set; }
    }

    public class MapListJson
    {
        public List<string> list { get; set; }
        public List<MapListJsonItem> maps { get; set; }

        static public MapListJson LoadFromResource(string jsonResName)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(jsonResName);
            using StreamReader reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<MapListJson>(reader.ReadToEnd(), new JsonSerializerOptions {ReadCommentHandling=JsonCommentHandling.Skip});
        }
    }
}
