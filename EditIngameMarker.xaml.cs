using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ARKInteractiveMap
{
    /// <summary>
    /// Logique d'interaction pour EditIngameMarker.xaml
    /// </summary>
    public partial class EditIngameMarker : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
        protected IngameMarker item_;
        protected IngameMarker edit_;
        protected bool userMarker_;

        public EditIngameMarker(IngameMarker item)
        {
            InitializeComponent();
            var cplist = typeof(Colors).GetProperties();
            cmbColors.ItemsSource = cplist;
            item_ = item;
            edit_ = (IngameMarker)item.Clone();
            DataContext = this;

            // Recherche la couleur la plus proche dans la liste
            var clist = cplist.ToList().ConvertAll(color => (Color)color.GetValue(cplist));
            var color_in_list = FindClosestColor((Color)ColorConverter.ConvertFromString(MarkerColor), clist);
            if (color_in_list != null)
            {
                // remove transparent color
                if (color_in_list == Colors.Transparent)
                {
                    color_in_list = Colors.Red;
                }
                cmbColors.SelectedItem = cplist.FirstOrDefault(cp => (Color)cp.GetValue(cplist) == color_in_list);
            }
        }

        public string MarkerName 
        {
            get => edit_.Name;
            set
            {
                if (value != edit_.Name)
                {
                    edit_.Name = value;
                    NotifyPropertyChanged("MarkerName");
                }
            }
        }

        public string MarkerColor
        {
            get => edit_.Color;
            set
            {
                if (value != edit_.Color)
                {
                    edit_.Color = value;
                    NotifyPropertyChanged("MarkerColor");
                }
            }
        }

        public string Lat
        {
            get => edit_.Lat;
            set
            {
                if (value != edit_.Lat)
                {
                    edit_.Lat = value;
                    NotifyPropertyChanged("Lat");
                }
            }
        }

        public string Lon
        {
            get => edit_.Lon;
            set
            {
                if (value != edit_.Lon)
                {
                    edit_.Lon = value;
                    NotifyPropertyChanged("Lon");
                }
            }
        }

        public bool UserMarker 
        {
            get => userMarker_;
            set 
            {
                cmbStyles.IsEnabled = true;
            }
        }

        public void AddMapIcon(MapPoiShape shape, int size, string param = null)
        {
            cmbStyles.Items.Add(new ResourceItem(shape, size, param));
            cmbStyles.SelectedIndex = 0;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Shape = item_.Shape;
        }

        public string Shape
        {
            get
            {
                if (cmbStyles.SelectedItem != null)
                {
                    var ctrl = cmbStyles.SelectedItem as ResourceItem;
                    return ctrl.Shape.ToString();
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (String.IsNullOrEmpty(item_.Shape))
                {
                    cmbStyles.SelectedIndex = 0;
                }
                else
                {
                    foreach (ResourceItem item in cmbStyles.Items)
                    {
                        if (item.Shape == value)
                        {
                            cmbStyles.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }

        private bool IsFloat(string s)
        {
            /*
                 ^ : start of string
                [ : beginning of character group
                a-z : any lowercase letter
                A-Z : any uppercase letter
                0-9 : any digit
                ' ' : space
                ] : end of character group
                + : 1+ of the given characters
                $ : end of string
            */
            Regex r = new Regex(@"^[0-9.]+$");
            return r.IsMatch(s);
        }

        private void textboxCoord_KeypressValidation(object sender, TextCompositionEventArgs e)
        {
            if (!IsFloat(e.Text))
                e.Handled = true;
        }

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            item_.Name = edit_.Name;
            item_.Color = edit_.Color;
            item_.Lat = edit_.Lat;
            item_.Lon = edit_.Lon;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void cmbColors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = e.AddedItems[0];
            if (item != null)
            {
                var split = item.ToString().Split(' ');
                MarkerColor = ColorConverter.ConvertFromString(split[1]).ToString();
            }
        }

        protected double CalculateColorDistance(Color color1, Color color2)
        {
            int deltaR = color1.R - color2.R;
            int deltaG = color1.G - color2.G;
            int deltaB = color1.B - color2.B;
            return Math.Sqrt(deltaR * deltaR + deltaG * deltaG + deltaB * deltaB);
        }

        protected Color FindClosestColor(Color referenceColor, List<Color> colors)
        {
            double minDistance = double.MaxValue;
            Color closestColor = Colors.Black;
            foreach (Color color in colors)
            {
                double distance = CalculateColorDistance(referenceColor, color);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestColor = color;
                }
            }
            return closestColor;
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            (sender as TextBox).SelectAll();
        }
    }
}
