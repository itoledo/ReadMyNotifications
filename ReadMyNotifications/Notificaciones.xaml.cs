using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ReadMyNotifications.ViewModels;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace ReadMyNotifications
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Notificaciones : Page
    {
        public Notificaciones()
        {
            this.InitializeComponent();
            DataContext = App.ViewModel;
            //this.Loaded += async (sender, args) =>
            //{
            //    try
            //    {
            //        await App.ViewModel.GetNotifications();
            //    }
            //    catch (Exception e)
            //    {
            //        Debug.WriteLine($"excepcion: {e}");
            //    }
            //};
        }

        private async void Lista_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems == null || e.AddedItems.Count == 0)
                return;

            try
            {
                var notif = e.AddedItems[0] as Notificacion;
                if (notif != null)
                {
                    //App.ViewModel.StopMediaPlayer();
                    await App.ViewModel.ReadNotification(notif);
                }
            }
            catch (Exception ex)
            {
                // esto se cae a veces
                Debug.WriteLine($"excepcion: {ex}");
            }
        }

        private async void Read_OnClick(object sender, RoutedEventArgs e)
        {
            //await App.ViewModel.ReadNotifications(MainViewModel.ReadType.ReadAll);
            await App.ViewModel.ReadAllNotifications();
        }

        private void Stop_OnClick(object sender, RoutedEventArgs e)
        {
            App.ViewModel.StopMediaPlayer();
        }

        private async void Reload_OnClick(object sender, RoutedEventArgs e)
        {
            App.ViewModel.StopMediaPlayer();
            await App.ViewModel.FillNotifications();
        }
    }
}
