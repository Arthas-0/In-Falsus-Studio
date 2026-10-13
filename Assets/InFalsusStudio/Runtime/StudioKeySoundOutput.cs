using System;
using System.Threading;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class StudioKeySoundOutput : MonoBehaviour
    {
        private PcmMixState state;
        private int frequency=48000;
        public readonly PcmKeySoundMixer Mixer=new PcmKeySoundMixer();
        public long CallbackErrors;
        public void Publish(PcmMixState next,int outputFrequency)
        {Volatile.Write(ref frequency,outputFrequency);Volatile.Write(ref state,next);}
        private void OnAudioFilterRead(float[] data,int channels)
        {
            // AudioSettings.dspTime is the audio clock used by Unity's official filter example.
            // Everything else, including decoded samples, has been copied on the main thread.
            try{Mixer.Render(data,channels,Volatile.Read(ref frequency),AudioSettings.dspTime,Volatile.Read(ref state));}
            catch{Array.Clear(data,0,data.Length);Interlocked.Increment(ref CallbackErrors);}
        }
    }
}
