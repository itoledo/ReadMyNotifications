#define MEDIAPLAYER
// #define SPEECH_WRITE_FILE
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Audio;
using Windows.Media.Core;
using Windows.Media.Devices;
using Windows.Media.Playback;
using Windows.Media.Render;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media.Imaging;
using MyToolkit.Mvvm;
using ReadMyNotifications.Language;
using ReadMyNotifications.Utils;
using SQLite;
using Windows.Media;
using Microsoft.QueryStringDotNET;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ReadMyNotifications.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private UserNotificationListener _listener;
        private LanguageDetector _detector;
        private ResourceLoader _l;
        public ObservableCollection<Notificacion> ListaNotificaciones { get; private set; }
        public ObservableCollection<VoiceInformation> AllVoices { get; private set; }
        private MediaPlayer _mediaPlayer;
        private MediaPlaybackList _mediaPlaybackList;
#if SMTC
        private SystemMediaTransportControls _smtc;
#endif
        private bool _initialized = false;
        Windows.Storage.ApplicationDataContainer settings = Windows.Storage.ApplicationData.Current.LocalSettings;
        SQLiteConnection _db;

        private SemaphoreSlim _fillNotificationsSemaphoreSlim = new SemaphoreSlim(1);
        private SemaphoreSlim _initializationSemaphoreSlim = new SemaphoreSlim(1);

        public static bool IsPhone
        {
            get { return DeviceTypeHelper.GetDeviceFormFactorType() == DeviceFormFactorType.Phone; }
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

        public class NotifId
        {
            [PrimaryKey]
            public uint Id { get; set; }
        }

        public MainViewModel()
        {
            _db = new SQLiteConnection("notifications.db3");
            _db.CreateTable<NotifId>();

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

        #region SETTINGS

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

        private bool _leerEnBackground;

        public bool LeerEnBackground
        {
            get { return _leerEnBackground; }
            set
            {
                _leerEnBackground = value;
                SaveSettings();
                RaisePropertyChanged(() => LeerEnBackground);
            }
        }

        private bool _leerHeadphones;

        public bool LeerHeadphones
        {
            get { return _leerHeadphones; }
            set
            {
                _leerHeadphones = value;
                SaveSettings();
                RaisePropertyChanged(() => LeerHeadphones);
            }
        }

        private bool _leerBluetooth;

        public bool LeerBluetooth
        {
            get { return _leerBluetooth; }
            set
            {
                _leerBluetooth = value;
                SaveSettings();
                RaisePropertyChanged(() => LeerBluetooth);
            }
        }

        private bool _leerSpeaker;

        public bool LeerSpeaker
        {
            get { return _leerSpeaker; }
            set
            {
                _leerSpeaker = value;
                SaveSettings();
                RaisePropertyChanged(() => LeerSpeaker);
            }
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

            if (settings.Values.ContainsKey("LeerEnBackground"))
            {
                _leerEnBackground = (bool) settings.Values["LeerEnBackground"];
                Debug.WriteLine($"ReadSetting: LeerEnBackground: {LeerEnBackground}");
            }
            else
                _leerEnBackground = false;

            if (settings.Values.ContainsKey("LeerSpeaker"))
            {
                _leerSpeaker = (bool) settings.Values["LeerSpeaker"];
                Debug.WriteLine($"ReadSetting: LeerSpeaker: {LeerSpeaker}");
            }
            else
                _leerSpeaker = true;

            if (settings.Values.ContainsKey("LeerHeadphones"))
            {
                _leerHeadphones = (bool) settings.Values["LeerHeadphones"];
                Debug.WriteLine($"ReadSetting: LeerHeadphones: {LeerHeadphones}");
            }
            else
                _leerHeadphones = true;

            if (settings.Values.ContainsKey("LeerBluetooth"))
            {
                _leerBluetooth = (bool) settings.Values["LeerBluetooth"];
                Debug.WriteLine($"ReadSetting: LeerBluetooth: {LeerBluetooth}");
            }
            else
                _leerBluetooth = true;
        }

        public void SaveSettings()
        {
            settings.Values["DeteccionAutomatica"] = DeteccionAutomatica;
            settings.Values["LeerEnBackground"] = LeerEnBackground;
            settings.Values["LeerSpeaker"] = LeerSpeaker;
            settings.Values["LeerHeadphones"] = LeerHeadphones;
            settings.Values["LeerBluetooth"] = LeerBluetooth;
            if (DefaultVoice != null)
                settings.Values["DefaultVoiceId"] = DefaultVoice.Id;
        }

        #endregion

        public async Task Init()
        {
            Debug.WriteLine("Init: Start");
            try
            {
                Debug.WriteLine("Init: Wait");
                await _initializationSemaphoreSlim.WaitAsync();

                if (_initialized == true)
                {
                    Debug.WriteLine("Init: ya inicializados, saliendo");
                    return;
                }

                Debug.WriteLine("Init: inicializando");
                _initialized = true;

                if (_l == null)
                    _l = new ResourceLoader();

                InitMediaPlayer();

                switch (await CheckListenerAccess())
                {
                    case 0:
                        await RegisterBackground();
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
            finally
            {
                _initializationSemaphoreSlim.Release();
            }
            Debug.WriteLine("Init: fin");
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

        public async Task RegisterBackground()
        {
            BackgroundExecutionManager.RemoveAccess();

            if (LeerEnBackground == false)
            {
                // quitemos el task
                var task = (from t in BackgroundTaskRegistration.AllTasks
                    where t.Value.Name.Equals("UserNotificationChanged")
                    select t.Value).FirstOrDefault();
                if (task != null)
                    task.Unregister(false);
                return;
            }

            await BackgroundExecutionManager.RequestAccessAsync();

            if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name.Equals("UserNotificationChanged")))
            {
                // Specify the background task
                var builder = new BackgroundTaskBuilder()
                {
                    Name = "UserNotificationChanged",
#if MULTI_PROCESS
                    TaskEntryPoint = "ReadMyNotifications.Background.PlayNotificationTask",
#endif
                };

                // Set the trigger for Listener, listening to Toast Notifications
                builder.SetTrigger(new UserNotificationChangedTrigger(NotificationKinds.Toast));

                // Register the task
                builder.Register();
            }

#if BACKGROUND_TOAST
            if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name.Equals("ToastAction")))
            {
                // Specify the background task
                var builder = new BackgroundTaskBuilder()
                {
                    Name = "ToastAction",
#if MULTI_PROCESS
                    TaskEntryPoint = "ReadMyNotifications.Background.PlayNotificationTask",
#endif
                };

                // Set the trigger for Listener, listening to Toast Notifications
                builder.SetTrigger(new ToastNotificationActionTrigger());

                // Register the task
                builder.Register();
            }
#endif
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

        private async Task<List<Notificacion>> GetNotifications(bool all)
        {
            Debug.WriteLine("obteniendo notificaciones");
            IReadOnlyList<UserNotification> newnotifs;
            // Get the toast notifications
            try
            {
                var listener = UserNotificationListener.Current;
                newnotifs = await listener.GetNotificationsAsync(NotificationKinds.Toast);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"excepcion: {e}");
                newnotifs = null;
            }

            if (newnotifs == null)
            {
//                await Speak(_l.GetString("ErrorGet"));
                return null;
            }

            var lista = new List<Notificacion>();

            Debug.WriteLine("iterando");

            foreach (var notif in newnotifs)
            {
                if (all == false)
                {
                    // comparemos con la bd
                    var existe = _db.Table<NotifId>().FirstOrDefault(z => z.Id == notif.Id);
                    if (existe != null)
                        continue;
                }

                var n = new Notificacion();
                try
                {
                    n.Id = notif.Id;
                    n.CreationTime = notif.CreationTime;

                    // Get the app's display name
                    string appDisplayName = notif.AppInfo.DisplayInfo.DisplayName;
                    n.AppName = appDisplayName;

                    //// Get the app's logo
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
                        /** esto puede fallar **/
//                        Debug.WriteLine($"excepcion: app logo: {e}");
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
                            {
                                lista.Add(n);
                            }
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

            return lista.OrderByDescending(f => f.CreationTime).ToList();
        }

        // notificaciones que se van a mostrar en pantalla
        public async Task FillNotifications()
        {
            try
            {
                Debug.WriteLine("FillNotifications: wait");
                await _fillNotificationsSemaphoreSlim.WaitAsync();
                Debug.WriteLine("FillNotifications: start");
                Getting = true;

                try
                {
                    var lista = await GetNotifications(true);
                    if (lista == null)
                    {
                        await new MessageDialog(_l.GetString("ErrorGet")).ShowAsync();
                        return;
                    }
                    lock (ListaNotificaciones)
                    {
                        ListaNotificaciones.Clear();
                        foreach (var n in lista.OrderByDescending(x => x.CreationTime))
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
            finally
            {
                _fillNotificationsSemaphoreSlim.Release();
            }
            Debug.WriteLine("FillNotifications: end");
        }

        private TaskCompletionSource<object> _taskMediaEnded;

        public void PrepareForMediaEnded()
        {
            // esperaremos hasta que termine el mediaplayer
            _taskMediaEnded = new TaskCompletionSource<object>();
        }

        public async Task WaitForMediaEnded()
        {
            Debug.WriteLine("WaitForMediaEnded: inicio");
            if (_taskMediaEnded != null)
                await _taskMediaEnded.Task;
            Debug.WriteLine("WaitForMediaEnded: fin");
        }

        public async Task<int> ReadAllNotifications(bool all = true, bool fromBackground = false)
        {
            Debug.WriteLine($"ReadAllNotifications: begin: {all}");
            int cnt = 0;
            var notifs = await GetNotifications(all);
            if (notifs != null)
                foreach (var n in notifs)
                {
                    cnt++;
                    await ReadNotification(n);
                }
            GuardarNotificaciones(notifs);
            string msg;

            // si ya hay un mensaje encolado, no pongamos otro por favor...
            if (fromBackground == false && _mediaPlaybackList.Items.All(i => i.Source.CustomProperties.ContainsKey("Id")))
            {
                if (cnt == 0)
                    msg = _l.GetString("NoNotifications");
                else
                    msg = _l.GetString("ReadEnd");

                var stream = await GenerarSpeech(msg);
                AddTrack(null, stream);
            }

            Debug.WriteLine("ReadAllNotifications: end");
            return cnt;
        }

        public void GuardarNotificaciones(List<Notificacion> lista)
        {
            try
            {
                _db.BeginTransaction();
                foreach (var n in lista)
                    _db.InsertOrReplace(new NotifId() { Id = n.Id });
                _db.Commit();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"excepcion en db: {e}");
                try
                {
                    _db.Rollback();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"excepcion en rollback: {ex}");
                }
            }
        }

        public bool IsNotificationInDb(uint id)
        {
            var existe = _db.Table<NotifId>().FirstOrDefault(z => z.Id == id);
            return existe != null;
        }

        public async Task ReadNotification(Notificacion n)
        {
            var stream = await GenerarSpeech($"{n.AppName}: {n.Title}. {n.Text}");
            AddTrack(n, stream);
        }

        public void Play()
        {
            _mediaPlayer.Play();
        }

        private bool _playing = false;

        public bool CanPlay
        {
            get { return !_playing; }
            set { _playing = !value; RaisePropertyChanged(() => CanPlay); }
        }

        public void StopMediaPlayer()
        {
            Debug.WriteLine("StopMediaPlayer");

            _mediaPlaybackList = new MediaPlaybackList();
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Source = _mediaPlaybackList;
            }

            if (_taskMediaEnded != null)
            {
                _taskMediaEnded.TrySetResult(null);
                _taskMediaEnded = null;
            }
        }

        private void InitMediaPlayer()
        {
            if (_mediaPlayer == null)
            {
                _mediaPlayer = new MediaPlayer();
                _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Speech;
                _mediaPlaybackList = new MediaPlaybackList();
                _mediaPlayer.Source = _mediaPlaybackList;
                //_mediaPlayer.MediaEnded -= MediaPlayerOnMediaEnded;
                _mediaPlayer.MediaOpened += MediaPlayerOnMediaOpened;
                _mediaPlayer.MediaEnded += MediaPlayerOnMediaEnded;
                _mediaPlayer.MediaFailed +=
                    (sender, args) => Debug.WriteLine($"media failed: {args.Error} - {args.ErrorMessage}");
//                _mediaPlayer.CommandManager.IsEnabled = false;
                //_mediaPlayer.AutoPlay = true;

#if SMTC
                // configurar STMC
                _smtc = _mediaPlayer.SystemMediaTransportControls;
                _smtc.ButtonPressed += SMTC_ButtonPressed;
                _smtc.PropertyChanged += SMTC_PropertyChanged;
                _smtc.IsEnabled = true;
                _smtc.IsStopEnabled = true;
                _smtc.IsPauseEnabled = true;
                _smtc.IsPlayEnabled = true;
                _smtc.IsNextEnabled = true;
                _smtc.IsPreviousEnabled = true;
#endif
            }
        }

        private void MediaPlayerOnMediaOpened(MediaPlayer sender, object args)
        {
            Debug.Write("MediaPlayerOnMediaOpened");
            sender.PlaybackSession.PlaybackStateChanged -= PlaybackSessionOnPlaybackStateChanged;
            sender.PlaybackSession.PlaybackStateChanged += PlaybackSessionOnPlaybackStateChanged;
        }

        private async void PlaybackSessionOnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            Debug.WriteLine($"PlaybackSessionOnPlaybackStateChanged: {sender.PlaybackState}");
            switch (sender.PlaybackState)
            {
                case MediaPlaybackState.Playing:
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal,
                        () =>
                        {
                            CanPlay = false;
                        });
                    break;
                default:
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal,
                        () =>
                        {
                            CanPlay = true;
                        });
                    break;
            }
        }

        private void AddTrack(Notificacion n, SpeechSynthesisStream stream)
        {
            if (n == null)
            {
                Debug.WriteLine($"Añadiendo track informativo");
            }
            else
            {
                Debug.WriteLine($"Añadiendo track para notificación {n.Id} - {n.AppName} - {n.Title}");
            }
            var src = MediaSource.CreateFromStream(stream, stream.ContentType);
            var mediaPlaybackItem = new MediaPlaybackItem(src);
            var props = mediaPlaybackItem.GetDisplayProperties();
            if (n != null)
            {
                src.CustomProperties.Add("Id", n.Id);
                props.MusicProperties.Title = n.AppName + " " + n.Title;
            }
            props.MusicProperties.Artist = _l.GetString("AppTitle");
            mediaPlaybackItem.ApplyDisplayProperties(props);
            _mediaPlaybackList.Items.Add(mediaPlaybackItem);
        }

        public bool IsSMTCMuted()
        {
            return _mediaPlayer.SystemMediaTransportControls.SoundLevel == SoundLevel.Muted;
        }

        public async Task<SpeechSynthesisStream> GenerarSpeech(string texto)
        {
            Debug.WriteLine("Reproducir: " + texto);

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
            SpeechSynthesisStream stream;
            using (var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer())
            {
                synth.Voice = v;
                stream = await synth.SynthesizeTextToStreamAsync(texto);
            }

            Debug.WriteLine("fin reproducir: " + texto);

            return stream;
        }

#if SMTC
        private bool _pausedDueToMute = false;

        private void SMTC_PropertyChanged(SystemMediaTransportControls sender, SystemMediaTransportControlsPropertyChangedEventArgs args)
        {
            Debug.WriteLine($"SMTC: PropertyChanged: {args.Property}");

            if (args.Property == SystemMediaTransportControlsProperty.SoundLevel)
            {
                Debug.WriteLine($"sound level: {sender.SoundLevel}");
                switch (sender.SoundLevel)
                {
                    case SoundLevel.Full:
                    case SoundLevel.Low:
                    if (_pausedDueToMute == true)
                    {
                        if (_mediaPlayer != null)
                            _mediaPlayer.Play();
                        _pausedDueToMute = false;
                    }
                    break;

                    case SoundLevel.Muted:
                    if (_mediaPlayer != null &&
                        _mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                    {
                        _mediaPlayer.Pause();
                        _pausedDueToMute = true;
                    }
                    break;
                }
            }
        }
#endif

        public void SendToast()
        {
            // mostremos un toast con la esperanza de que el usuario haga algo
            ToastVisual visual = new ToastVisual()
            {
                BindingGeneric = new ToastBindingGeneric()
                {
                    Children = {
                        new AdaptiveText()
                        {
                            Text = _l.GetString("AppNotRunning") // "Read My Notifications is not running")
                        },
                        new AdaptiveText()
                        {
                            Text = _l.GetString("SelectToRead") // "Select to read your notifications")
                        },
                    },
                    AppLogoOverride = new ToastGenericAppLogo()
                    {
                        Source = "ms-appx:///Assets/StoreLogo.png"
                    }
                }
            };
            ToastContent toastContent = new ToastContent()
            {
                Visual = visual,
                ActivationType = ToastActivationType.Foreground,

                // Arguments when the user taps body of toast
                Launch = new QueryString()
                {
                    { "action", "ToastRead" },
                }.ToString(),

                Scenario = ToastScenario.Default,
            };
            var toast = new ToastNotification(toastContent.GetXml());
            toast.SuppressPopup = false;
            toast.Group = "RMN";
            toast.Tag = "BR";
            ToastNotificationManager.History.RemoveGroup("RMN");
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

#if SMTC
        private void SMTC_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            Debug.WriteLine("SMTC: button pressed");
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    _mediaPlayer.Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    _mediaPlayer.Pause();
                    break;
                case SystemMediaTransportControlsButton.Stop:
                    _mediaPlayer.Pause();
                    _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
                    break;
                case SystemMediaTransportControlsButton.Next:
                    SaltaTrack(1);
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    SaltaTrack(-1);
                    break;
            }
        }

        private void SaltaTrack(int idx)
        {
            Debug.WriteLine($"SaltaTrack: {idx}");
            var cnt = _mediaPlaybackList.Items.Count();
            var pos = cnt + idx;
            if (pos >= cnt - 1)
                _smtc.IsNextEnabled = false;
            else
                _smtc.IsNextEnabled = true;
            if (pos <= 0)
                _smtc.IsPreviousEnabled = false;
            else
                _smtc.IsPreviousEnabled = true;
            _mediaPlaybackList.MoveTo((uint) pos);
        }
#endif

        private void MediaPlayerOnMediaEnded(MediaPlayer sender, object args)
        {
            Debug.WriteLine("MediaPlayerOnMediaEnded");

#if SMTC
            // veamos si quedan elementos en la cola
            if (_mediaPlaybackList.CurrentItemIndex < _mediaPlaybackList.Items.Count - 1)
            {
                // todavia hay items
                _mediaPlayer.AutoPlay = true;
                SaltaTrack(1);
            }
            else
            {
                _mediaPlayer.AutoPlay = false;
                _mediaPlayer.Pause();
                _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
            }
#endif
            StopMediaPlayer();
        }
    }
}
