using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace ARKInteractiveMap
{
    public enum MapPoiShape
    {
        None,
        Ellipse,
        Icon,
        Triangle,
        Pie,
        Letter,
    }

    public class MapPos
    {
        public float lat { get; set; }
        public float lon { get; set; }

        public MapPos()
        {
        }
        public MapPos(ArkWikiJsonMarker marker)
        {
            lat = (marker.lat == 0) ? marker.y : marker.lat;
            lon = (marker.lon == 0) ? marker.x : marker.lon;
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
        protected string layer_;
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
        public ArkWikiJsonGroup group;
        public string shape;
        public bool userPoi;

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

        public string Layer
        {
            get => layer_;
            set => layer_ = value;
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
            return icon;
        }

        // "Dossier: Ver des sables <span class=\"datamap-explorer-note-id\">(ID: 1)</span>"
        // "Dossier Ver des sables <span class=\"datamap-explorer-note-id\">(ID: 1)</span>"
        // "Note: Note de Dahkeya #1 <span class=\"datamap-explorer-note-id\">(ID: 100)</span>"
        // "Note de Dahkeya #1 <span class=\"datamap-explorer-note-id\">(ID: 100)</span>"
        // "Chroniques de Genesis 2 #10 <span class=\"datamap-explorer-note-id\">(ID: 310)</span>"
        // "Découverte d'HLN-A #4 <span class=\"datamap-explorer-note-id\">(ID: 304)</span>"
        public static (string, int) ExtractLabelFromWikiExplorerPoiName(string value, bool dossier=false)
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
                    int sub_idx = 0;
                    int sub_len = idx;
                    int idx_start = value.IndexOf(":", 0, idx);
                    if (idx_start > 0)
                    {
                        sub_idx = idx_start + 1;
                        sub_len = idx - idx_start - 1;
                    }
                    else if (dossier)
                    {
                        idx_start = value.IndexOf("Dossier ");
                        if (idx_start >= 0)
                        {
                            sub_idx = 8;
                            sub_len = idx - 8;
                        }
                    }
                    poi_label = value.Substring(sub_idx, sub_len).Trim();
                }
            }
            catch { }
            return (poi_label, poi_id);
        }

        public static string GetShapeName(string shapeText)
        {
            return shapeText?.Split('#')[0];
        }
        public static string GetShapeParam(string shapeText)
        {
            var split = shapeText.Split('#');
            return split.Length == 2 ? split[1] : null;
        }

        public MapPoi BuildMapPoi(MapScrollViewer map)
        {
            switch (GetShapeName(shape))
            {
                case "ellipse":  return new MapPoiEllipse(this, map);
                case "icon":     return new MapPoiIcon(this, map);
                case "triangle": return new MapPoiTriangle(this, map, GetShapeParam(shape));
                case "pie":      return new MapPoiPie(this, map);
                case "letter":   return new MapPoiLetter(this, map, GetShapeParam(shape));
                default:
                    Console.WriteLine($"ERREUR: pas de forme définie pour '{groupName}:{Label}' !");
                    return null;
            }
        }

        public static FrameworkElement BuildForContents(MapPoi poi, int size)
        {
            switch (GetShapeName(poi.Shape))
            {
                case "ellipse": return new MapPoiEllipse(poi.poiDef, null).BuildForContents(size);
                case "icon": return new MapPoiIcon(poi.poiDef, null).BuildForContents(size);
                case "triangle": return new MapPoiTriangle(poi.poiDef, null, GetShapeParam(poi.Shape)).BuildForContents(size);
                case "pie": return new MapPoiPie(poi.poiDef, null).BuildForContents(size);
                case "letter": return new MapPoiLetter(poi.poiDef, null, GetShapeParam(poi.Shape)).BuildForContents(size);
                default:
                    Console.WriteLine($"BuildForContents Erreur: pas de forme définie pour '{poi.Shape}' !");
                    return null;
            }
        }

        public MapPoiDef()
        {
        }

        public MapPoiDef(string groupName, ArkWikiJsonMarker marker, ArkWikiJsonGroup group, Dictionary<string, ArkWikiJsonGroup> groups = null)
        {
            this.group = group;
            this.fullGroupName = groupName;
            this.groupName = extractGroupName(groupName);
            if (marker != null)
            {
                pos = new MapPos(marker);
                item_id = marker.uid;
                Label = (marker.name != null) ? marker.name : group.name;
            }
            isCollectible = (group.isCollectible == true);
            // process size, color, ...
            borderColor = (group.borderColor != null) ? group.borderColor : group.strokeColor;
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
                shape = "ellipse";
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
                case "osd-crate":
                    {
                        // "surface-crate cg:27 cc:bgw" => surface-crate + type [cg:inde cc:bgw] => bgw: code couleur [blue-green-white]
                        shape = "pie";
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
                        (var poi_label, var poi_id) = ExtractLabelFromWikiExplorerPoiName(Label, true);
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
            if (String.IsNullOrEmpty(shape) && icon != null)
            {
                if (ResFiles.Contains(icon))
                {
                    if (iconCollected != null && !ResFiles.Contains(iconCollected))
                    {
                        Console.WriteLine($"il manque l'icon '{iconCollected}'");
                    }
                    shape = "icon";
                }
                else
                {
                    Console.WriteLine($"il manque l'icon '{icon}'");
                }
            }
        }

        // User map poi from IngameMarker
        public MapPoiDef(string groupName, IngameMarker ingameMarker)
        {
            uuid_ = GenerateUUID();
            userPoi = true;
            this.groupName = groupName;
            pos = new MapPos(ingameMarker.floatLat, ingameMarker.floatLon);
            Label = ingameMarker.Name;
            fillColor = ingameMarker.Color;
            shape = ingameMarker.Shape;
            if (String.IsNullOrEmpty(shape)) {
                shape = groupName == "ingame-map-poi" ? "triangle" : "letter#?";
            }
            size = shape.Contains("triangle") ? new ArkWikiJsonSize(20) : new ArkWikiJsonSize(50);
        }
    }
}
