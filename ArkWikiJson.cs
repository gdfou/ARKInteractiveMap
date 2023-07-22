﻿using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Windows;
using System.Diagnostics;
using System.Text;
using ARKInteractiveMap.Properties;

namespace ARKInteractiveMap
{
    public class JsonSizeConverter : JsonConverter<ArkWikiJsonSize>
    {
        public override void Write(Utf8JsonWriter writer, ArkWikiJsonSize value, JsonSerializerOptions options)
        {
        }

        public override ArkWikiJsonSize Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Size format : integer, float ou array with two values
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                reader.Read();
                var elements = new List<float>();
                while (reader.TokenType != JsonTokenType.EndArray)
                {
                    elements.Add(JsonSerializer.Deserialize<float>(ref reader, options));
                    reader.Read();
                }
                return new ArkWikiJsonSize(elements[0], elements[1]);
            }
            else
            {
                var value = reader.GetSingle();
                return new ArkWikiJsonSize(value);
            }
        }
    }

    public class ArkWikiJsonSizeBasic
    {
        public float width { get; set; }
        public float heigth { get; set; }
    }

    [JsonConverter(typeof(JsonSizeConverter))]
    public class ArkWikiJsonSize
    {
        public float width { get; set; }
        public float height { get; set; }
        public ArkWikiJsonSize(float width, float height = float.NaN)
        {
            this.width = width;
            this.height = height;
        }
    }

    public class ArkWikiJsonMarker
    {
        public float lat { get; set; }
        public float lon { get; set; }
        public string name { get; set; }
        public int id { get; set; }
        public string description { get; set; }
        public string icon { get; set; }
        public bool isWikitext { get; set; }

        public void Merge(ArkWikiJsonMarker src)
        {
            // Copie générique des propriétess
            foreach (PropertyInfo property in src.GetType().GetProperties())
            {
                var value = property.GetValue(src);
                if (value != null)
                {
                    var dst = GetType().GetProperties().FirstOrDefault(p => p.Name == property.Name);
                    dst.SetValue(this, value);
                }
            }
        }
    }

    public class ArkWikiJsonGroup
    {
        public string groupName { get; set; }
        public string name { get; set; }
        public string borderColor { get; set; }
        public string fillColor { get; set; }
        public string icon { get; set; }
        public string iconCollected { get; set; }
        public string subtleText { get; set; }
        public string overrideIcon { get; set; }
        public bool isCollectible { get; set; }
        public ArkWikiJsonSize size { get; set; }
        public ArkWikiJsonSize sizeCollected { get; set; }

        public void Merge(ArkWikiJsonGroup src)
        {
            // Copie générique des propriétess
            foreach (PropertyInfo property in src.GetType().GetProperties())
            {
                var value = property.GetValue(src);
                if (value != null)
                {
                    var dst = GetType().GetProperties().FirstOrDefault(p => p.Name == property.Name);
                    dst.SetValue(this, value);
                }
            }
        }
    }

    public class ArkWikiJsonBackground
    {
        public string name { get; set; }
        public string image { get; set; }
        public List<List<float>> at { get; set; }
    }

    public class ArkWikiJson
    {
        //[JsonPropertyName("$schema")]
        //public string schema { get; set; }
        [JsonPropertyName("$mixin")]
        public bool mixin { get; set; }
        public List<string> mixins { get; set; }
        public Dictionary<string, ArkWikiJsonGroup> groups { get; set; }
        public Dictionary<string, ArkWikiJsonGroup> layers { get; set; }
        public Dictionary<string, List<ArkWikiJsonMarker>> markers { get; set; }
        public List<ArkWikiJsonBackground> backgrounds { get; set; }

        public void Merge(ArkWikiJson src)
        {
            // Copie générique des propriétess
            foreach (PropertyInfo property in src.GetType().GetProperties())
            {
                var src_value = property.GetValue(src);
                if (src_value != null)
                {
                    var dst_property = GetType().GetProperties().First(p => p.Name == property.Name);
                    var dst_value = dst_property.GetValue(this);
                    if (dst_value != null)
                    {
                        // Check type Dictionary ou List
                        if (dst_value.GetType() == typeof(Dictionary<string, ArkWikiJsonGroup>))
                        {
                            var src_var = src_value as Dictionary<string, ArkWikiJsonGroup>;
                            var dst_var = dst_value as Dictionary<string, ArkWikiJsonGroup>;
                            foreach (var src_item in src_var)
                            {
                                var dst_item = dst_var.FirstOrDefault(x => x.Key == src_item.Key);
                                if (dst_item.Value != null)
                                {
                                    dst_item.Value.Merge(src_item.Value);
                                }
                                else
                                {
                                    dst_var.Add(src_item.Key, src_item.Value);
                                }
                            }
                        }
                        else if (dst_value.GetType() == typeof(Dictionary<string, List<ArkWikiJsonMarker>>))
                        {
                            var src_var = src_value as Dictionary<string, List<ArkWikiJsonMarker>>;
                            var dst_var = dst_value as Dictionary<string, List<ArkWikiJsonMarker>>;
                            foreach (var src_item in src_var)
                            {
                                var dst_item = dst_var.FirstOrDefault(x => x.Key == src_item.Key);
                                if (dst_item.Value != null)
                                {
                                    Console.WriteLine($"les markers {src_item.Key} exitsent déjà !");
                                }
                                else
                                {
                                    dst_var.Add(src_item.Key, src_item.Value);
                                }
                            }
                        }
                        else if (dst_value.GetType() == typeof(List<ArkWikiJsonBackground>))
                        {
                            Console.WriteLine($"Merge json background non implémenté !");
                            var src_var = src_value as List<ArkWikiJsonBackground>;
                            var dst_var = dst_value as List<ArkWikiJsonBackground>;
                            foreach (var src_item in src_var)
                            {
                                /*var dst_item = dst_var.First(x => x == src_item.Key);
                                if (dst_item.Value != null)
                                {
                                    dst_item.Value.Merge(src_item.Value);
                                }
                                else
                                {
                                    dst_var.Add(src_item.Key, src_item.Value);
                                }*/
                            }
                        }
                    }
                    else
                    {
                        dst_property.SetValue(this, src_value);
                    }
                }
            }
        }
    }

    static public class ArkWiki
    {
        static private ArkWikiJson LoadWikiGGJsonMixins(string ressourceName, bool disabledWarning=false)
        {
            ArkWikiJson values = null;
            try
            {
                using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ressourceName);
                if (stream == null)
                {
                    if (!disabledWarning)
                    {
                        Console.WriteLine($"Ressource {ressourceName} non trouvé !");
                    }
                    return null;
                }
                using StreamReader reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                // Cartes/Définitions des groupes normés => groups {name, fillColor, size, icon, borderColor}
                // Cartes/Obélisques Scorched Earth      => markers
                values = JsonSerializer.Deserialize<ArkWikiJson>(json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error accessing resources ! ({ex.Message})");
            }
            return values;
        }

        static public void LoadWikiGGJsonRessourceName(MapDefItem mapDef,
                                                       string jsonResName, 
                                                       Dictionary<string, MapPoiDef> poiDict,
                                                       List<string> contentList,
                                                       List<CollectibleTreeViewItem> collectibleList,
                                                       Dictionary<string, string> expNoteList)
        {
            try
            {
                using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(jsonResName);
                if (stream == null)
                {
                    Console.WriteLine($"Ressource {jsonResName} non trouvé !");
                    return;
                }
                using StreamReader reader = new StreamReader(stream);
                // mixins      -> Cartes/Définitions des groupes normés -> couleurs, tailles, icons et noms en français
                // mixins      -> Cartes/Obélisques Scorched Earth      -> position obélisques
                // backgrounds -> cartes dispo
                // markers     -> liste des éléments dispo
                ArkWikiJson mainJson = JsonSerializer.Deserialize<ArkWikiJson>(reader.ReadToEnd());
                if (mainJson != null)
                {
                    // mixins -> merge
                    if (mainJson.mixins != null)
                    {
                        foreach (var mixin in mainJson.mixins)
                        {
                            var jsonMixin = LoadWikiGGJsonMixins("ARKInteractiveMap.Ressources." + mixin.Replace('/', '.').Replace(' ', '_') + ".json");
                            if (jsonMixin != null && jsonMixin.mixin)
                            {
                                mainJson.Merge(jsonMixin);

                                // Patch ?
                                string newResName = mixin + "_Patch";
                                jsonMixin = LoadWikiGGJsonMixins("ARKInteractiveMap.Ressources." + newResName.Replace('/', '.').Replace(' ', '_') + ".json", true);
                                if (jsonMixin != null)
                                {
                                    mainJson.Merge(jsonMixin);
                                }
                            }
                        }
                    }
                    // backgrounds
                    if (mainJson.backgrounds != null)
                    {
                        if (mapDef.mapPicture == null)
                        {
                            var background = mainJson.backgrounds.ToList().FindLast(x => x.name == "Topographique");
                            if (background != null)
                            {
                                mapDef.mapPicture = background.image.Replace(' ', '_');
                            }
                            else // pas de carte topo -> on prend celle là
                            {
                                background = mainJson.backgrounds[0];
                                mapDef.mapPicture = background.image.Replace(' ', '_');
                            }
                            // background.at must be [[x1][y1],[x2][y2]]
                            if ((background.at != null) && (background.at.Count == 2) && (background.at[0].Count == 2) && (background.at[1].Count == 2))
                            {
                                mapDef.mapSize = new MapSize(background.at[0][1], background.at[0][0], background.at[1][1], background.at[1][0]);
                            }
                        }
                    }
                    var groups = new Dictionary<string, ArkWikiJsonGroup>();
                    // Add expNoteList to groups
                    foreach (var exp in expNoteList)
                    {
                        groups[exp.Key] = new ArkWikiJsonGroup()
                        {
                            name = exp.Key,
                            overrideIcon = exp.Value,
                            groupName = exp.Key
                        };
                    }
                    // groups
                    if (mainJson.groups != null)
                    {
                        foreach (var group in mainJson.groups)
                        {
                            group.Value.groupName = group.Key;
                            groups.Add(group.Key, group.Value);
                        }
                    }
                    // layers
                    if (mainJson.layers != null)
                    {
                        foreach (var layer in mainJson.layers)
                        {
                            layer.Value.groupName = layer.Key;
                            groups.Add(layer.Key, layer.Value);
                        }
                    }
                    // markers
                    if (mainJson.markers != null)
                    {
                        foreach (var markers in mainJson.markers)
                        {
                            var groupName = MapPoiDef.extractGroupName(markers.Key);
                            if (!groups.ContainsKey(groupName))
                            {
                                Console.WriteLine($"Il manque la définition de {groupName}");
                            }
                            var group = groups[groupName];
                            if (contentList.FirstOrDefault(x => x == groupName) == null)
                            {
                                contentList.Add(groupName);
                            }
                            // Collectible
                            CollectibleTreeViewItem collectible = null;
                            if (group.isCollectible)
                            {
                                collectible = collectibleList.FirstOrDefault(x => x.Name == groupName);
                                if (collectible == null)
                                {
                                    collectible = new CollectibleTreeViewItem(groupName, group); // Need to call FinalizeInit after !
                                    collectibleList.Add(collectible);
                                }
                            }
                            foreach (var marker in markers.Value)
                            {
                                try
                                {
                                    var poi = new MapPoiDef(markers.Key, marker, group, groups);
                                    poiDict[poi.Id] = poi;
                                    // Collectible
                                    if (collectible != null)
                                    {
                                        // Need to call FinalizeInit after !
                                        collectible.Childrens.Add(new CollectibleTreeViewItem(groupName, group, poi, collectible));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Error accessing resources ! ({ex.Message})");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error accessing resources ! ({ex.Message})");
            }
        }
    }
}
