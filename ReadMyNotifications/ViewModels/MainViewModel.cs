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
using Windows.System;
using Windows.UI.Core;
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
        public ObservableCollection<VoiceInformation> AllVoices { get; private set; }

        private bool _deteccionAutomatica;

        public bool DeteccionAutomatica
        {
            get { return _deteccionAutomatica; }
            set
            {
                _deteccionAutomatica = value;
                SaveSettings();
                RaisePropertyChanged(() => DeteccionAutomatica);
            }
        }

        private VoiceInformation _defaultVoice;

        public VoiceInformation DefaultVoice
        {
            get { return _defaultVoice; }
            set
            {
                _defaultVoice = value;
                SaveSettings();
                RaisePropertyChanged(() => DefaultVoice);
            }
        }

        Windows.Storage.ApplicationDataContainer settings = Windows.Storage.ApplicationData.Current.LocalSettings;

        public MainViewModel()
        {
            AllVoices = new ObservableCollection<VoiceInformation>();
            try
            {
                if (SpeechSynthesizer.AllVoices != null)
                {
                    var voces = from VoiceInformation voice in SpeechSynthesizer.AllVoices select voice;
                    foreach (var v in voces)
                    {
                        Debug.WriteLine($"voz: {v.Language} {v.DisplayName}");
                        AllVoices.Add(v);
                    }
                }
            }
            catch (Exception ex)
            {
                // para prevenir cualquier error de init
                Debug.WriteLine($"excepcion en ctx: {ex}");
            }

            ListaNotificaciones = new ObservableCollection<Notificacion>();
            LoadSettings();
        }

        public void SetDefaultVoice()
        {
            if (SpeechSynthesizer.DefaultVoice == null)
            {
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        await new MessageDialog(_l.GetString("NoVoiceInstalled")).ShowAsync();
                        await Launcher.LaunchUriAsync(new Uri("ms-settings:speech"));
                    });
                return;
            }
            // busquémoslo en la lista de voces.
            var voz =
            (from VoiceInformation voice in AllVoices
                where voice.Id.Equals(SpeechSynthesizer.DefaultVoice.Id)
                select voice).FirstOrDefault();
            if (voz == null)
                _defaultVoice = SpeechSynthesizer.DefaultVoice;
            else
                _defaultVoice = voz;
        }

        public void LoadSettings()
        {
            if (settings.Values.ContainsKey("DefaultVoiceId"))
            {
                string id = settings.Values["DefaultVoiceId"] as string;
                if (!string.IsNullOrEmpty(id))
                {
                    Debug.WriteLine($"ReadSetting: DefaultVoiceId: {id}");
                    var voz =
                        (from VoiceInformation voice in AllVoices where voice.Id.Equals(id) select voice).FirstOrDefault
                            ();
                    if (voz != null)
                        _defaultVoice = voz;
                    else
                        SetDefaultVoice();
                }
            }
            else
                SetDefaultVoice();
                //_defaultVoice =
                //    (from v in AllVoices where v.Id == SpeechSynthesizer.DefaultVoice.Id select v).FirstOrDefault();

            if (settings.Values.ContainsKey("DeteccionAutomatica"))
            {
                _deteccionAutomatica = (bool) settings.Values["DeteccionAutomatica"];
                Debug.WriteLine($"ReadSetting: DeteccionAutomatica: {DeteccionAutomatica}");
            }
            else
                _deteccionAutomatica = true;
        }

        public void SaveSettings()
        {
            settings.Values["DeteccionAutomatica"] = DeteccionAutomatica;
            if (DefaultVoice != null)
                settings.Values["DefaultVoiceId"] = DefaultVoice.Id;
        }

        public async Task Init(MediaElement mediaElement)
        {
            _l = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();

            await ActivarMediaElement(mediaElement);

            switch (await CheckListenerAccess())
            {
                case 0:
                    break;
                case 1:
                    await new MessageDialog(_l.GetString("NeedsPermission")).ShowAsync();
                    break;
                case -1:
                    var dlg = new MessageDialog(_l.GetString("RetryPermission"));
                    dlg.Commands.Add(new UICommand(_l.GetString("Retry")));
                    dlg.Commands.Add(new UICommand(_l.GetString("Cancel")));
                    bool hecho = false;
                    while (hecho == false)
                    {
                        var ret = await dlg.ShowAsync();
                        if (ret.Label.Equals(_l.GetString("Retry")))
                        {
                            var ok = await CheckListenerAccess();
                            if (ok == 1)
                                await new MessageDialog(_l.GetString("NeedsPermission")).ShowAsync();
                        }
                        else
                            hecho = true;
                    }
                    break;
            }

            _detector = new LanguageDetector();
            await _detector.AddLanguages("es", "en", "de", "fr", "it", "ja", "pt", "zh-cn", "zh-tw");
        }

        public async Task<int> CheckListenerAccess()
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
                    return 0;

                // This means the user has denied access.
                // Any further calls to RequestAccessAsync will instantly
                // return Denied. The user must go to the Windows settings
                // and manually allow access.
                case UserNotificationListenerAccessStatus.Denied:

                    // Show UI explaining that listener features will not
                    // work until user allows access.
                    return 1;

                // This means the user closed the prompt without
                // selecting either allow or deny. Further calls to
                // RequestAccessAsync will show the dialog again.
                case UserNotificationListenerAccessStatus.Unspecified:

                    // Show UI that allows the user to bring up the prompt again
                    return -1;
            }

            return -1;
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

        private Boolean _getting = false;

        public Boolean Getting
        {
            get { return _getting; }
            set
            {
                _getting = value;
                RaisePropertyChanged(() => Getting);
            }
        }

        public async Task GetNotifications()
        {
            if (_getting == true)
            {
                Debug.WriteLine("skipping");
                return;
            }

            Getting = true;

            try
            {
                IReadOnlyList<UserNotification> notifs;
                // Get the toast notifications
                try
                {
                    _listener = UserNotificationListener.Current;
                    notifs = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"excepcion: {e}");
                    notifs = null;
                }

                if (notifs == null)
                {
                    await new MessageDialog(_l.GetString("ErrorGet")).ShowAsync();
                    return;
                }

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
                            RandomAccessStreamReference appLogoStream =
                                notif.AppInfo.DisplayInfo.GetLogo(new Size(64, 64));
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

                                // sólo si tiene algún texto
                                if (!string.IsNullOrEmpty(bodyText) || !string.IsNullOrEmpty(titleText))
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
            catch (Exception e)
            {
                // ???
                Debug.WriteLine($"excepción general: {e}");
            }
            finally
            {
                Getting = false;
            }
        }

        public async Task ReadNotifications()
        {
            int cnt = 0;
            foreach (var n in ListaNotificaciones)
            {
                await ReadNotification(n);
                cnt++;
            }
            if (cnt == 0)
                await Speak(_l.GetString("NoNotifications"));
            else
                await Speak(_l.GetString("ReadEnd"));
        }

        public async Task ReadNotification(Notificacion n)
        {
            // _l.GetString("From") + " " + 
            await Speak(n.AppName);
            await Speak($"{n.Title}. {n.Text}");
        }

        public class Mensaje
        {
            public string Texto;
            public Action<string> Accion;
            public string[] Comandos;
        }

        public List<Mensaje> Mensajes = new List<Mensaje>();

        private bool _playing = false;

        public async Task Speak(string texto)
        {
            Debug.WriteLine("Speak: " + texto);
            // The media object for controlling and playing audio.

            if (_playing == false && _currentMediaElement != null)
            {
                await Reproducir(texto);
            }
            else
            {
                Debug.WriteLine("Speak: encolando");
                Mensajes.Add(new Mensaje() { Texto = texto });
            }
        }

        public bool CanPlay
        {
            get { return !_playing; } set { _playing = !value; RaisePropertyChanged(() => CanPlay); }
        }

        public void StopReading()
        {
            Mensajes.Clear();

            if (_currentMediaElement != null)
            {
                _currentMediaElement.Stop();
            }

            CanPlay = true;
        }

        public async Task Reproducir(string texto)
        {
            Debug.WriteLine("Reproducir: " + texto);

            if (_currentMediaElement == null)
            {
                Debug.WriteLine("Reproducir: mediaElement NULL!");
                return;
            }

            CanPlay = false;

            VoiceInformation v = null;

            if (DeteccionAutomatica == true)
            {
                var tlang = _detector.Detect(texto);
                if (!string.IsNullOrEmpty(tlang))
                {
                    Debug.WriteLine("lenguaje detectado: " + tlang);
                    v = (from VoiceInformation voice in AllVoices where voice.Language.ToLower().StartsWith(tlang) == true select voice).FirstOrDefault();
                    if (v == null)
                    {
                        v = DefaultVoice;
                    }
                    else
                    {
                        Debug.WriteLine("seleccionando voz " + v.DisplayName);
                    }
                }
                else
                {
                    v = DefaultVoice;
                }
            }
            else
            {
                v = DefaultVoice;
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
            CanPlay = true;
            await ProcesarCola();
        }

        public async Task ProcesarCola()
        {
            Debug.WriteLine("ProcesarCola");
            if (Mensajes.Any())
            {
                var msg = Mensajes.FirstOrDefault();
                if (msg == null)
                    return;
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
