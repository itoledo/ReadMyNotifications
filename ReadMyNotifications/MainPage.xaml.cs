using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using ReadMyNotifications.Language;
using Windows.UI.ViewManagement;
using Windows.UI;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ReadMyNotifications
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        public MainPage()
        {
            this.InitializeComponent();

            DataContext = App.ViewModel;

            this.Loaded += async (sender, args) =>
            {
                await App.ViewModel.Init();
                await App.ViewModel.GetNotifications();
            };

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.BackgroundColor = Color.FromArgb(100, 14, 116, 230);
            titleBar.ForegroundColor = Colors.White;
            titleBar.ButtonBackgroundColor = Color.FromArgb(100, 14, 116, 230);
            titleBar.ButtonForegroundColor = Colors.White;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter != null && e.Parameter.Equals("read"))
            {
                await App.ViewModel.GetNotifications();
                await App.ViewModel.ReadNotifications();
            }
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            MySplitView.IsPaneOpen = !MySplitView.IsPaneOpen;
        }

        private void irPrincipal(object sender, TappedRoutedEventArgs e)
        {
            var f = new Frame();
            MySplitView.Content = f;
            f.Navigate(typeof(Notificaciones));
        }

        private void irConfiguracion(object sender, TappedRoutedEventArgs e)
        {
            var f = new Frame();
            MySplitView.Content = f;
            f.Navigate(typeof(Configuracion));
        }

        private void irAcercaDe(object sender, TappedRoutedEventArgs e)
        {
            var f = new Frame();
            MySplitView.Content = f;
            f.Navigate(typeof(AcercaDe));
        }

        private void Read_OnClick(object sender, RoutedEventArgs e)
        {
            
        }
    }
}
