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

namespace ARKInteractiveMap
{
    /// <summary>
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        string lastArkImportFolder_;
        int lockInterface_;
        MainConfig cfg_;
        ObservableCollection<ResourceItem> contentList_;
        ObservableCollection<MapDef> mapDefList_;
        ObservableCollection<CollectibleTreeViewItem> collectibleList_;
        ObservableCollection<IngameMarker> ingameMarkerList_;
        ObservableCollection<IngameMarker> userMarkerList_;
        ObservableCollection<ResourceItem> layerList_;
        Dictionary<string, string> expNoteList_;
        int ingameMarkerListChanged_;

        public string ContentVisible
        {
            get { return "Contenus"; }
            set {}
        }
        public string LayerVisible
        {
            get { return "Calques"; }
            set { }
        }

        public MainWindow()
        {
            DataContext = this;
            contentList_ = new ObservableCollection<ResourceItem>();
            mapDefList_ = new ObservableCollection<MapDef>();
            collectibleList_ = new ObservableCollection<CollectibleTreeViewItem>();
            ingameMarkerList_ = new ObservableCollection<IngameMarker>();
            userMarkerList_ = new ObservableCollection<IngameMarker>();
            layerList_ = new ObservableCollection<ResourceItem>();
            InitializeComponent();

#if !DEBUG
            mainMenu.Visibility = Visibility.Collapsed;
#endif
            LoadMainConfig();
            if (cfg_ == null)
            {
                cfg_ = new MainConfig()
                {
                    window = new JsonRect(),
                    fog_of_wars = true
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
                    mapDefList_.Add(new MapDef(map));
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
            // TODO => ajouter un séparateur => construction manuelle ComboBoxItem/Separator !
            comboBoxMap.ItemsSource = mapDefList_;
            var currentMapDef = mapDefList_.FirstOrDefault(x => x.IsMainMap(cfg_.map));
            if (currentMapDef == null)
            {
                currentMapDef = mapDefList_[0];
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

            layerList_.Add(new ResourceItem(mapViewer, "layers-surface", "Surface"));
            layerList_.Add(new ResourceItem(mapViewer, "layers-cave", "Grottes"));
            layerList_.Add(new ResourceItem(mapViewer, "layers-user", "Répères"));
            LoadMapDef(currentMapDef);

            trvCollectible.ItemsSource = collectibleList_;
            listviewIngameMarkers.ItemsSource = ingameMarkerList_;
            listviewUserMarkers.ItemsSource = userMarkerList_;

            comboBoxContent.ItemsSource = contentList_;
            comboBoxLayer.ItemsSource = layerList_;
            checkboxFow.IsChecked = cfg_.fog_of_wars;
            lockInterface_--;
        }

        private void MapViewer_CommandEvent(object sender, CommandEventArgs e)
        {
            Command(e.Cmd, e.Id, e.Param);
        }

        public void Command(string cmd, string id=null, object param=null)
        {
            switch (cmd)
            {
                case "IngameMarkerEdit":
                    {
                        var item = ingameMarkerList_.FirstOrDefault(x => x.Id == id);
                        if (item != null)
                        {
                            EditIngameMarker(true, item);
                        }
                        break;
                    }

                case "IngameMarkerDel":
                    {
                        var item = ingameMarkerList_.FirstOrDefault(x => x.Id == id);
                        if (item != null)
                        {
                            if (MessageBox.Show($"Voulez-vous vraiment supprimer le repère '{item.Name}' ?", this.Title, MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                            {
                                ingameMarkerList_.Remove(item);
                                mapViewer.RemovePoi(item.Id);
                                SaveMapDef(null);
                                ingameMarkerListChanged_++;
                            }
                        }
                        break;
                    }

                case "IngameMarkerAdd": // + option param => MapPos
                    {
                        EditIngameMarker(true, null, param as MapPos);
                        break;
                    }

                case "UserMarkerEdit":
                    {
                        var item = userMarkerList_.FirstOrDefault(x => x.Id == id);
                        if (item != null)
                        {
                            EditIngameMarker(false, item);
                        }
                        break;
                    }

                case "UserMarkerDel":
                    {
                        var item = userMarkerList_.FirstOrDefault(x => x.Id == id);
                        if (item != null)
                        {
                            if (MessageBox.Show($"Voulez-vous vraiment supprimer le repère '{item.Name}' ?", this.Title, MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                            {
                                userMarkerList_.Remove(item);
                                mapViewer.RemovePoi(item.Id);
                                SaveMapDef(null);
                            }
                        }
                        break;
                    }

                case "UserMarkerAdd": // + option param => MapPos
                    {
                        EditIngameMarker(false, null, param as MapPos);
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
                if (cmd == "IngameMarkerDel")
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
                    checkboxFow.IsEnabled = true;
                    mapViewer.LoadFogOfWars(fow, cfg_.fog_of_wars);
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
                        foreach (var map in mapDefList_)
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
                        foreach (var map in mapDefList_)
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
            checkboxFow.IsEnabled = false;
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
                mapViewer.mapBorderWidth = currentMapDef.border.width;
                mapViewer.mapBorderColor = currentMapDef.border.color;
                var poi_dict = new Dictionary<string, MapPoiDef>();
                var contentList = new List<string>();
                var collectibleList = new List<CollectibleTreeViewItem>();
                foreach (var res in currentMapDef.resources)
                {
                    ArkWiki.LoadWikiGGJsonRessourceName(currentMapDef, $"ARKInteractiveMap.Ressources.{mapDef.folder}.{res}", poi_dict, contentList, collectibleList, expNoteList_);
                }
                if (poi_dict != null)
                {
                    collectibleList_.Clear();
                    foreach (var item in collectibleList)
                    {
                        item.FinalizeInit(mapViewer);
                        collectibleList_.Add(item);
                    }

                    var app_res_list = Assembly.GetExecutingAssembly().GetManifestResourceNames();

                    mapViewer.MapSize = currentMapDef.mapSize;
                    mapViewer.MapImage = $"ARKInteractiveMap.Ressources.{mapDef.folder}.{currentMapDef.mapPicture}";

                    mapViewer.LoadPoi(poi_dict);

                    // Update liste and group map icon
                    contentList_.Clear();
                    foreach (var group_name in contentList)
                    {
                        var poi_def = poi_dict.FirstOrDefault(x => x.Value.groupName == group_name);
                        if (poi_def.Key != null)
                        {
                            var contentItem = new ResourceItem(group_name);
                            var poi = mapViewer[poi_def.Value.Id];
                            contentItem.UpdateIcons(mapViewer, poi, 20);
                            contentList_.Add(contentItem);
                        }
                    }

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
                        var contentList2 = contentList_.ToList();
                        foreach (var item in json_map_def.map_poi_visible)
                        {
                            var elt = contentList2.FindLast(x => item.Key == x.GroupName);
                            if (elt != null)
                            {
                                elt.IsVisible = item.Value;
                            }
                        }
                    }

                    // Layers visibility
                    if (json_map_def.layers_visible != null)
                    {
                        foreach (var item in json_map_def.layers_visible)
                        {
                            var elt = layerList_.FirstOrDefault(x => item.Key == x.GroupName);
                            if (elt != null)
                            {
                                elt.IsVisible = item.Value;
                            }
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
                mapDef = mapDefList_.FirstOrDefault(x => cfg_.map == x.Name);
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

            foreach (var cbitem in contentList_)
            {
                json_map_def.map_poi_visible[cbitem.GroupName] = cbitem.IsVisible;
            }

            foreach (var cbitem in layerList_)
            {
                json_map_def.layers_visible[cbitem.GroupName] = cbitem.IsVisible;
            }

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
            var mapDef = mapDefList_.FirstOrDefault(x => x.IsMainMap(cfg_.map));
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
        private void TextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
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

        private void ButtonContentAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in contentList_)
            {
                item.IsVisible = true;
            }
        }

        private void ButtonContentNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in contentList_)
            {
                item.IsVisible = false;
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
                dialog.Title = add_item ? "Ajout d'un repère libre" : "Edition du répère libre";
                dialog.cmbStyles.Items.Add(new ResourceItem(MapPoiShape.Triangle, 30));
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

        private void checkboxFow_Click(object sender, RoutedEventArgs e)
        {
            cfg_.fog_of_wars = (bool)checkboxFow.IsChecked;
            mapViewer.FogOfWarsVisible(cfg_.fog_of_wars);
            SaveMainConfig();
        }

        private Dictionary<string, string> LoadEploratorNotesIconList()
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ARKInteractiveMap.Ressources.Eplorator-notes-icon.json");
            using StreamReader reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
        }
    }
}