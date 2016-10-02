using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace ReadMyNotifications.Background
{
    public sealed class PlayNotificationTask : IBackgroundTask
    {
        BackgroundTaskDeferral _deferral;
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            Debug.WriteLine("PlayNotificationTask: Run");
            _deferral = taskInstance.GetDeferral();

            SpeechSynthesisStream stream;
            using (var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer())
            {
                synth.Voice = SpeechSynthesizer.DefaultVoice;
                stream = await synth.SynthesizeTextToStreamAsync("nueva notificación");
            }

            // Send the stream to the media object.
            var mediaSource = MediaSource.CreateFromStream(stream, stream.ContentType);
            var mediaPlayer = new MediaPlayer();
            mediaPlayer.Source = mediaSource;
            mediaPlayer.Play();
            //BackgroundMediaPlayer.Current.Source = mediaSource;
            //BackgroundMediaPlayer.Current.Play();

            _deferral.Complete();
        }
    }
}
