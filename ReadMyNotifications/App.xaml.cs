using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.VoiceCommands;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Phone.Media.Devices;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.HockeyApp;
using Microsoft.QueryStringDotNET;
using ReadMyNotifications.Utils;
using ReadMyNotifications.ViewModels;

namespace ReadMyNotifications
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        public static MainViewModel ViewModel { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            ViewModel = new MainViewModel();
            Microsoft.HockeyApp.HockeyClient.Current.Configure("1b73ec47435844f3b00c28e67b048c75");
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = false;
            }
#endif
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            await RegistrarComandosVoz();

            await ViewModel.Init();

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                ToastNotificationManager.History.RemoveGroup("RMN");

                // Ensure the current window is active
                Window.Current.Activate();
            }
        }

        public async Task RegistrarComandosVoz()
        {
#if WINDOWS_PHONE_APP || WINDOWS_UWP
            try
            {
                var storageFile =
                    await
                        Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///ComandosVoz.xml"));
                await
                    VoiceCommandDefinitionManager.InstallCommandDefinitionsFromStorageFileAsync(
                        storageFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("excepcion al cargar comandos de voz: " + ex.Message);
            }
#endif
        }


        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            base.OnActivated(args);
            Frame rootFrame = Window.Current.Content as Frame;

            var navigationToPageType = typeof(MainPage);
            string navigationCommand = null;

            if (args.Kind == ActivationKind.VoiceCommand)
            {
                VoiceCommandActivatedEventArgs voiceArgs = (VoiceCommandActivatedEventArgs) args;
                if (voiceArgs.Result.RulePath.ToList().Contains("Read"))
                {
                    navigationCommand = "read";
                    ToastNotificationManager.History.RemoveGroup("RMN");
                }
            }
            if (args is ToastNotificationActivatedEventArgs)
            {
                ToastNotificationActivatedEventArgs toastArgs = (ToastNotificationActivatedEventArgs) args;
                QueryString qa = QueryString.Parse(toastArgs.Argument);
                switch (qa["action"])
                {
                    case "ToastRead":
                        navigationCommand = "read";
                        ToastNotificationManager.History.RemoveGroup("RMN");
                        break;
                }
            }

            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            await ViewModel.Init();

            // Since we're expecting to always show a details page, navigate even if 
            // a content frame is in place (unlike OnLaunched).
            // Navigate to either the main trip list page, or if a valid voice command
            // was provided, to the details page for that trip.
            rootFrame.Navigate(navigationToPageType, navigationCommand);

            // Ensure the current window is active
            Window.Current.Activate();
        }

        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            Debug.WriteLine("OnBackgroundActivated: inicio");
            //base.OnBackgroundActivated(args);

            var deferral = args.TaskInstance.GetDeferral();

#if BACKGROUND_TOAST
            bool launchedFromToast = false;

            var details = args.TaskInstance.TriggerDetails as ToastNotificationActionTriggerDetail;

            if (details != null)
            {
                //string arguments = details.Argument;
                //var userInput = details.UserInput;

                // Perform tasks
                launchedFromToast = true;
            }
#endif

            try
            {
                if (ViewModel.LeerEnBackground == false)
                    return;

                if (MainViewModel.IsPhone)
                {
                    var current = AudioRoutingManager.GetDefault().GetAudioEndpoint();
                    switch (current)
                    {
                        case AudioRoutingEndpoint.Bluetooth:
                            case AudioRoutingEndpoint.BluetoothPreferred:
                            case AudioRoutingEndpoint.BluetoothWithNoiseAndEchoCancellation:
                            if (ViewModel.LeerBluetooth == false)
                            {
                                Debug.WriteLine("ignorando: bluetooth");
                                return;
                            }
                            break;
                            case AudioRoutingEndpoint.Earpiece:
                            case AudioRoutingEndpoint.WiredHeadset:
                            case AudioRoutingEndpoint.WiredHeadsetSpeakerOnly:
                            if (ViewModel.LeerHeadphones == false)
                            {
                                Debug.WriteLine("ignorando: headphones");
                                return;
                            }
                            break;

                            //case AudioRoutingEndpoint.Default:
                            //case AudioRoutingEndpoint.Speakerphone:
                        default:
                            if (ViewModel.LeerSpeaker == false)
                            {
                                Debug.WriteLine("ignorando: speaker");
                                return;
                            }
                            break;
                    }
                }



                try
                {
                    await ViewModel.Init();
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"OnBackgroundActivated: excepcion: {e}");
                    return;
                }

                // no tiene sentido hacer todo el ejercicio si no va a sonar nada
                if ((Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow == null
                || Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess == false)
#if BACKGROUND_TOAST
                && (launchedFromToast == false)
#endif
                )
//                || (DeviceTypeHelper.GetDeviceFormFactorType() != DeviceFormFactorType.Phone && ViewModel.IsSMTCMuted()))
                {
                    // no va a sonar nada
                    var history = ToastNotificationManager.History.GetHistory();
                    var existe = (from n in history where n.Group == "RMN" && n.Tag == "BR" select n).FirstOrDefault();
                    if (existe == null)
                    {
                        Debug.WriteLine("sending toast");
                        ViewModel.SendToast();
                    }
                    return;
                }

                Debug.WriteLine($"Task Name: {args.TaskInstance.Task.Name}");
                switch (args.TaskInstance.Task.Name)
                {
                    case "UserNotificationChanged":
                        await ViewModel.FillNotifications();
                        ViewModel.PrepareForMediaEnded();
                        int cnt = await ViewModel.ReadAllNotifications(false, true);
                        ViewModel.Play();
                        if (cnt > 0)
                            await ViewModel.WaitForMediaEnded();
                        break;

                    case "ToastAction":
                        await ViewModel.FillNotifications();
                        await ViewModel.ReadAllNotifications(false, false);
                        ViewModel.Play();
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"excepcion: {e}");
            }
            finally
            {
                deferral.Complete();
            }
            Debug.WriteLine("OnBackgroundActivated: fin");
        }
    }
}
