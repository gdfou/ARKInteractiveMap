using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;
using System.Reflection.Emit;

namespace ARKInteractiveMap
{
    public class MapPopup
    {
        StackPanel popup_;
        Border popupBorder_;
        StackPanel popupBorderStack_;
        MapPoi ownerPoi_;
        bool controlPopupVisiblity_;

        public MapPopup(StackPanel popup)
        {
            popup_ = popup;
            BuildPopup();
        }

        virtual public void BuildPopup()
        {
            // 'popup' est un StackPanel Vertical, Hidden par défaut
            //  _______________
            // /               \
            // |               |
            // \______  _______/
            //        \/
            // La zone éditable est le StackPanel du border (popupBorderStack_).
#if false
            <Border Background="LightGray" Width="290" Height="70" CornerRadius="10" >
                <Border.Effect>
                    <DropShadowEffect ShadowDepth="6"/>
                </Border.Effect>
                <StackPanel Orientation="Vertical" VerticalAlignment="Center" Margin="20,0,0,0">
                    <TextBlock x:Name="popupLabel" FontSize="16" FontFamily="Verdana" FontWeight="Black" FontStretch="Medium"/>
                    <TextBlock x:Name="popupInfo" FontSize="14" FontFamily="Verdana" Foreground="#757575"/>
                </StackPanel>
            </Border>
            <Polygon Points="130,-1 145,20 160,-1" Fill="LightGray">
                <Polygon.Effect>
                    <DropShadowEffect ShadowDepth="6"/>
                </Polygon.Effect>
            </Polygon>
#endif
            popup_.Children.Clear();
            // Dessine une bordure avec une ombre contenant 2 lignes de texte
            popupBorder_ = new Border()
            {
                Background = Brushes.LightGray,
                Width = 290,
                Height = 70,
                CornerRadius = new CornerRadius(10),
                Effect = new DropShadowEffect() { ShadowDepth = 6 }
            };
            popupBorderStack_ = new StackPanel()
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 0, 0)
            };
            popupBorder_.Child = popupBorderStack_;
            popup_.Children.Add(popupBorder_);
            // Ajoute un triangle au milieu et en dessous du border
            var polyline = new Polyline()
            {
                Points = PointCollection.Parse("130,-1 145,20 160,-1"), // calculer pour {290 x 70}
                Fill = new SolidColorBrush(Colors.LightGray)
            };
            popup_.Children.Add(polyline);
        }

        virtual public void BuildEditableZone(string label, string info)
        {
            popupBorderStack_.Children.Clear();
            var popupLabel = new TextBlock()
            {
                FontSize = 16,
                FontFamily = new FontFamily("Verdana"),
                FontWeight = FontWeights.Black,
                FontStretch = FontStretches.Medium,
                Text = label
            };
            popupBorderStack_.Children.Add(popupLabel);
            var popupInfo = new TextBlock()
            {
                FontSize = 14,
                FontFamily = new FontFamily("Verdana"),
                Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#757575"),
                Text = info
            };
            popupBorderStack_.Children.Add(popupInfo);
        }

        virtual public void ViewPopup(MapPoi poi, double x, double y, string label, string info)
        {
            ownerPoi_ = poi;
            BuildEditableZone(label, info);
            RescalePopup(x, y);
            popup_.Visibility = Visibility.Visible;
            controlPopupVisiblity_ = true;
        }
        virtual public void ViewPopup(MapPoi poi, double x, double y, List<FrameworkElement> list)
        {
            ownerPoi_ = poi;
            popupBorderStack_.Children.Clear();
            foreach (var item in list)
            {
                popupBorderStack_.Children.Add(item);
            }
            RescalePopup(x, y);
            popup_.Visibility = Visibility.Visible;
            controlPopupVisiblity_ = true;
        }

        virtual public void HidePopup(MapPoi poi)
        {
            if (ownerPoi_ == poi)
            {
                popup_.Visibility = Visibility.Hidden;
                controlPopupVisiblity_ = false;
            }
        }

        virtual public void RescalePopup(double x, double y)
        {
            popup_.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(popup_, x - popup_.DesiredSize.Width / 2);
            Canvas.SetTop(popup_, y - popup_.DesiredSize.Height);
        }

        virtual public void ControlPopupVisiblity()
        {
            if (controlPopupVisiblity_ == false)
            {
                popup_.Visibility = Visibility.Hidden;
                ownerPoi_ = null;
            }
            else
            {
                controlPopupVisiblity_ = false;

            }
        }

        virtual public void RescalePopup()
        {
            if (ownerPoi_ != null)
            {
                ownerPoi_.RescalePopup();
            }
        }
    }
}
