using System;
using UnityEngine;

namespace InFalsusStudio
{
    public sealed class StudioTransport : IDisposable
    {
        private readonly AudioSource source;
        private AudioClip originalClip,stretchedClip;
        private float[] originalPcm;
        private double baseMs,anchorDsp;
        public bool Playing {get;private set;}
        public int Revision {get;private set;}
        public double PlaybackStartMs {get{return baseMs;}}
        public double DspTimeAtChart(double chartMs){return anchorDsp+(chartMs-baseMs)/(1000*Rate);}
        public double ChartTimeAtDsp(double dsp){return baseMs+(dsp-anchorDsp)*1000*Rate;}
        public double Rate {get;private set;}
        public double AudioOffsetMs {get;private set;}
        public float Volume {get{return source.volume;}set{source.volume=Mathf.Clamp01(value);}}
        public bool MusicVirtual {get{return source.isVirtual;}}
        public bool MusicPlaying {get{return source.isPlaying;}}
        public int MusicPriority {get{return source.priority;}}
        public AudioClip Clip {get{return originalClip;}}
        public double TimeMs {get{return Playing?baseMs+Math.Max(0,AudioSettings.dspTime-anchorDsp)*1000*Rate:baseMs;}}
        public StudioTransport(AudioSource audioSource){source=audioSource;originalClip=source.clip;source.playOnAwake=false;source.spatialBlend=0;source.dopplerLevel=0;source.priority=InFalsusStudio.Core.AudioVoiceBudget.MusicPriority;Rate=1;}
        public void SetClip(AudioClip clip){bool run=Playing;Pause();ReleaseStretch();originalClip=clip;originalPcm=null;source.clip=clip;if(run)Play();}
        public void SetRate(double rate)
        {
            if(!InFalsusStudio.Core.AuthoringSafety.Finite(rate))throw new ArgumentOutOfRangeException("rate");
            bool run=Playing;Pause();Rate=Math.Max(.25,Math.Min(2,rate));if(run)Play();
        }
        public void SetAudioOffset(double ms){bool run=Playing;Pause();AudioOffsetMs=ms;if(run)Play();}
        public void Play()
        {
            if(Playing)return;source.Stop();PreparePlaybackClip();
            source.priority=InFalsusStudio.Core.AudioVoiceBudget.MusicPriority;Revision++;anchorDsp=AudioSettings.dspTime+.08;Playing=true;
            if(originalClip==null)return;
            double audioMs=baseMs+AudioOffsetMs;if(audioMs>=originalClip.length*1000)return;
            double delay=audioMs<0?-audioMs/(1000*Rate):0;
            source.pitch=1;
            double scale=stretchedClip!=null?Rate:1;
            source.timeSamples=(int)Math.Min(source.clip.samples-1,Math.Max(0,audioMs*.001*originalClip.frequency/scale));
            source.PlayScheduled(anchorDsp+delay);
        }
        public void Pause(){if(Playing)baseMs=TimeMs;Playing=false;Revision++;source.Stop();}
        public void Seek(double ms){bool run=Playing;Pause();baseMs=Math.Max(0,ms);if(run)Play();}
        public void Toggle(){if(Playing)Pause();else Play();}
        private void PreparePlaybackClip()
        {
            ReleaseStretch();source.pitch=1;source.clip=originalClip;
            if(originalClip==null||Math.Abs(Rate-1)<1e-9)return;
            if(originalPcm==null)
            {
                long count=(long)originalClip.samples*originalClip.channels;
                if(count>128L*1024*1024)throw new InvalidOperationException("Music PCM exceeds the 512 MiB time-stretch budget.");
                var pcm=new float[(int)count];
                if(!originalClip.GetData(pcm,0))throw new InvalidOperationException("Cannot read decoded music PCM for pitch-preserving playback.");
                originalPcm=pcm;
            }
            var processor=new InFalsusStudio.Core.WsolaTimeStretch(originalPcm,originalClip.channels,originalClip.frequency,Rate);
            var gate=new object();
            stretchedClip=AudioClip.Create(originalClip.name+" · pitch-preserved "+Rate.ToString("0.##"),processor.OutputFrames,
                originalClip.channels,originalClip.frequency,true,
                data=>{lock(gate)processor.Read(data);},position=>{lock(gate)processor.ResetOutputPosition(position);});
            source.clip=stretchedClip;
        }
        private void ReleaseStretch()
        {
            if(stretchedClip==null)return;
            if(Application.isPlaying)UnityEngine.Object.Destroy(stretchedClip);else UnityEngine.Object.DestroyImmediate(stretchedClip);
            stretchedClip=null;
        }
        public void Dispose(){Pause();source.clip=null;ReleaseStretch();originalClip=null;originalPcm=null;}
    }
}
