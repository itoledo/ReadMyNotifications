using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Devices;
using Windows.Media.Playback;
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

namespace ReadMyNotifications.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private UserNotificationListener _listener;
        private LanguageDetector _detector;
        private ResourceLoader _l;
        public ObservableCollection<Notificacion> ListaNotificaciones { get; private set; }
        public ObservableCollection<VoiceInformation> AllVoices { get; private set; }

        public bool IsPhone
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

        Windows.Storage.ApplicationDataContainer settings = Windows.Storage.ApplicationData.Current.LocalSettings;
        SQLiteConnection _db;

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
                _leerEnBackground = (bool)settings.Values["LeerEnBackground"];
                Debug.WriteLine($"ReadSetting: LeerEnBackground: {LeerEnBackground}");
            }
            else
                _leerEnBackground = true;

            if (settings.Values.ContainsKey("LeerSpeaker"))
            {
                _leerSpeaker = (bool)settings.Values["LeerSpeaker"];
                Debug.WriteLine($"ReadSetting: LeerSpeaker: {LeerSpeaker}");
            }
            else
                _leerSpeaker = true;

            if (settings.Values.ContainsKey("LeerHeadphones"))
            {
                _leerHeadphones = (bool)settings.Values["LeerHeadphones"];
                Debug.WriteLine($"ReadSetting: LeerHeadphones: {LeerHeadphones}");
            }
            else
                _leerHeadphones = true;

            if (settings.Values.ContainsKey("LeerBluetooth"))
            {
                _leerBluetooth = (bool)settings.Values["LeerBluetooth"];
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

        private MediaPlayer _mediaPlayer;
        private MediaPlaybackList _mediaPlaybackList;
        private bool _initialized = false;

        public async Task Init()
        {
            if (_initialized == true)
                return;

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
                return;

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

        private bool _checking = false;

        public async Task CheckNewNotifications()
        {
            if (_checking == true)
            {
                Debug.WriteLine("skipping");
                return;
            }

            _checking = true;

            try
            {
                Debug.WriteLine("obteniendo notificaciones");
                IReadOnlyList<UserNotification> newnotifs;
                // Get the toast notifications
                try
                {
                    var listener = UserNotificationListener.Current;
                    newnotifs = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"excepcion: {e}");
                    newnotifs = null;
                }

                if (newnotifs == null)
                {
                    await Speak(_l.GetString("ErrorGet"));
                    return;
                }

                var lista = new List<Notificacion>();

                Debug.WriteLine("iterando");

                foreach (var notif in newnotifs)
                {
                    // comparemos con la bd
                    var existe = _db.Table<NotifId>().FirstOrDefault(z => z.Id == notif.Id);
                    if (existe != null)
                        continue;

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
                                {
                                    lista.Add(n);
                                    await ReadNotification(n);
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
                lock (ListaNotificaciones)
                {
                    var listafull = new List<Notificacion>();
                    foreach (var n in ListaNotificaciones)
                        listafull.Add(n);
                    foreach (var n in lista)
                        listafull.Add(n);
                    var olista = listafull.OrderByDescending(f => f.CreationTime);
                    ListaNotificaciones.Clear();
                    foreach (var n in olista)
                        ListaNotificaciones.Add(n);
                    try
                    {
                        _db.BeginTransaction();
                        // solo las nuevas
                        foreach (var n in lista)
                            _db.Insert(new NotifId() { Id = n.Id });
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
            }
            catch (Exception e)
            {
                // ???
                Debug.WriteLine($"excepción general: {e}");
            }
            finally
            {
                _checking = false;
            }
            Debug.WriteLine("fin");
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
                        n.Id = notif.Id;
                        n.CreationTime = notif.CreationTime;

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
                    foreach (var n in lista.OrderByDescending(x => x.CreationTime))
                        ListaNotificaciones.Add(n);
                    try
                    {
                        _db.BeginTransaction();
                        foreach (var n in lista)
                            _db.Insert(new NotifId() {Id = n.Id});
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

        private bool _playing = false;

        public async Task Speak(string texto)
        {
            Debug.WriteLine("Speak: " + texto);
            // The media object for controlling and playing audio.

            await Reproducir(texto);
        }

        public bool CanPlay
        {
            get { return !_playing; } set { _playing = !value; RaisePropertyChanged(() => CanPlay); }
        }

        public void StopMediaPlayer()
        {
            Debug.WriteLine("StopMediaPlayer");
            if (Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow == null
                || Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess == false)
            {
                Debug.WriteLine("modo background, ignorando stop");
                return;
            }

            if (_mediaPlaybackList != null && _mediaPlaybackList.Items != null)
                _mediaPlaybackList.Items.Clear();
            if (_mediaPlayer != null)
                _mediaPlayer.Pause();
            if (Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow != null
            && Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess == true)
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    CanPlay = true;
                });
        }

        private void InitMediaPlayer()
        {
            if (_mediaPlayer == null)
            {
                _mediaPlayer = new MediaPlayer();
                _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Speech;
                _mediaPlaybackList = new MediaPlaybackList();
                _mediaPlayer.Source = _mediaPlaybackList;
                _mediaPlayer.MediaEnded -= MediaPlayerOnMediaEnded;
                _mediaPlayer.MediaEnded += MediaPlayerOnMediaEnded;
            }
        }

        public async Task Reproducir(string texto)
        {
            Debug.WriteLine("Reproducir: " + texto);

            InitMediaPlayer();
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

            // Generate the audio stream from plain text.
            //Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
            //    CoreDispatcherPriority.Normal,
            //    async () =>
            //    {
            //Debug.WriteLine("Generando Speech: await");
            //var view = Windows.ApplicationModel.Core.CoreApplication.MainView;
            //await view.Dispatcher.RunAsync(
            ////await view.CoreWindow.Dispatcher.RunAsync(
            //    CoreDispatcherPriority.Normal,
            //    async () =>
            //    {
                    Debug.WriteLine("Generando Speech");
            // The object for controlling the speech synthesis engine (voice).
                    SpeechSynthesisStream stream;
                    using (var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer())
                    {
                        synth.Voice = v;
                        stream = await synth.SynthesizeTextToStreamAsync(texto);
                    }
                    Debug.WriteLine("Generando Speech: post await");
            if (Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow != null
                && Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess == true)
            {
                // Send the stream to the media object.
                stream.Seek(0);
                var mediaSource = MediaSource.CreateFromStream(stream, stream.ContentType);
                var mediaPlaybackItem = new MediaPlaybackItem(mediaSource);

                _mediaPlaybackList.Items.Add(mediaPlaybackItem);
                _mediaPlayer.Play();
            }
            else
            {
                Debug.WriteLine("background mode");

//                var folder = ApplicationData.Current.LocalFolder;
                var folder = KnownFolders.MusicLibrary;
                var file = await folder.CreateFileAsync("speech.wav", CreationCollisionOption.GenerateUniqueName);
                using (var targetStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                using (var reader = new DataReader(stream.GetInputStreamAt(0)))
                {
                    var os = targetStream.GetOutputStreamAt(0);
                    await reader.LoadAsync((uint)stream.Size);
                    while (reader.UnconsumedBufferLength > 0)
                    {
                        uint dataToRead = reader.UnconsumedBufferLength > 64
                                        ? 64
                                        : reader.UnconsumedBufferLength;

                        IBuffer buffer = reader.ReadBuffer(dataToRead);

                        await os.WriteAsync(buffer);
                    }
                    await os.FlushAsync();
                }

                var player = new MediaPlayer();
                player.PlaybackSession.PlaybackStateChanged +=
                    (sender, args) => Debug.WriteLine($"Background Player: state: {player.PlaybackSession.PlaybackState}");
                player.MediaFailed +=
                    (sender, args) => Debug.WriteLine($"media failed: {args.Error} - {args.ErrorMessage}");
                //var player = BackgroundMediaPlayer.Current;
                player.AudioCategory = MediaPlayerAudioCategory.Speech;
                //var uri = new Uri("ms-appdata:///local/" + file.Name);
                //                var src = MediaSource.CreateFromStorageFile(file);
                 //var src = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Alarm01.wav"));
                //var src = MediaSource.CreateFromUri(uri);
                stream.Seek(0);
                var src = MediaSource.CreateFromStream(stream, stream.ContentType);
                //player.SetUriSource(uri);
                player.Source = src;

                string audioSelector = MediaDevice.GetAudioRenderSelector();
                var outputDevices = await DeviceInformation.FindAllAsync(audioSelector);
                foreach (var device in outputDevices)
                {
                    Debug.WriteLine($"dev: {device.Id} - {device.Name}");
                }
                player.AudioDevice = outputDevices[2];

                player.Play();
                //BackgroundMediaPlayer.Current.Source = mediaPlaybackItem;
                //BackgroundMediaPlayer.Current.Play();
            }
                //});
        }

        private async void MediaPlayerOnMediaEnded(MediaPlayer sender, object args)
        {
            Debug.WriteLine("MediaElementOnMediaEnded");

            StopMediaPlayer();
        }
    }
}
