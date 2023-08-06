using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using ArkFileDecode;
using System.Globalization;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;
using System.Windows.Media;
using System.Text.RegularExpressions;
using System.Text;
using System.Diagnostics;

namespace ARKInteractiveMap
{
    /// <summary>
    /// Main Windows
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        string lastArkImportFolder_;
        int lockInterface_;
        MainConfig cfg_;
        Dictionary<string, MapDef> mapDefDict_;
        ObservableCollection<CollectibleTreeViewItem> collectibleList_;
        ObservableCollection<IngameMarker> ingameMarkerList_;
        ObservableCollection<IngameMarker> userMarkerList_;
        ObservableCollection<PoiTreeViewItem> poiList_;
        Dictionary<string, string> expNoteList_;
        int ingameMarkerListChanged_;

        public MainWindow()
        {
            DataContext = this;
            mapDefDict_ = new Dictionary<string, MapDef>();
            collectibleList_ = new ObservableCollection<CollectibleTreeViewItem>();
            ingameMarkerList_ = new ObservableCollection<IngameMarker>();
            userMarkerList_ = new ObservableCollection<IngameMarker>();
            poiList_ = new ObservableCollection<PoiTreeViewItem>();
            InitializeComponent();

#if !DEBUG
            mainMenu.Visibility = Visibility.Collapsed;
#endif
            LoadMainConfig();
            if (cfg_ == null)
            {
                cfg_ = new MainConfig()
                {
                    window = new JsonRect()
                };
            }
        }

        /// <summary>
        ///     Event raised when the Window has loaded.
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            lockInterface_++;
            mainMenu.Visibility = Visibility.Collapsed;

            // Load map list
            MapListJson mapList = MapListJson.LoadFromResource("ARKInteractiveMap.Ressources.MapList.json");
            if (mapList != null && mapList.maps != null)
            {
                foreach (var map in mapList.maps)
                {
                    mapDefDict_[map.name] = new MapDef(map);
                }
            }

            // Auto-import game file or use last imported poi
            if (cfg_.auto_import_local_data)
            {
                if (CheckArkSaveFolder())
                {
                    lastArkImportFolder_ = cfg_.ark_save_folder + @"\LocalProfiles";
                    ImportPlayerLocalDataFile(lastArkImportFolder_ + @"\PlayerLocalData.arkprofile");
                }
            }

            // Load explorator notes icon list
            expNoteList_ = LoadEploratorNotesIconList();

            // Load map def config from user config
            foreach (var map_name in mapList.list)
            {
                if (map_name == "-")
                {
                    comboBoxMap.Items.Add(new Separator());
                }
                else
                {
                    comboBoxMap.Items.Add(mapDefDict_[map_name]);
                }
            }
            var currentMapDef = mapDefDict_.FirstOrDefault(x => x.Value.IsMainMap(cfg_.map)).Value;
            if (currentMapDef == null)
            {
                currentMapDef = mapDefDict_["The Island"];
            }
            else if (currentMapDef.maps.Count > 0)
            {
                currentMapDef.SelectSubMap(cfg_.map);
            }
            comboBoxMap.SelectedItem = currentMapDef;

            mapViewer.UpdateCollectedEvent += MapViewer_UpdateCollectedEvent;
            mapViewer.SaveEvent += MapViewer_SaveEvent;
            mapViewer.CommandEvent += MapViewer_CommandEvent;

            mapViewer.ZoomInFull();
            mapViewer.MaxScale = 8;

            LoadMapDef(currentMapDef);

            trvCollectible.ItemsSource = collectibleList_;
            listviewIngameMarkers.ItemsSource = ingameMarkerList_;
            listviewUserMarkers.ItemsSource = userMarkerList_;
            trvPointOfInterest.ItemsSource = poiList_;

            lockInterface_--;
        }

        private void MapViewer_CommandEvent(object sender, CommandEventArgs e)
        {
            Command(e.Cmd, e.Id, e.Param);
        }

        public void Command(string cmd, string id=null, object param=null)
        {
            // <cmd>:<params>
            var split = cmd.Split(':');
            string cmd_arg = (split.Length == 2) ? split[1] : null;
            switch (split[0])
            {
                case "IngameMarkerEdit":
                    {
                        bool ingame_marker = string.IsNullOrEmpty(cmd_arg) ? true : (cmd_arg != "user");
                        ObservableCollection<IngameMarker> list = ingame_marker ? ingameMarkerList_ : userMarkerList_;
                        var item = list.FirstOrDefault(x => x.Id == id);
                        if (item != null)
                        {
                            EditIngameMarker(ingame_marker, item);
                        }
                        else
                        {
                            EditIngameMarker(ingame_marker, null, param as MapPos);
                        }
                        break;
                    }

                case "IngameMarkerDel":
                    {
                        bool ingame_marker = string.IsNullOrEmpty(cmd_arg) ? true : (cmd_arg != "user");
                        ObservableCollection<IngameMarker> list = ingame_marker ? ingameMarkerList_ : userMarkerList_;
                        var item = list.FirstOrDefault(x => x.Id == id);
                        if (item != null)
                        {
                            if (MessageBox.Show($"Voulez-vous vraiment supprimer le repère '{item.Name}' ?", this.Title, MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                            {
                                list.Remove(item);
                                mapViewer.RemovePoi(item.Id);
                                SaveMapDef(null);
                                if (ingame_marker)
                                    ingameMarkerListChanged_++;
                            }
                        }
                        break;
                    }

                case "IngameMarkerAdd": // + option param => MapPos
                    {
                        bool ingame_marker = string.IsNullOrEmpty(cmd_arg) ? true : (cmd_arg != "user");
                        EditIngameMarker(ingame_marker, null, param as MapPos);
                        break;
                    }

                case "UpdateVisible":
                    {
                        // UpdateVisible:layers-xxx
                        mapViewer.UpdateVisible(split[1], (split[2] == "True"));
                        break;
                    }
            }
        }
        public void Command(string cmd, List<string> ids)
        {
            if (ids.Count == 1)
            {
                Command(cmd, ids[0]);
            }
            else
            {
                if (cmd.Contains("IngameMarkerDel"))
                {
                    List<IngameMarker> list_igm = new List<IngameMarker>();
                    foreach (var id in ids)
                    {
                        var item = ingameMarkerList_.FirstOrDefault(x => x.Id == id);
                        if (item != null)
                        {
                            list_igm.Add(item);
                        }
                    }
                    string list_name = "";
                    for (int i = 0; i < list_igm.Count; i++)
                    {
                        list_name += $"'{list_igm[i].Name}'";
                        if (i == list_igm.Count - 2)
                        {
                            list_name += " et ";
                        }
                        else if (i < list_igm.Count - 1)
                        {
                            list_name += ", ";
                        }
                    }
                    if (MessageBox.Show($"Voulez-vous vraiment supprimer les repères {list_name} ?", this.Title, MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        foreach (var item in list_igm)
                        {
                            ingameMarkerList_.Remove(item);
                            mapViewer.RemovePoi(item.Id);
                        }
                        SaveMapDef(null);
                        ingameMarkerListChanged_++;
                    }
                }
            }
        }

        private void MapViewer_SaveEvent(object sender, EventArgs e)
        {
            // Save config
            SaveMainConfig();
        }

        // Event call when the collected property is changed on a poi on the map (user action)
        private void MapViewer_UpdateCollectedEvent(object sender, UpdateCollectedEventArgs e)
        {
            var name = e.Id.Split('#')[0];
            var itemList = collectibleList_.FirstOrDefault(x => x.Name == name);
            if (itemList != null)
            {
                var item = itemList.Childrens.FirstOrDefault(x => x.Id == e.Id);
                if (item != null)
                {
                    item.SetCollected(e.Collected);
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (ingameMarkerListChanged_ > 0)
            {
                if (MessageBox.Show($"La liste des répères ingame a changée, voulez-vous exporter cette liste vers le fichier PlayerLocalData ?", this.Title, MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    ExportPlayerLocalDataFile();
                }
            }
            // Save config
            SaveMainConfig();
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            ToolBar toolBar = sender as ToolBar;
            var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
            if (overflowGrid != null)
            {
                overflowGrid.Visibility = Visibility.Collapsed;
            }
            var mainPanelBorder = toolBar.Template.FindName("MainPanelBorder", toolBar) as FrameworkElement;
            if (mainPanelBorder != null)
            {
                mainPanelBorder.Margin = new Thickness();
            }
        }

        private void LoadIngameMapPoi(List<JsonIngamePoi> ingameMapPoiList)
        {
            mapViewer.LockSave++;
            ingameMarkerListChanged_ = 0;
            ingameMarkerList_.Clear();
            var poi_dict = new Dictionary<string, MapPoiDef>();
            foreach (var item in ingameMapPoiList)
            {
                var ing_marker = new IngameMarker(item, null);
                var poi = new MapPoiDef("ingame-map-poi", ing_marker);
                poi.Layer = "ingame-map-poi";
                poi_dict[poi.Id] = poi;
                ing_marker.Id = poi.Id;
                ingameMarkerList_.Add(ing_marker);
            }
            mapViewer.AddPoi(poi_dict);
            mapViewer.LockSave--;
        }

        private List<JsonIngamePoi> SaveIngameMapPoi()
        {
            var list = new List<JsonIngamePoi>();
            foreach (var item in ingameMarkerList_)
            {
                var poi = new JsonIngamePoi();
                poi.name = item.Name;
                poi.color = item.Color;
                poi.lat = item.floatLat;
                poi.lon = item.floatLon;
                list.Add(poi);
            }
            return list;
        }

        private void LoadIngameMapFow(List<int> fow)
        {
            if (fow.Count > 0)
            {
                // Check if all one => no fow !
                bool all_1 = fow.All(x => x == 1);
                if (!all_1)
                {
                    mapViewer.LockSave++;
                    mapViewer.LoadFogOfWars(fow);
                    mapViewer.LockSave--;
                }
            }
        }

        private void LoadUserMapPoi(List<JsonIngamePoi> userMapPoiList)
        {
            mapViewer.LockSave++;
            userMarkerList_.Clear();
            var poi_dict = new Dictionary<string, MapPoiDef>();
            foreach (var item in userMapPoiList)
            {
                var ing_marker = new IngameMarker(item, null);
                var poi = new MapPoiDef("user-map-poi", ing_marker);
                poi.Layer = "user-map-poi";
                poi_dict[poi.Id] = poi;
                ing_marker.Id = poi.Id;
                userMarkerList_.Add(ing_marker);
            }
            mapViewer.AddPoi(poi_dict);
            mapViewer.LockSave--;
        }

        private List<JsonIngamePoi> SaveUserMapPoi()
        {
            var list = new List<JsonIngamePoi>();
            foreach (var item in userMarkerList_)
            {
                var poi = new JsonIngamePoi();
                poi.name = item.Name;
                poi.color = item.Color;
                poi.lat = item.floatLat;
                poi.lon = item.floatLon;
                poi.shape = item.Shape;
                list.Add(poi);
            }
            return list;
        }

        private void ImportPlayerLocalDataFile(string filename)
        {
            if (File.Exists(filename) == false)
            {
                MessageBox.Show($"Le fichier '{filename}' n'existe pas !");
                return;
            }
            try
            {
                var arkFile_ = new ArkFile();
                arkFile_.Load(filename);
                if (arkFile_.FileType == "PrimalLocalProfile")
                {
                    if (cfg_.map_def == null)
                    {
                        cfg_.map_def = new Dictionary<string, JsonMapDef>();
                    }
                    var mapMarkersDict = arkFile_.ReadMapMarkers();
                    if (mapMarkersDict != null)
                    {
                        foreach (var map in mapDefDict_.Values)
                        {
                            if (mapMarkersDict.TryGetValue(map._name, out var mapMarkers))
                            {
                                var cfg_user_map_poi = new List<JsonIngamePoi>();
                                foreach (var marker in mapMarkers)
                                {
                                    var json_poi = new JsonIngamePoi()
                                    {
                                        name = marker.Name,
                                        color = $"#{marker.Color:x}",
                                        lat = marker.Lat,
                                        lon = marker.Lon
                                    };
                                    cfg_user_map_poi.Add(json_poi);
                                }
                                var json_map_def = cfg_.map_def.FirstOrDefault(x => map._name == x.Key).Value;
                                if (json_map_def == null)
                                {
                                    json_map_def = new JsonMapDef();
                                    cfg_.map_def[map._name] = json_map_def;
                                }
                                json_map_def.ingame_map_poi = cfg_user_map_poi;
                            }
                        }
                    }
                    var fogOfWars = arkFile_.ReadFogOfWars();
                    if (fogOfWars != null)
                    {
                        foreach (var map in mapDefDict_.Values)
                        {
                            if (fogOfWars.TryGetValue(map._name, out var fow))
                            {
                                var json_map_def = cfg_.map_def.FirstOrDefault(x => map._name == x.Key).Value;
                                if (json_map_def == null)
                                {
                                    json_map_def = new JsonMapDef();
                                    cfg_.map_def[map._name] = json_map_def;
                                }
                                json_map_def.fog_of_wars = fow;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) 
            { 
                MessageBox.Show(ex.Message);
            }
        }

        private void ExportPlayerLocalDataFile()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                InitialDirectory = lastArkImportFolder_,
                Filter = "ARK Player Local Data file|PlayerLocalData.arkprofile",
                Title = "Sélectionnez le fichier 'PlayerLocalData.arkprofile' vers lequel exporter"
            };
            if (String.IsNullOrEmpty(lastArkImportFolder_))
            {
                if (Directory.Exists(cfg_.ark_save_folder))
                {
                    openFileDialog.InitialDirectory = cfg_.ark_save_folder + @"\LocalProfiles";
                }
                else
                {
                    openFileDialog.InitialDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\ARK\ShooterGame\Saved\LocalProfiles";
                }
            }
            if (openFileDialog.ShowDialog() == true)
            {
                ExportPlayerLocalDataFile(openFileDialog.FileName);
            }
        }
        
        private void ExportPlayerLocalDataFile(string filename)
        {
            try
            {
                string backup = filename + ".bak";
                File.Copy(filename, backup, true);
                var arkFile_ = new ArkFile();
                arkFile_.Load(filename);
                var mapMarkersDict = new Dictionary<string, List<MapMarker>>();
                foreach (var map_def in cfg_.map_def)
                {
                    var poi_list = new List<MapMarker>();
                    foreach (var poi in map_def.Value.ingame_map_poi)
                    {
                        var marker = new MapMarker();
                        marker.Name = poi.name;
                        string hexcolor = poi.color.Substring(1); // Remove '#' html prefixed
                        marker.Color = uint.Parse(hexcolor, NumberStyles.HexNumber);
                        marker.Lat = poi.lat;
                        marker.Lon = poi.lon;
                        poi_list.Add(marker);
                    }
                    mapMarkersDict[map_def.Key] = poi_list;
                }
                arkFile_.WriteMapMarkers(mapMarkersDict);
                arkFile_.Save(filename);
                ingameMarkerListChanged_ = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private bool CheckArkSaveFolder()
        {
            if (String.IsNullOrEmpty(cfg_.ark_save_folder))
            {
                MessageBox.Show($"L'import automatique est actif mais le dossier de sauvegarde d'ARK n'est pas défini !");
                return false;
            }
            else if (Directory.Exists(cfg_.ark_save_folder) == false)
            {
                MessageBox.Show($"Le dossier '{cfg_.ark_save_folder}' n'existe pas !");
                return false;
            }
            return true;
        }

        private void MenuItem_ImportMarkers_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                InitialDirectory = lastArkImportFolder_,
                Filter = "ARK Player Local Data file|PlayerLocalData.arkprofile",
                Title = "Sélectionnez le fichier 'PlayerLocalData.arkprofile' à importer"
            };
            if (String.IsNullOrEmpty(lastArkImportFolder_))
            {
                if (Directory.Exists(cfg_.ark_save_folder))
                {
                    openFileDialog.InitialDirectory = cfg_.ark_save_folder + @"\LocalProfiles";
                }
                else
                {
                    openFileDialog.InitialDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\ARK\ShooterGame\Saved\LocalProfiles";
                }
            }
            if (openFileDialog.ShowDialog() == true)
            {
                lastArkImportFolder_ = Path.GetDirectoryName(openFileDialog.FileName);
                ImportPlayerLocalDataFile(openFileDialog.FileName);
                var json_map_def = cfg_.map_def.FirstOrDefault(x => cfg_.map == x.Key).Value;
                if (json_map_def != null && json_map_def.ingame_map_poi != null)
                {
                    LoadIngameMapPoi(json_map_def.ingame_map_poi);
                }
                else
                {
                    ingameMarkerList_.Clear();
                }
            }
        }

        private void MenuItem_ExportMarkers_Click(object sender, RoutedEventArgs e)
        {
            ExportPlayerLocalDataFile();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if ((e.SystemKey == System.Windows.Input.Key.LeftAlt) && (e.IsRepeat == false))
            {
                if (mainMenu.Visibility == Visibility.Collapsed)
                {
                    mainMenu.Visibility = Visibility.Visible;
                }
                else if (mainMenu.Visibility == Visibility.Visible)
                {
                    mainMenu.Visibility = Visibility.Collapsed;
                }
            }
        }

        private string getSplitterPos()
        {
            var converter = new GridLengthConverter();
            return $"{converter.ConvertToString(LeftColumn.Width)};{converter.ConvertToString(RightColumn.Width)}";
        }

        private void setSplitterPos(string splitterPos)
        {
            var split = splitterPos.Split(';');
            var converter = new GridLengthConverter();
            LeftColumn.Width = (GridLength)converter.ConvertFromString(split[0]);
            RightColumn.Width = (GridLength)converter.ConvertFromString(split[1]);
        }

        private void LoadMainConfig()
        {
            try
            {
                // Read config
                string cfg_filename = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".json");
                string cfg_lines = File.ReadAllText(cfg_filename);
                cfg_ = JsonSerializer.Deserialize<MainConfig>(cfg_lines);
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = cfg_.window.left;
                Top = cfg_.window.top;
                Width = cfg_.window.width;
                Height = cfg_.window.height;
                if (cfg_.splitter_pos != null)
                {
                    setSplitterPos(cfg_.splitter_pos);
                }
            }
            catch (FileNotFoundException)
            { }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading config ! ({0})", ex.Message);
            }
        }

        private void SaveMainConfig()
        {
            string cfg_filename = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".json");
            // Update config
            cfg_.window.left = Convert.ToInt32(Left);
            cfg_.window.top = Convert.ToInt32(Top);
            cfg_.window.width = Convert.ToInt32(Width);
            cfg_.window.height = Convert.ToInt32(Height);
            cfg_.splitter_pos = getSplitterPos();
            // Map def
            SaveMapDef(null);
            // Write config
            string json = JsonSerializer.Serialize(cfg_, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(cfg_filename, json);
        }

        private void LoadMapDef(MapDef mapDef, bool subMapLoad=false)
        {
            if (!subMapLoad)
            {
                if (mapDef.maps.Count > 1)
                {
                    lockInterface_++;
                    comboBoxSubMap.Items.Clear();
                    foreach (var map in mapDef.maps)
                    {
                        comboBoxSubMap.Items.Add(map.name);
                    }
                    comboBoxSubMap.SelectedItem = mapDef.currentMap.name;
                    comboBoxSubMap.Visibility = Visibility.Visible;
                    lockInterface_--;
                }
                else
                {
                    comboBoxSubMap.Visibility = Visibility.Collapsed;
                    comboBoxSubMap.Items.Clear();
                }
            }
            mapViewer.LockSave++;
            mapViewer.ClearPoi();
            mapViewer.ClearFogOfWars();
            cfg_.map = mapDef.Name;
            try
            {
                var currentMapDef = mapDef.currentMap;
                mapViewer.MapBorderWidth = currentMapDef.border.width;
                mapViewer.MapBorderColor = currentMapDef.border.color;
                var markerDict = new Dictionary<string, MapPoiDef>();
                var contentList = new List<string>();
                var collectibleList = new List<CollectibleTreeViewItem>();
                // La liste de poi n'est extraite que de la ressource de carte d'exploration
                var poiList = new List<MapPoiDef>();
                mapViewer.EnabledLayer("layers-resources", false);
                mapViewer.EnabledLayer("layers-exploration", false);
                if (currentMapDef.resource != null)
                {
                    ArkWiki.LoadWikiGGJsonRessourceName(currentMapDef, $"ARKInteractiveMap.Ressources.{mapDef.folder}.{currentMapDef.resource}", 
                        markerDict, contentList, collectibleList, null, expNoteList_, "layers-resources");
                    mapViewer.EnabledLayer("layers-resources", true);
                }
                if (currentMapDef.exploration != null)
                {
                    ArkWiki.LoadWikiGGJsonRessourceName(currentMapDef, $"ARKInteractiveMap.Ressources.{mapDef.folder}.{currentMapDef.exploration}", 
                        markerDict, contentList, collectibleList, poiList, expNoteList_, "layers-exploration");
                    mapViewer.EnabledLayer("layers-exploration", true);
                }
                if (markerDict != null)
                {
                    collectibleList_.Clear();
                    foreach (var item in collectibleList)
                    {
                        item.FinalizeInit(mapViewer);
                        collectibleList_.Add(item);
                    }

                    // Build poi groups
                    var poi_groups = new Dictionary<string, List<MapPoiDef>>();
                    foreach (var poi in poiList)
                    {
                        var groupName = poi.groupName;
                        if (poi.groupName.Contains("obelisk-"))
                        {
                            groupName = "obelisk";
                        }
                        if (poi_groups.ContainsKey(groupName) == false)
                        {
                            poi_groups[groupName] = new List<MapPoiDef>();
                        }
                        poi_groups[groupName].Add(poi);
                    }

                    var app_res_list = Assembly.GetExecutingAssembly().GetManifestResourceNames();

                    mapViewer.MapSize = currentMapDef.mapSize;
                    mapViewer.MapImage = $"ARKInteractiveMap.Ressources.{mapDef.folder}.{currentMapDef.mapPicture}";

                    mapViewer.LoadPoi(markerDict);
                    mapViewer.LoadContentList(contentList);

                    if (cfg_.map_def == null)
                    {
                        cfg_.map_def = new Dictionary<string, JsonMapDef>();
                    }

                    var json_map_def = cfg_.map_def.FirstOrDefault(x => cfg_.map == x.Key).Value;
                    if (json_map_def == null)
                    {
                        json_map_def = new JsonMapDef();
                    }

                    // Map items visibility
                    if (json_map_def.map_poi_visible != null)
                    {
                        mapViewer.LoadLayersVisibility(json_map_def.map_poi_visible);
                    }

                    // Layers visibility
                    if (json_map_def.layers_visible != null)
                    {
                        mapViewer.LoadLayersVisibility(json_map_def.layers_visible);
                    }

                    // Poi list [group + item]
                    poiList_.Clear();
                    // Build poi treeview
                    foreach (var group in poi_groups)
                    {
                        var parent_node = new PoiTreeViewItem(group.Value[0], mapViewer);
                        if (parent_node.Label.Contains("Obélisque "))
                        {
                            parent_node.Label = "Obélisque";
                        }
                        poiList_.Add(parent_node);
                        foreach (var item in group.Value)
                        {
                            parent_node.Childrens.Add(new PoiTreeViewItem(item, mapViewer, parent_node));
                        }
                    }

                    // Map items collected list
                    mapViewer.UpdateCollected(json_map_def.map_poi_collected);
                    UpdateCollected(json_map_def.map_poi_collected);

                    // Use last imported poi
                    if (json_map_def.ingame_map_poi != null)
                    {
                        LoadIngameMapPoi(json_map_def.ingame_map_poi);
                    }
                    else
                    {
                        ingameMarkerList_.Clear();
                    }

                    // Use last imported fog of wars
                    if (json_map_def.fog_of_wars != null)
                    {
                        LoadIngameMapFow(json_map_def.fog_of_wars);
                    }

                    if (json_map_def.user_map_poi != null)
                    {
                        LoadUserMapPoi(json_map_def.user_map_poi);
                    }
                    else
                    {
                        userMarkerList_.Clear();
                    }

                    mapViewer.Scale = json_map_def.scale;
                    mapViewer.Origin = new Point(json_map_def.origin.x, json_map_def.origin.y);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
            mapViewer.LockSave--;
        }

        private void SaveMapDef(MapDef mapDef)
        {
            if (mapDef == null)
            {
                mapDef = mapDefDict_.FirstOrDefault(x => x.Value.IsMainMap(cfg_.map)).Value;
                //mapDef = mapDefDict_[cfg_.map];
            }

            cfg_.map = mapDef.Name;

            var json_map_def = cfg_.map_def.FirstOrDefault(x => cfg_.map == x.Key).Value;
            if (json_map_def == null)
            {
                json_map_def = new JsonMapDef();
                cfg_.map_def[cfg_.map] = json_map_def;
            }

            json_map_def.origin.x = Convert.ToInt32(mapViewer.Origin.X);
            json_map_def.origin.y = Convert.ToInt32(mapViewer.Origin.Y);
            json_map_def.scale = mapViewer.Scale;

            json_map_def.layers_visible = SaveLayerList();
            json_map_def.map_poi_visible = SaveVisibleList();
            json_map_def.map_poi_collected = SaveCollected();
            json_map_def.ingame_map_poi = SaveIngameMapPoi();
            json_map_def.user_map_poi = SaveUserMapPoi();
        }

        private void UpdateCollected(Dictionary<string, List<string>> dict)
        {
            if (dict != null)
            {
                foreach (var group in dict)
                {
                    var item = collectibleList_.FirstOrDefault(x => x.Name == group.Key);
                    if (item != null)
                    {
                        item.UpdateCollected(group.Value);
                    }
                }
            }
        }

        public Dictionary<string, List<string>> SaveCollected()
        {
            var dict = new Dictionary<string, List<string>>();
            foreach (var poi in collectibleList_)
            {
                var item_list = new List<string>();
                if (poi.IsExpanded)
                {
                    item_list.Add($"{poi.Name}#Expanded");
                }
                foreach (var item in poi.Childrens)
                {
                    if (item.IsCollected)
                    {
                        item_list.Add(item.Id);
                    }
                }
                dict[poi.Name] = item_list;
            }
            return dict;
        }

        // json_map_def.layers_visible
        public Dictionary<string, bool> SaveLayerList()
        {
            return mapViewer.GetLayersVisibility();
        }

        // json_map_def.map_poi_visible
        public Dictionary<string, bool> SaveVisibleList()
        {
            return mapViewer.GetContentDict();
        }

        private void comboBoxMap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lockInterface_ != 0) return;
            lockInterface_++;
            var map_item = e.RemovedItems[0] as MapDef;
            if (map_item != null)
            {
                SaveMapDef(map_item);
            }
            map_item = e.AddedItems[0] as MapDef;
            if (map_item != null)
            {
                LoadMapDef(map_item);
            }
            lockInterface_--;
        }

        private void comboBoxSubMap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lockInterface_ != 0) return;
            lockInterface_++;
            var mapDef = mapDefDict_.FirstOrDefault(x => x.Value.IsMainMap(cfg_.map)).Value;
            var map_item = e.RemovedItems[0] as string;
            if (map_item != null)
            {
                SaveMapDef(mapDef);
            }
            map_item = e.AddedItems[0] as string;
            if (map_item != null)
            {
                mapDef.SelectSubMap(map_item);
                LoadMapDef(mapDef, true);
            }
            lockInterface_--;
        }

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // Process clic on collectible item text
        private void Collectible_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1 && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var textBlock = sender as TextBlock;
                if (textBlock != null)
                {
                    var item = textBlock.DataContext as CollectibleTreeViewItem;
                    if (item != null && item.Id != null)
                    {
                        mapViewer.ZoomTo(item.Id);
                    }
                }
            }
        }

        // Process clic on poi item text
        private void PointOfInterest_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1 && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var textBlock = sender as TextBlock;
                if (textBlock != null)
                {
                    var item = textBlock.DataContext as PoiTreeViewItem;
                    if (item != null && item.Id != null)
                    {
                        mapViewer.ZoomTo(item.Id);
                    }
                }
            }
        }

        private void ButtonGotTo_Click(object sender, RoutedEventArgs e)
        {
            float lat = Convert.ToSingle(textboxLat.Text, CultureInfo.InvariantCulture.NumberFormat);
            float lon = Convert.ToSingle(textboxLon.Text, CultureInfo.InvariantCulture.NumberFormat);
            mapViewer.ZoomToMapPos(lat, lon);
        }

        private void textbox_GotFocus(object sender, RoutedEventArgs e)
        {
            (sender as TextBox).SelectAll();
        }

        protected void EditIngameMarker(bool ingameMarker, IngameMarker item =null, MapPos param=null)
        {
            bool add_item = false;
            if (item == null) // new poi
            {
                add_item = true;
                item = new IngameMarker();
            }
            if (item != null && param != null)
            {
                item.floatLat = param.lat;
                item.floatLon = param.lon;
            }
            var dialog = new EditIngameMarker(item)
            {
                Owner = this
            };
            if (ingameMarker)
            {
                dialog.Title = add_item ? "Ajout d'un repère Ingame" : "Edition du répère Ingame";
            }
            else
            {
                dialog.UserMarker = true;
                dialog.Title = add_item ? "Ajout d'un repère libre" : "Edition du répère libre";
                dialog.AddMapIcon(MapPoiShape.Triangle, 24);
                dialog.AddMapIcon(MapPoiShape.Letter, 24, "?");
                dialog.AddMapIcon(MapPoiShape.Letter, 24, "!");
            }
            if (dialog.ShowDialog() == true)
            {
                if (item.Id == null)
                {
                    var poi = new MapPoiDef(ingameMarker ? "ingame-map-poi" : "user-map-poi", item);
                    item.Id = poi.Id;
                    mapViewer.AddPoi(poi.Id, poi);
                    if (ingameMarker)
                        ingameMarkerList_.Add(item);
                    else
                        userMarkerList_.Add(item);
                }
                var map_poi = mapViewer[item.Id];
                if (map_poi != null)
                {
                    if (map_poi.Shape != dialog.Shape)
                    {
                        // Shape ou param ?
                        item.Shape = dialog.Shape;
                        if (MapPoiDef.GetShapeName(map_poi.Shape) != MapPoiDef.GetShapeName(dialog.Shape))
                        {
                            // new shape
                            mapViewer.RemovePoi(item.Id);
                            var poi = new MapPoiDef(ingameMarker ? "ingame-map-poi" : "user-map-poi", item);
                            item.Id = poi.Id;
                            mapViewer.AddPoi(poi.Id, poi);
                        }
                        else
                        {
                            // param
                            map_poi.Shape = dialog.Shape;
                        }
                    }
                    map_poi.Label = item.Name;
                    map_poi.MapPos = item.MapPos;
                    map_poi.FillColor = item.Color;
                    map_poi.Update();
                    SaveMapDef(null);
                    if (ingameMarker)
                        ingameMarkerListChanged_++;
                }
            }
        }

        private void listviewIngameMarkers_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = listviewIngameMarkers.SelectedItem as IngameMarker;
            if (item != null)
            {
                mapViewer.ZoomTo(item.Id);
            }
        }

        private void ListviewIngameMarkersMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuCmd = (sender as MenuItem).Tag as string;
            if (listviewIngameMarkers.SelectedItem != null)
            {
                if (listviewIngameMarkers.SelectedItems.Count > 1)
                {
                    List<string> ids = new List<string>();
                    foreach (var item in listviewIngameMarkers.SelectedItems)
                    {
                        ids.Add((item as IngameMarker).Id);
                    }
                    Command(menuCmd, ids);
                }
                else
                {
                    var item = listviewIngameMarkers.SelectedItem as IngameMarker;
                    Command(menuCmd, item.Id);
                }
            }
            else
            {
                Command(menuCmd);
            }
        }

        private void listviewIngameMarkers_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (listviewIngameMarkers.SelectedItem == null)
            {
                listviewIngameMarkersEdit.IsEnabled = false;
                listviewIngameMarkersDel.IsEnabled = false;
                listviewIngameMarkersAdd.IsEnabled = true;
            }
            else if (listviewIngameMarkers.SelectedItems.Count > 1)
            {
                listviewIngameMarkersEdit.IsEnabled = false;
                listviewIngameMarkersDel.IsEnabled = true;
                listviewIngameMarkersAdd.IsEnabled = false;
            }
            else // 1 selected
            {
                listviewIngameMarkersEdit.IsEnabled = true;
                listviewIngameMarkersDel.IsEnabled = true;
                listviewIngameMarkersAdd.IsEnabled = true;
            }
        }

        private void listviewUserMarkers_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = listviewUserMarkers.SelectedItem as IngameMarker;
            if (item != null)
            {
                mapViewer.ZoomTo(item.Id);
            }
        }

        private void ListviewUserMarkersMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuCmd = (sender as MenuItem).Tag as string;
            if (listviewUserMarkers.SelectedItem != null)
            {
                if (listviewUserMarkers.SelectedItems.Count > 1)
                {
                    List<string> ids = new List<string>();
                    foreach (var item in listviewUserMarkers.SelectedItems)
                    {
                        ids.Add((item as IngameMarker).Id);
                    }
                    Command(menuCmd, ids);
                }
                else
                {
                    var item = listviewUserMarkers.SelectedItem as IngameMarker;
                    Command(menuCmd, item.Id);
                }
            }
            else
            {
                Command(menuCmd);
            }
        }

        private void listviewUserMarkers_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (listviewUserMarkers.SelectedItem == null)
            {
                listviewUserMarkersEdit.IsEnabled = false;
                listviewUserMarkersDel.IsEnabled = false;
                listviewUserMarkersAdd.IsEnabled = true;
            }
            else if (listviewUserMarkers.SelectedItems.Count > 1)
            {
                listviewUserMarkersEdit.IsEnabled = false;
                listviewUserMarkersDel.IsEnabled = true;
                listviewUserMarkersAdd.IsEnabled = false;
            }
            else // 1 selected
            {
                listviewUserMarkersEdit.IsEnabled = true;
                listviewUserMarkersDel.IsEnabled = true;
                listviewUserMarkersAdd.IsEnabled = true;
            }
        }

        private void ButtonConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ConfigDialog()
            {
                Owner = this,
                AutoImportLocalData = cfg_.auto_import_local_data,
                ArkSaveFolder = cfg_.ark_save_folder
            };
            if (dialog.ShowDialog() == true)
            {
                if (cfg_.auto_import_local_data != dialog.AutoImportLocalData && dialog.AutoImportLocalData == true)
                {
                    ImportPlayerLocalDataFile(dialog.ArkSaveFolder + @"\LocalProfiles\PlayerLocalData.arkprofile");
                }
                cfg_.auto_import_local_data = dialog.AutoImportLocalData;
                cfg_.ark_save_folder = dialog.ArkSaveFolder;
                SaveMainConfig();
            }
        }

        private Dictionary<string, string> LoadEploratorNotesIconList()
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ARKInteractiveMap.Ressources.Eplorator-notes-icon.json");
            using StreamReader reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
        }

        private void textboxLon_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                ButtonGotTo_Click(sender, null);
            }
        }
    }
}