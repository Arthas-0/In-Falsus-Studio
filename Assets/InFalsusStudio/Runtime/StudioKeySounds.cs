using System;
using System.Threading;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    /// <summary>One software-mixed FX bus, separate from the music AudioSource.</summary>
    public sealed class StudioKeySounds : IDisposable
    {
        private readonly StudioTransport transport;
        private readonly GameObject root;
        private readonly AudioSource output;
        private readonly StudioKeySoundOutput filter;
        private readonly AudioClip carrier;
        private readonly AudioClip[] clips=new AudioClip[6];
        private readonly PcmSample[] samples=new PcmSample[6];
        private readonly bool[] decodeFailed=new bool[6];
        private HitSoundPlan sourcePlan;
        private PcmSoundPlan plan;
        private bool running,disposed;
        private int revision=-1,frequency;
        private double endLimit=double.PositiveInfinity,gate,nextDecodeAttempt;
        private float master=-1,attack=-1,hold=-1;
        private string missing="";
        public bool Enabled=true,AutoPlayEnabled=true;
        public float MasterVolume=.65f,ShotVolume=.85f,HoldVolume=.35f;
        public int RealVoiceLimit {get;private set;}
        public int ShotBudget {get{return PcmKeySoundMixer.MaxAttackLayers;}}
        public int HoldBudget {get{return 2;}}
        public int LateDropped {get{return 0;}} // Compatibility diagnostic: no frame-based late scheduling.
        public int CapacityDropped {get{return (int)Math.Min(int.MaxValue,Interlocked.Read(ref filter.Mixer.AttackLayersTrimmed));}}
        public bool MusicVirtual {get{return transport.MusicVirtual;}}
        public string MissingClips {get{return missing;}}
        public const int MaxVoices=1;
        public StudioKeySounds(Transform parent,StudioTransport clock)
        {
            transport=clock;frequency=Math.Max(8000,AudioSettings.outputSampleRate);
            root=new GameObject("Key sounds - ONE PCM bus, music isolated");root.transform.SetParent(parent,false);
            output=root.AddComponent<AudioSource>();output.playOnAwake=false;output.spatialBlend=0;output.dopplerLevel=0;
            output.priority=1;output.pitch=1;output.volume=1;output.loop=true;
            filter=root.AddComponent<StudioKeySoundOutput>();
            // A silent carrier keeps the filter active. The filter generates the effects only.
            carrier=AudioClip.Create("PCM FX carrier",1024,2,frequency,false);output.clip=carrier;
            string[] names={"FloorHit","SideHit","SkyHit","Flick","FloorHold","SkyHold"};
            for(int i=0;i<6;i++){clips[i]=Resources.Load<AudioClip>("InFalsusStudio/KeySounds/"+names[i]);if(clips[i]!=null)clips[i].LoadAudioData();}
            DecodeReadySamples();
        }
        private bool DecodeReadySamples()
        {
            bool changed=false;missing="";
            for(int i=0;i<clips.Length;i++)
            {
                if(samples[i]!=null)continue;var clip=clips[i];
                if(clip==null){missing+=((HitSoundKind)i)+" missing; ";continue;}
                if(decodeFailed[i]){missing+=clip.name+" PCM decode failed; restart after reimport; ";continue;}
                if(clip.loadState!=AudioDataLoadState.Loaded){missing+=clip.name+" loading/failed; ";continue;}
                if(clip.samples<=0||clip.channels<1||clip.channels>8||((long)clip.samples*clip.channels)>12000000)
                {decodeFailed[i]=true;missing+=clip.name+" exceeds PCM safety limit; ";continue;}
                var data=new float[clip.samples*clip.channels];
                if(!clip.GetData(data,0)){decodeFailed[i]=true;missing+=clip.name+" GetData failed (DecompressOnLoad required); ";continue;}
                samples[i]=new PcmSample(data,clip.channels,clip.frequency);changed=true;
            }
            return changed;
        }
        public void Rebuild(HitSoundPlan value)
        {StopAll();sourcePlan=value;plan=value==null?null:new PcmSoundPlan(value,samples);}
        public void StopAll()
        {
            if(disposed)return;filter.Publish(null,frequency);running=false;
            if(output!=null)output.Stop(); // This is ONLY the FX source, never music.
        }
        public void Tick(double exclusiveEndMs=double.PositiveInfinity)
        {
            if(disposed)return;
            var configuration=AudioSettings.GetConfiguration();RealVoiceLimit=configuration.numRealVoices;
            int nextFrequency=Math.Max(8000,AudioSettings.outputSampleRate);
            bool decoded=false;
            if(!string.IsNullOrEmpty(missing)&&AudioSettings.dspTime>=nextDecodeAttempt)
            {nextDecodeAttempt=AudioSettings.dspTime+.5;decoded=DecodeReadySamples();}
            if(decoded&&sourcePlan!=null){plan=new PcmSoundPlan(sourcePlan,samples);running=false;}
            bool enabled=Enabled&&AutoPlayEnabled&&transport.Playing&&plan!=null;
            if(!enabled){if(running)StopAll();return;}
            // One music + one FX voice. If a host explicitly configured only one real voice,
            // disable effects rather than competing with/stealing the music voice.
            if(RealVoiceLimit<2){if(running)StopAll();return;}
            bool changed=!running||revision!=transport.Revision||endLimit!=exclusiveEndMs||frequency!=nextFrequency;
            if(changed)
            {
                gate=revision!=transport.Revision?transport.PlaybackStartMs:transport.TimeMs;
                revision=transport.Revision;endLimit=exclusiveEndMs;frequency=nextFrequency;
            }
            float m=Mathf.Clamp01(MasterVolume),a=Mathf.Clamp01(ShotVolume),h=Mathf.Clamp01(HoldVolume);
            if(changed||master!=m||attack!=a||hold!=h)
            {
                master=m;attack=a;hold=h;
                filter.Publish(new PcmMixState(plan,transport.PlaybackStartMs,transport.DspTimeAtChart(transport.PlaybackStartMs),
                    transport.Rate,gate,endLimit,m,a,h,true),frequency);
            }
            running=true;
            if(!output.isPlaying){output.priority=1;output.pitch=1;output.volume=1;output.Play();}
        }
        public string Diagnostic
        {
            get{return "PCM FX: 1 Unity voice / 2 independent hold beds | blocks "+Interlocked.Read(ref filter.Mixer.RenderedBlocks)+
                " | capped tail layers "+Interlocked.Read(ref filter.Mixer.AttackLayersTrimmed)+" | callback errors "+Interlocked.Read(ref filter.CallbackErrors)+
                "\nMusic priority "+transport.MusicPriority+" / virtual "+transport.MusicVirtual+" | FX virtual "+output.isVirtual+" | real voices "+RealVoiceLimit;}
        }
        public void Dispose()
        {if(disposed)return;StopAll();disposed=true;UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(carrier);}
    }
}
