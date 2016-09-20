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
        private UserNotificationListener _listener;
        private LanguageDetector _detector;
        private ResourceLoader _l;

        public MainPage()
        {
            this.InitializeComponent();

            _l = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();

            this.Loaded += async (sender, args) => await Init();

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.BackgroundColor = Color.FromArgb(100, 14, 116, 230);
            titleBar.ForegroundColor = Colors.White;
            titleBar.ButtonBackgroundColor = Color.FromArgb(100, 14, 116, 230);
            titleBar.ButtonForegroundColor = Colors.White;
        }

        public async Task Init()
        {
            await ActivarMediaElement(MediaElement);

            await CheckListenerAccess();

            _detector = new LanguageDetector();
            await _detector.AddLanguages("es", "en");
        }

        public void RegisterBackground()
        {
            if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name.Equals("UserNotificationChanged")))
            {
                // Specify the background task
                var builder = new BackgroundTaskBuilder()
                {
                    Name = "UserNotificationChanged"
                };

                // Set the trigger for Listener, listening to Toast Notifications
                builder.SetTrigger(new UserNotificationChangedTrigger(NotificationKinds.Toast));

                // Register the task
                builder.Register();
            }
        }

        public async Task CheckListenerAccess()
        {
            // Get the listener
            _listener = UserNotificationListener.Current;

            // And request access to the user's notifications (must be called from UI thread)
            UserNotificationListenerAccessStatus accessStatus = await _listener.RequestAccessAsync();

            switch (accessStatus)
            {
                // This means the user has granted access.
                case UserNotificationListenerAccessStatus.Allowed:
                    //RegisterBackground();
                    // Yay! Proceed as normal
                    break;

                // This means the user has denied access.
                // Any further calls to RequestAccessAsync will instantly
                // return Denied. The user must go to the Windows settings
                // and manually allow access.
                case UserNotificationListenerAccessStatus.Denied:

                    // Show UI explaining that listener features will not
                    // work until user allows access.
                    break;

                // This means the user closed the prompt without
                // selecting either allow or deny. Further calls to
                // RequestAccessAsync will show the dialog again.
                case UserNotificationListenerAccessStatus.Unspecified:

                    // Show UI that allows the user to bring up the prompt again
                    break;
            }
        }

        public async Task ReadNotifications()
        {
            // Get the toast notifications
            IReadOnlyList<UserNotification> notifs = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
            int cnt = 0;

            foreach (var notif in notifs)
            {
                // Get the app's display name
                string appDisplayName = notif.AppInfo.DisplayInfo.DisplayName;

                // Get the app's logo
                try
                {
                    BitmapImage appLogo = new BitmapImage();
                    RandomAccessStreamReference appLogoStream = notif.AppInfo.DisplayInfo.GetLogo(new Size(16, 16));
                    await appLogo.SetSourceAsync(await appLogoStream.OpenReadAsync());
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"excepcion: app logo: {e}");
                }

                try
                {
                    // Get the toast binding, if present
                    NotificationBinding toastBinding = notif.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);

                    if (toastBinding != null)
                    {
                        // And then get the text elements from the toast binding
                        IReadOnlyList<AdaptiveNotificationText> textElements = toastBinding.GetTextElements();

                        // Treat the first text element as the title text
                        string titleText = textElements.FirstOrDefault()?.Text;

                        // We'll treat all subsequent text elements as body text,
                        // joining them together via newlines.
                        string bodyText = string.Join("\n", textElements.Skip(1).Select(t => t.Text));

                        await Speak(_l.GetString("From") + " " + appDisplayName);
                        await Speak($"{titleText}. {bodyText}");
                        cnt++;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"excepcion: leer notif: {e}");
                }
            }

            if (cnt == 0)
                await Reproducir(_l.GetString("NoNotifications"));
            else
                await Reproducir(_l.GetString("ReadEnd"));

//            _listener.ClearNotifications();
        }

        private async void Read_OnClick(object sender, RoutedEventArgs e)
        {
            await ReadNotifications();
        }

        public class Mensaje
        {
            public string Texto;
            public Action<string> Accion;
            public string[] Comandos;
        }

        public List<Mensaje> Mensajes = new List<Mensaje>();

        private bool _reproduciendo = false;

        public async Task Speak(string texto)
        {
            Debug.WriteLine("Speak: " + texto);
            // The media object for controlling and playing audio.

            if (_reproduciendo == false && _currentMediaElement != null)
            {
                await Reproducir(texto);
            }
            else
            {
                Debug.WriteLine("Speak: encolando");
                Mensajes.Add(new Mensaje() { Texto = texto });
            }
        }


        public async Task Reproducir(string texto)
        {
            Debug.WriteLine("Reproducir: " + texto);

            if (_currentMediaElement == null)
            {
                Debug.WriteLine("Reproducir: mediaElement NULL!");
                return;
            }

            _reproduciendo = true;

            string lang = "en";

            var tlang = _detector.Detect(texto);
            if (!string.IsNullOrEmpty(tlang))
            {
                Debug.WriteLine("lenguaje detectado: " + tlang);
                lang = tlang;
            }

            var v =
                (from VoiceInformation voice in SpeechSynthesizer.AllVoices
                 where
                     // (voice.Language.Equals("en-US") || voice.Language.Equals("en-GB"))
                     voice.Language.StartsWith(lang) == true
                 select voice).First();
            if (v == null)
            {
                v =
                    (from VoiceInformation voice in SpeechSynthesizer.AllVoices
                     select voice).First();
                if (v == null)
                {
                    _reproduciendo = false;
                    await new MessageDialog(string.Format(_l.GetString("MissingLanguage"), tlang)).ShowAsync();
                    return;
                }
            }
            else
            {
                Debug.WriteLine("seleccionando voz " + v.DisplayName);
            }

            Debug.WriteLine("Generando Speech");
            // The object for controlling the speech synthesis engine (voice).
            var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
            synth.Voice = v;
            // Generate the audio stream from plain text.
            //Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
            //    CoreDispatcherPriority.Normal,
            //    async () =>
            //    {
            Debug.WriteLine("Generando Speech: await");
            SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(texto);
            Debug.WriteLine("Generando Speech: post await");

            // Send the stream to the media object.
            _currentMediaElement.SetSource(stream, stream.ContentType);
            _currentMediaElement.Play();
        }

        private MediaElement _currentMediaElement;

        public async Task ActivarMediaElement(MediaElement _mediaElement)
        {
            Debug.WriteLine("ActivarMediaElement");
            _currentMediaElement = _mediaElement;
            _currentMediaElement.MediaEnded -= MediaElementOnMediaEnded;
            _currentMediaElement.MediaEnded += MediaElementOnMediaEnded;
            if (_currentMediaElement.CurrentState == MediaElementState.Playing)
                ;
            else
                await ProcesarCola();
        }

        private async void MediaElementOnMediaEnded(object sender, RoutedEventArgs routedEventArgs)
        {
            Debug.WriteLine("MediaElementOnMediaEnded");
            _reproduciendo = false;
            await ProcesarCola();
        }

        public async Task ProcesarCola()
        {
            Debug.WriteLine("ProcesarCola");
            if (Mensajes.Any())
            {
                var msg = Mensajes.First();
                Debug.WriteLine("ProcesarCola: procesando " + msg.Texto);
                Mensajes.Remove(msg);
                //if (msg.Texto.Equals(RECOG))
                //    await CapturarSpeech(msg.Accion, msg.Comandos);
                //else
                    await Reproducir(msg.Texto);
            }
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            MySplitView.IsPaneOpen = !MySplitView.IsPaneOpen;
        }

        private void irPrincipal(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof (MainPage));
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
    }
}
