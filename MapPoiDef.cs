using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ARKInteractiveMap
{
    public enum MapPoiCategory
    {
        Wiki,
        IngamePoi
    }

    public enum MapPoiShape
    {
        None,
        Ellipse,
        Icon,
        Triangle,
        Pie
    }

    public class MapPos
    {
        public float lat;
        public float lon;
        public MapPos(ArkWikiJsonMarker marker)
        {
            lat = marker.lat;
            lon = marker.lon;
        }
        public MapPos(float lat, float lon)
        {
            this.lat = lat;
            this.lon = lon;
        }
        public MapPos(double lat, double lon)
        {
            this.lat = (float)lat;
            this.lon = (float)lon;
        }
    }

    public class MapPoiDef
    {
        protected string label_;
        protected string uuid_;  // unique id (needed by editable poi)
        public MapPoiCategory category;
        public string groupName;
        public string fullGroupName;
        public int item_id;
        public string collectibleName;
        public MapPos pos; // pos en coordonnée sur la carte
        public bool inCave;
        public string borderColor;
        public string fillColor;
        public string icon;
        public string iconCollected;
        public bool isCollectible;
        public ArkWikiJsonSize size;
        public ArkWikiJsonSize sizeCollected;
        public MapPoiShape shape;

        protected static string GenerateUUID()
        {
            Guid myuuid = Guid.NewGuid();
            return myuuid.ToString();
        }

        public string Label
        {
            get => label_;
            set
            {
                if (label_ != value)
                {
                    label_ = value;
                }
            }
        }

        public string Id
        {
            get
            {
                if (groupName == "ingame-map-poi" || groupName == "user-map-poi")
                {
                    // Id format: <groupName>#<uuid>
                    return $"{groupName}#{uuid_}";
                }
                else
                {
                    // Id format: <groupName>#<lat;lon>
                    return $"{groupName}#{pos.lat};{pos.lon}";
                }
            }
        }

        public string CollectibleLabel
        {
            get => collectibleName;
        }

        // Extraction groupe
        public static string extractGroupName(string value)
        {
            // "crystal cave"                 => crystal
            // "artifact cg:1 cc:immune"      => artifact
            // "surface-crate cg:27 cc:bgw"   => surface-crate
            // "cave-crate cave cg:11 Ccc:br" => cave-crate
            // "sea-crate cg:22"              => sea-crate
            return value.Split(' ')[0];
        }

        public static string getIconResname(string icon)
        {
            return (icon != null) ? "ARKInteractiveMap.Ressources.Icons." + icon.Replace(' ', '_') : null;
        }

        // "Dossier: Ver des sables <span class=\"datamap-explorer-note-id\">(ID: 1)</span>"
        // "Note: Note de Dahkeya #1 <span class=\"datamap-explorer-note-id\">(ID: 100)</span>"
        // "Chroniques de Genesis 2 #10 <span class=\"datamap-explorer-note-id\">(ID: 310)</span>"
        // "Découverte d'HLN-A #4 <span class=\"datamap-explorer-note-id\">(ID: 304)</span>"
        public static (string, int) ExtractLabelFromWikiExplorerPoiName(string value)
        {
            int poi_id = 0;
            string poi_label = null;
            try
            {
                // extraction de l'id
                int idx = value.IndexOf("(ID:");
                if (idx >= 0)
                {
                    int idx_end = value.IndexOf(")", idx);
                    if (idx_end > 0)
                    {
                        poi_id = Convert.ToInt32(value.Substring(idx + 4, idx_end - idx - 4));
                    }
                }
                // Extraction du label
                idx = value.IndexOf("<");
                if (idx >= 0)
                {
                    int idx_start = value.IndexOf(":", 0, idx);
                    if (idx_start > 0)
                    {
                        poi_label = value.Substring(idx_start + 1, idx - idx_start - 1).Trim();
                    }
                    else
                    {
                        poi_label = value.Substring(0, idx).Trim();
                    }
                }
            }
            catch { }
            return (poi_label, poi_id);
        }

        public MapPoi BuildMapPoi(MapScrollViewer map)
        {
            switch (shape)
            {
                case MapPoiShape.Ellipse:  return new MapPoiEllipse(this, map);
                case MapPoiShape.Icon:     return new MapPoiIcon(this, map);
                case MapPoiShape.Triangle: return new MapPoiTriangle(this, map);
                case MapPoiShape.Pie:      return new MapPoiPie(this, map);
                default:
                    Console.WriteLine($"ERREUR: pas de forme définie pour '{groupName}:{Label}' !");
                    return null;
            }
        }

        static public FrameworkElement BuildForContents(string groupName, ArkWikiJsonGroup group, MapPoiCategory category, int size)
        {
            if (group != null && group.size.width > 0)
            {
                if (group.fillColor != null)
                {
                    return new MapPoiEllipse(group).BuildForContents(size);
                }
                else if (group.iconCollected != null)
                {
                    return new MapPoiIcon(group).BuildForContents(size);
                }
                else if (groupName.Contains("surface-crate"))
                {
                    return new MapPoiPie(group).BuildForContents(size);
                }
            }
            else if (category == MapPoiCategory.IngamePoi)
            {
                group = new ArkWikiJsonGroup()
                {
                    size = new ArkWikiJsonSize(10),
                    fillColor = "#ff0000"
                };
                return new MapPoiTriangle(group).BuildForContents(size);
            }
            return null;
        }

        public MapPoiDef()
        {
        }

        public MapPoiDef(MapPoiCategory category, string groupName, ArkWikiJsonMarker marker, ArkWikiJsonGroup group, Dictionary<string, ArkWikiJsonGroup> groups = null)
        {
            this.category = category;
            this.fullGroupName = groupName;
            this.groupName = extractGroupName(groupName);
            if (marker != null)
            {
                pos = new MapPos(marker);
                item_id = marker.id;
                Label = (marker.name != null) ? marker.name : group.name;
            }
            isCollectible = group.isCollectible;
            // process size, color, ...
            borderColor = group.borderColor;
            fillColor = group.fillColor;
            size = group.size;
            icon = getIconResname(group.icon);
            if (group.iconCollected != null)
            {
                iconCollected = getIconResname(group.iconCollected);
            }
            if (group.sizeCollected != null)
            {
                sizeCollected = group.sizeCollected;
            }
            if (marker?.icon != null)
            {
                icon = getIconResname(marker.icon);
            }
            if (fillColor != null && size != null)
            {
                shape = MapPoiShape.Ellipse;
                if (borderColor == null)
                {
                    borderColor = "#fff";
                    group.borderColor = borderColor;
                }
            }
            // groupName
            // "crystal cave" => crystal + inCave
            var split = groupName.Split(' ');
            if (split.Length > 1 && split[1] == "cave")
            {
                inCave = true;
            }
            switch (this.groupName)
            {
                case "artifact":
                    {
                        // "artifact cg:1 cc:immune" => artifact + type [cg:index cc:artifact_id]
                        collectibleName = $"{Label} (lat {pos.lat}, lon {pos.lon})";
                        if (groups != null && split.Length == 3 && split[2].Contains("cc:"))
                        {
                            if (groups.TryGetValue(split[2], out var info))
                            {
                                if (info.overrideIcon != null)
                                {
                                    icon = getIconResname(info.overrideIcon);
                                }
                            }
                        }
                        break;
                    }
                case "surface-crate":
                    {
                        // "surface-crate cg:27 cc:bgw" => surface-crate + type [cg:inde cc:bgw] => bgw: code couleur [blue-green-white]
                        shape = MapPoiShape.Pie;
                        break;
                    }
                case "cave-crate":
                    {
                        // "cave-crate cave cg:11 Ccc:br" => cave-crate + in_cave + type [cg:index Ccc:br] => br: code couleur [blue-red]
                        //shape = MapPoiShape.Pie;
                        break;
                    }
                case "sea-crate":
                    {
                        // "sea-crate cg:22" => sea-crate + type [cg:index]
                        break;
                    }
                case "dossier":
                    {
                        // label = 'Dossier: Insecte jarre <span class=\"datamap-explorer-note-id\">(ID: 93)</span>' => 'Insecte jarre (ID: 93)'
                        (var poi_label, var poi_id) = ExtractLabelFromWikiExplorerPoiName(Label);
                        if (poi_label != null)
                        {
                            Label = poi_label;
                            item_id = poi_id;
                        }
                        collectibleName = $"{Label} (ID: {item_id}, lat {pos.lat}, lon {pos.lon})";
                        break;
                    }

                case "explorer-note":
                    {
                        // 'Note: Note de Dahkeya #1 <span class=\"datamap-explorer-note-id\">(ID: 100)</span>'
                        (var poi_label, var poi_id) = ExtractLabelFromWikiExplorerPoiName(Label);
                        if (poi_label != null)
                        {
                            Label = poi_label;
                            item_id = poi_id;
                        }
                        collectibleName = $"{Label} (ID: {item_id}, lat {pos.lat}, lon {pos.lon})";
                        if (groups != null)
                        {
                            var split_label = Label.Split(' ','\'');
                            foreach (var str in split_label)
                            {
                                if (groups.TryGetValue(str, out var info))
                                {
                                    icon = getIconResname(info.overrideIcon);
                                    break;
                                }
                            }
                        }
                        break;
                    }

                case "glitch":
                    {
                        (var poi_label, var poi_id) = ExtractLabelFromWikiExplorerPoiName(Label);
                        if (poi_label != null)
                        {
                            Label = poi_label;
                            item_id = poi_id;
                        }
                        collectibleName = $"{Label} (ID: {item_id}, lat {pos.lat}, lon {pos.lon})";
                        break;
                    }
            }
            if (isCollectible && collectibleName == null && pos != null)
            {
                collectibleName = $"lat {pos.lat}, lon {pos.lon}";
            }
            if (shape == MapPoiShape.None && icon != null)
            {
                var app_res_list = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                if (app_res_list.Contains(icon))
                {
                    if (iconCollected != null && !app_res_list.Contains(iconCollected))
                    {
                        Console.WriteLine($"il manque l'icon {iconCollected}");
                    }
                    shape = MapPoiShape.Icon;
                }
                else
                {
                    Console.WriteLine($"il manque l'icon {icon}");
                }
            }
        }

        // User map poi from IngameMarker
        public MapPoiDef(string groupName, IngameMarker ingameMarker)
        {
            uuid_ = GenerateUUID();
            category = MapPoiCategory.IngamePoi;
            this.groupName = groupName;
            pos = new MapPos(ingameMarker.floatLat, ingameMarker.floatLon);
            Label = ingameMarker.Name;
            fillColor = ingameMarker.Color;
            size = new ArkWikiJsonSize(20);
            shape = MapPoiShape.Triangle;
        }
    }
}
