using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using MyToolkit.Mvvm;
using ReadMyNotifications.Language;

namespace ReadMyNotifications.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private UserNotificationListener _listener;
        private LanguageDetector _detector;
        private ResourceLoader _l;
        public ObservableCollection<Notificacion> ListaNotificaciones { get; private set; }

        public MainViewModel()
        {
            ListaNotificaciones = new ObservableCollection<Notificacion>();
        }
        public async Task Init(MediaElement mediaElement)
        {
            _l = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();

            await ActivarMediaElement(mediaElement);

            await CheckListenerAccess();

            _detector = new LanguageDetector();
            await _detector.AddLanguages("es", "en");
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

        public async Task GetNotifications()
        {
            // Get the toast notifications
            IReadOnlyList<UserNotification> notifs = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
            var lista = new List<Notificacion>();

            foreach (var notif in notifs)
            {
                var n = new Notificacion();
                try
                {
                    // Get the app's display name
                    string appDisplayName = notif.AppInfo.DisplayInfo.DisplayName;
                    n.AppName = appDisplayName;

                    // Get the app's logo
                    try
                    {
                        BitmapImage appLogo = new BitmapImage();
                        RandomAccessStreamReference appLogoStream = notif.AppInfo.DisplayInfo.GetLogo(new Size(64, 64));
                        await appLogo.SetSourceAsync(await appLogoStream.OpenReadAsync());
                        n.Logo = appLogo;
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"excepcion: app logo: {e}");
                    }

                    try
                    {
                        // Get the toast binding, if present
                        NotificationBinding toastBinding =
                            notif.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);

                        if (toastBinding != null)
                        {
                            // And then get the text elements from the toast binding
                            IReadOnlyList<AdaptiveNotificationText> textElements = toastBinding.GetTextElements();

                            // Treat the first text element as the title text
                            string titleText = textElements.FirstOrDefault()?.Text;
                            n.Title = titleText;

                            // We'll treat all subsequent text elements as body text,
                            // joining them together via newlines.
                            string bodyText = string.Join("\n", textElements.Skip(1).Select(t => t.Text));
                            n.Text = bodyText;
                            lista.Add(n);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"excepcion: leer notif: {e}");
                    }

                }
                catch (Exception e)
                {
                    Debug.WriteLine($"excepcion: base: {e}");
                }
            }
            lock (ListaNotificaciones)
            {
                ListaNotificaciones.Clear();
                foreach (var n in lista)
                    ListaNotificaciones.Add(n);
            }
        }

        public async Task ReadNotifications()
        {
            int cnt = 0;
            foreach (var n in ListaNotificaciones)
            {
                await Speak(_l.GetString("From") + " " + n.AppName);
                await Speak($"{n.Title}. {n.Text}");
                cnt++;
            }
            if (cnt == 0)
                await Reproducir(_l.GetString("NoNotifications"));
            else
                await Reproducir(_l.GetString("ReadEnd"));
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

    }
}
