using System;
using System.ComponentModel;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ARKInteractiveMap
{
    /// <summary>
    /// Logique d'interaction pour ConfigDialog.xaml
    /// </summary>
    public partial class ConfigDialog : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
        protected bool autoImportLocalData_;
        protected bool realtimeAutoImportLocalData_;
        protected string arkSaveFolder_;

        public bool AutoImportLocalData
        {
            get => autoImportLocalData_;
            set
            {
                if (value != autoImportLocalData_)
                {
                    autoImportLocalData_ = value;
                    NotifyPropertyChanged("AutoImportLocalData");
                }
            }
        }

        public bool RealtimeAutoImportLocalData
        {
            get => realtimeAutoImportLocalData_;
            set
            {
                if (value != realtimeAutoImportLocalData_)
                {
                    realtimeAutoImportLocalData_ = value;
                    NotifyPropertyChanged("RealtimeAutoImportLocalData");
                }
            }
        }

        public string ArkSaveFolder
        {
            get => arkSaveFolder_;
            set
            {
                if (value != arkSaveFolder_)
                {
                    arkSaveFolder_ = value;
                    NotifyPropertyChanged("ArkSaveFolder");
                }
            }
        }

        public ConfigDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ButtonFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog();
            dlg.Title = "My Title";
            dlg.IsFolderPicker = true;
            dlg.InitialDirectory = arkSaveFolder_;
            if (String.IsNullOrEmpty(arkSaveFolder_))
            {
                dlg.InitialDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\ARK\ShooterGame\Saved";
            }
            dlg.AddToMostRecentlyUsedList = false;
            dlg.AllowNonFileSystemItems = false;
            dlg.DefaultDirectory = arkSaveFolder_;
            dlg.EnsureFileExists = true;
            dlg.EnsurePathExists = true;
            dlg.EnsureReadOnly = false;
            dlg.EnsureValidNames = true;
            dlg.Multiselect = false;
            dlg.ShowPlacesList = true;
            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                ArkSaveFolder = dlg.FileName;
            }
        }
    }
}
