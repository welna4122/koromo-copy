﻿/***

   Copyright (C) 2018. dc-koromo. All Rights Reserved.
   
   Author: Koromo Copy Developer

***/

using Koromo_Copy;
using Koromo_Copy.Component.Hitomi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace Koromo_Copy_UX3.Utility
{
    /// <summary>
    /// ZipViewerElements.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ZipViewerElements : UserControl
    {
        string zip_file_name;

        public ZipViewerElements(string zip_file_name)
        {
            InitializeComponent();

            this.zip_file_name = zip_file_name;
            Loaded += ZipViewerElements_Loaded;
        }

        private void ZipViewerElements_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    var zipFile = ZipFile.Open(zip_file_name, ZipArchiveMode.Read);

                    var model = JsonConvert.DeserializeObject<HitomiJsonModel>(new StreamReader(zipFile.GetEntry("Info.json").Open()).ReadToEnd());
                    Application.Current.Dispatcher.BeginInvoke(new Action(
                    delegate
                    {
                        Title.Text = model.Title;
                        Artist.Text = model.Artists[0];
                    }));

                    var zipEntry = !zipFile.Entries[0].Name.EndsWith(".json") ? zipFile.Entries[0] : zipFile.Entries[1];
                    var zipStream = zipEntry.Open();

                    Application.Current.Dispatcher.BeginInvoke(new Action(
                    delegate
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.DecodePixelWidth = 250;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = zipStream;
                        bitmap.EndInit();

                        Image.Source = bitmap;
                    }));
                }
                catch (Exception ex)
                {
                    Monitor.Instance.Push(ex.Message);
                    Monitor.Instance.Push(ex.StackTrace);
                }
            });
        }
        
        private void UserControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Process.Start(zip_file_name);
        }
    }
}
