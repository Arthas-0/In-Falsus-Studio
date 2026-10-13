using System;
using System.Collections.Generic;
using System.Threading;

namespace InFalsusStudio.Core
{
    // Immutable copies: Unity objects are NEVER touched from the audio thread.
    public sealed class PcmSample
    {
        public readonly float[] Data;
        public readonly int Channels, Frequency, Frames;
        public double Duration { get { return (double)Frames/Frequency; } }
        public PcmSample(float[] data, int channels, int frequency)
        {
            if(data==null||channels<1||channels>8||frequency<8000||data.Length%channels!=0)
                throw new ArgumentException("Invalid decoded key sample.");
            Data=data;Channels=channels;Frequency=frequency;Frames=data.Length/channels;
        }
        public float At(double frame,int channel,bool loop)
        {
            if(Frames==0||frame<0)return 0;
            if(loop)frame%=Frames;
            else if(frame>=Frames)return 0;
            int a=(int)frame,b=a+1;float f=(float)(frame-a);
            if(b>=Frames)b=loop?0:a;
            int c=Channels==1?0:channel%Channels;
            return Data[a*Channels+c]+(Data[b*Channels+c]-Data[a*Channels+c])*f;
        }
    }
    public struct PcmCue
    {
        public readonly double Start,End;
        public readonly HitSoundKind Kind;
        public PcmCue(HitSoundCue c){Start=c.Start;End=c.End;Kind=c.Kind;}
    }
    public sealed class PcmSoundPlan
    {
        public readonly PcmCue[] Shots,Floor,Sky;
        public readonly PcmSample[] Samples;
        public readonly double MaxAttackSeconds;
        public PcmSoundPlan(HitSoundPlan source,PcmSample[] samples)
        {
            Samples=(PcmSample[])samples.Clone();
            var shots=new List<PcmCue>();var floor=new List<PcmCue>();var sky=new List<PcmCue>();
            foreach(var s in source.Shots)
            {
                // Coalesce only same time + same sample. Chart/combo objects remain distinct.
                if(shots.Count>0&&shots[shots.Count-1].Start==s.Start&&shots[shots.Count-1].Kind==s.Kind)continue;
                shots.Add(new PcmCue(s));
            }
            foreach(var c in source.Holds)(c.Kind==HitSoundKind.FloorHold?floor:sky).Add(new PcmCue(c));
            Shots=shots.ToArray();Floor=floor.ToArray();Sky=sky.ToArray();
            double max=0;for(int i=0;i<4;i++)if(Samples[i]!=null)max=Math.Max(max,Samples[i].Duration);
            MaxAttackSeconds=max;
        }
        public static int LowerBound(PcmCue[] a,double t)
        {int l=0,h=a.Length;while(l<h){int m=l+(h-l)/2;if(a[m].Start<t)l=m+1;else h=m;}return l;}
    }
    public sealed class PcmMixState
    {
        public readonly PcmSoundPlan Plan;
        public readonly double OriginMs,OriginDsp,Rate,AuditionStartMs,EndMs;
        public readonly float Master,AttackGain,HoldGain;
        public readonly bool Enabled;
        public PcmMixState(PcmSoundPlan plan,double ms,double dsp,double rate,double auditionStart,double end,
            float master,float attack,float hold,bool enabled)
        {
            Plan=plan;OriginMs=ms;OriginDsp=dsp;Rate=rate;AuditionStartMs=auditionStart;EndMs=end;
            Master=master;AttackGain=attack;HoldGain=hold;Enabled=enabled;
        }
        public double DspAt(double ms){return OriginDsp+(ms-OriginMs)/(Rate*1000);}
        public double TimeAt(double dsp){return OriginMs+(dsp-OriginDsp)*Rate*1000;}
    }
    /// <summary>
    /// One Unity audio voice for ALL effects. No per-event AudioSources, no future voice
    /// reservations, no main-frame scheduling horizon. Rendering is random-access in DSP
    /// time; two separate hold beds stop at the exact source tail even halfway through a loop.
    /// </summary>
    public sealed class PcmKeySoundMixer
    {
        public const int MaxAttackLayers=64;
        public long RenderedBlocks,AttackLayersTrimmed,LimitedSamples;
        // Runs exclusively on the audio thread. No allocation, IO, locks or Unity APIs here.
        public void Render(float[] output,int channels,int sampleRate,double dspStart,PcmMixState state)
        {
            Array.Clear(output,0,output.Length);
            if(channels<=0||sampleRate<=0||state==null||!state.Enabled||state.Plan==null||state.Rate<=0||double.IsNaN(state.Rate)||double.IsInfinity(state.Rate)||
                double.IsNaN(dspStart)||double.IsInfinity(dspStart)||double.IsNaN(state.OriginMs)||double.IsNaN(state.OriginDsp))return;
            int frames=output.Length/channels;if(frames==0)return;
            double dspEnd=dspStart+(double)frames/sampleRate;
            double gate=state.DspAt(state.AuditionStartMs),end=state.DspAt(state.EndMs);
            if(dspEnd<=gate||dspStart>=end)return;
            PcmSoundPlan p=state.Plan;
            double a=state.TimeAt(dspStart),b=state.TimeAt(dspEnd);
            int first=PcmSoundPlan.LowerBound(p.Shots,Math.Max(state.AuditionStartMs,a-p.MaxAttackSeconds*state.Rate*1000));
            int last=PcmSoundPlan.LowerBound(p.Shots,Math.Min(b,state.EndMs));
            // Bound DSP cost. Prefer new attacks over old reverb tails, never reject a new
            // long Sky because short previous holds reserved all four future voices.
            if(last-first>MaxAttackLayers){Interlocked.Add(ref AttackLayersTrimmed,last-first-MaxAttackLayers);first=last-MaxAttackLayers;}
            for(int i=first;i<last;i++)MixCue(output,channels,sampleRate,dspStart,gate,end,state,p.Shots[i],false);
            MixBed(output,channels,sampleRate,dspStart,dspEnd,gate,end,state,p.Floor);
            MixBed(output,channels,sampleRate,dspStart,dspEnd,gate,end,state,p.Sky);
            for(int i=0;i<output.Length;i++)
            {
                float x=output[i];
                if(float.IsNaN(x)||float.IsInfinity(x)){output[i]=0;continue;}
                // FX-only peak guard: never changes or ducks the music source.
                float v=Math.Abs(x);
                if(v>.75f){output[i]=Math.Sign(x)*(.75f+.20f*(1-1/(1+(v-.75f)*5)));Interlocked.Increment(ref LimitedSamples);}
            }
            Interlocked.Increment(ref RenderedBlocks);
        }
        private static void MixBed(float[] dst,int ch,int hz,double start,double end,double gate,double limit,PcmMixState s,PcmCue[] a)
        {
            int i=Math.Max(0,PcmSoundPlan.LowerBound(a,s.TimeAt(start))-1);
            while(i<a.Length&&s.DspAt(a[i].Start)<end&&a[i].Start<s.EndMs)
            {if(s.DspAt(a[i].End)>start)MixCue(dst,ch,hz,start,gate,limit,s,a[i],true);i++;}
        }
        private static void MixCue(float[] dst,int ch,int hz,double blockStart,double gate,double limit,PcmMixState s,PcmCue cue,bool loop)
        {
            PcmSample pcm=s.Plan.Samples[(int)cue.Kind];if(pcm==null||pcm.Frames==0)return;
            double origin=s.DspAt(cue.Start);
            double finish=Math.Min(limit,loop?s.DspAt(cue.End):origin+pcm.Duration);
            double begin=Math.Max(origin,gate);
            if(finish<=begin)return;
            int total=dst.Length/ch;
            // [start,end): ceil sample boundaries avoid playing any samples after the tail.
            int from=(int)Math.Max(0,Math.Min(total,Math.Ceiling((begin-blockStart)*hz-1e-7)));
            int to=(int)Math.Max(0,Math.Min(total,Math.Ceiling((finish-blockStart)*hz-1e-7)));
            if(from>=to)return;
            double phase=(blockStart+(double)from/hz-origin)*pcm.Frequency;
            double step=(double)pcm.Frequency/hz; // Fixed pitch=1, NOT multiplied by playback Rate.
            float gain=s.Master*(loop?s.HoldGain:s.AttackGain);
            for(int f=from;f<to;f++,phase+=step)
            {
                double at=blockStart+(double)f/hz;
                // A tiny in-range fade prevents clicks. No sample extends beyond note/loopB.
                double envelope=loop?Math.Min(1,Math.Min((at-begin)/.002,(finish-at)/.003)):Math.Min(1,(finish-at)/.002);
                float g=gain*(float)Math.Max(0,envelope);
                for(int c=0;c<ch;c++)dst[f*ch+c]+=pcm.At(phase,c,loop)*g;
            }
        }
    }
}
