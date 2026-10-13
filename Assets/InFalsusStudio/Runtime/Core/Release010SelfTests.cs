using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    // Deliberately a separate entry point: this update must not overwrite the user's
    // locally repaired build preflight or historical metadata test fixtures.
    public static class Release010SelfTests
    {
        private static bool Throws(Action a){try{a();return false;}catch{return true;}}
        private static PcmSample Constant(float value,int frames=800)
        {var data=new float[frames];for(int i=0;i<frames;i++)data[i]=value;return new PcmSample(data,1,8000);}
        private static PcmSample[] Bank()
        {return new[]{Constant(.01f),Constant(.02f),Constant(.03f),Constant(.04f),Constant(.2f),Constant(.3f)};}
        private static PcmSoundPlan Plan(string notes,PcmSample[] samples,bool heads=false,SkyAttackPolicy sky=SkyAttackPolicy.Off)
        {return new PcmSoundPlan(new HitSoundPlan(SpcDocument.Parse("chart(120,4)\n"+notes).ReadEvents(),sky,heads),samples);}
        private static float[] Read(PcmSoundPlan plan,double dspStart,int frames,double rate=1,double gate=0,double end=double.PositiveInfinity)
        {
            var data=new float[frames];new PcmKeySoundMixer().Render(data,1,8000,dspStart,
                new PcmMixState(plan,0,0,rate,gate,end,1,1,1,true));return data;
        }
        private static bool Zero(float[] x){return x.All(v=>Math.Abs(v)<1e-7);}
        public static void Run(Action<bool,string> check)
        {
            var bank=Bank();
            var beds=Plan("hold(1000,1,1,123)\nskyarea(1000,1,2,1,1,2,1,0,0,211,4)\n",bank);
            check(Math.Abs(Read(beds,1.05,1)[0]-.5)<1e-6,"010 ground and sky loops sum independently");
            check(Math.Abs(Read(beds,1.13,1)[0]-.3)<1e-6,"010 ground tail stops while sky continues");
            check(Zero(Read(beds,1.211,300)),"010 hold/sky stop exactly at tail even halfway through sample loop");
            check(Zero(Read(beds,.999,1)),"010 no sustain before head");
            check(!Zero(Read(beds,1.122,1)),"010 in-range samples before tail remain audible");
            check(Zero(Read(beds,1.05,100,1,0,1049)),"010 exclusive loop B cancels both beds");
            check(Math.Abs(Read(beds,1.08,1,1,1070)[0]-.5)<1e-6,"010 resume mid-hold restores both buses without historical heads");
            var gaps=Plan("skyarea(1000,1,2,1,1,2,1,0,0,39,4)\nskyarea(1077,1,2,1,1,2,1,0,0,39,5)",bank);
            check(Zero(Read(gaps,1.040,250)),"010 short sky gap is silent rather than waiting for loop end");
            check(!Zero(Read(gaps,1.080,120)),"010 next short Sky is not lost to a future voice reservation");
            foreach(double rate in new[]{1d,.75,.5,.25})
            {
                check(!Zero(Read(beds,1005/(rate*1000),5,rate)),"010 sustain onset at chart rate "+rate);
                check(Zero(Read(beds,1211/(rate*1000),64,rate)),"010 exact end at chart rate "+rate);
            }
            // Same PCM phase per REAL second at every chart playback rate.
            var sine=new float[800];for(int i=0;i<sine.Length;i++)sine[i]=(float)(.2*Math.Sin(i*Math.PI/10));
            var pitchBank=Bank();pitchBank[(int)HitSoundKind.FloorHold]=new PcmSample(sine,1,8000);
            var pitchPlan=Plan("hold(1000,1,1,1000)",pitchBank);
            var normal=Read(pitchPlan,1.04,240);
            foreach(double rate in new[]{.75,.5,.25})
            {var slow=Read(pitchPlan,1/rate+.04,240,rate);check(normal.Zip(slow,(a,b)=>Math.Abs(a-b)).Max()<1e-5,"010 key pitch invariant at "+rate);}
            // DSP buffer partitioning must not change placement or envelopes.
            var entire=Read(beds,.98,2200);var split=new List<float>();int offset=0;
            foreach(int n in new[]{127,311,512,1024,226}){split.AddRange(Read(beds,.98+offset/8000d,n));offset+=n;}
            check(entire.Zip(split,(a,b)=>Math.Abs(a-b)).Max()<1e-5,"010 callback block sizes do not change PCM phase/tail");
            var routes=Plan("hold(1000,0,1,1000)\nhold(2000,3,1,1000)\nhold(3000,5,1,1000)",bank,true);
            check(routes.Shots.Select(x=>x.Kind).SequenceEqual(new[]{HitSoundKind.SideHit,HitSoundKind.FloorHit,HitSoundKind.SideHit}),"010 Hold heads use side/floor/side attack routing");
            var group=Plan("skyarea(1000,1,2,1,1,2,1,0,0,39,7)\nskyarea(1300,1,2,1,1,2,1,0,0,39,7)\nskyarea(1400,1,2,1,1,2,1,0,0,39,8)",bank,false,SkyAttackPolicy.ContinuousRegionStart);
            check(group.Shots.Length==2&&group.Sky.Length==3,"010 group-only heads do not keep a loop sounding across gaps");
            var dense="";for(int i=0;i<200;i++)dense+="flick("+(1000+i*10)+",1,2,1,16)\n";
            dense+="tap(3030,1,1)\nskyarea(3100,1,2,1,1,2,1,0,0,333,9)\n";
            var longBank=Bank();longBank[(int)HitSoundKind.Flick]=Constant(.005f,24000);
            var densePlan=Plan(dense,longBank,true,SkyAttackPolicy.ContinuousRegionStart);
            check(!Zero(Read(densePlan,3.035,40)),"010 attack after dense burst remains audible");
            check(!Zero(Read(densePlan,3.11,40)),"010 Sky following dense attacks gets a sustain bus");
            var mixer=new PcmKeySoundMixer();var buffer=new float[256];
            mixer.Render(buffer,1,8000,2.95,new PcmMixState(densePlan,0,0,1,0,double.PositiveInfinity,1,1,1,true));
            check(mixer.AttackLayersTrimmed>0&&!Zero(buffer),"010 bounded old tails prefer newest cues, not multi-second dead time");
            mixer.Render(buffer,1,8000,3,null);check(Zero(buffer),"010 paused publication clears old output");
            // Editing safety is atomic and does not alter source content on refusal.
            var doc=SpcDocument.Parse("chart(120,4)\nhold(500,1,1,1000)\nskyarea(500,1,2,1,1,2,1,0,0,1000,9)\n");
            var history=new EditHistory(doc);history.BeforeCommit=AuthoringSafety.BeforeCommit;var original=doc.ToBytes();
            check(Throws(()=>history.Execute(d=>AuthoredNumber.Set(d,1,3,9000000000))),"010 rejects enormous Hold before render/count/group normalization");
            check(history.UndoCount==0&&history.Document.ToBytes().SequenceEqual(original),"010 invalid numeric edit keeps exact source and undo count");
            check(Throws(()=>history.Execute(d=>AuthoredNumber.Set(d,2,9,300001))),"010 single Sky five-minute upper limit");
            history.Execute(d=>AuthoredNumber.Set(d,1,3,300000));check(history.UndoCount==1,"010 exactly 300000ms is allowed");
            check(history.Undo()&&history.Document.ToBytes().SequenceEqual(original),"010 permitted edit has normal undo");
            var legacy=SpcDocument.Parse("chart(120,4)\nhold(500,1,1,9000000000)\n");
            var unsafeNote=legacy.ReadEvents().First(x=>x.Kind==EventKind.Hold);string unchanged=legacy.ToText();
            check(ChartMath.InvalidReason(unsafeNote)!=null&&legacy.ToText()==unchanged,"010 unsafe imported source quarantined, never truncated on load");
            var pending=new PointPlacement(EventKind.Hold);check(pending.ChooseTime(500,1,1),"010 Hold draft head");
            check(!pending.ChooseTime(9000000000,1,1)&&pending.Phase==PlacementPhase.EndTime,"010 invalid draft end leaves prior endpoint intact");
            check(pending.ChooseTime(300500,1,1),"010 draft maximum duration inclusive");
            foreach(double v in new[]{double.NaN,double.PositiveInfinity,-1,9000000000d})
                check(Throws(()=>AuthoringSafety.Time(v)),"010 unsafe timeline value rejected "+v);
            foreach(var wh in new[]{new[]{1280d,720},new[]{1920d,1080},new[]{2560d,1440},new[]{3840d,2160}})
            {
                double s=OverlayMetrics.Scale(wh[0],wh[1]);
                check(Math.Abs(OverlayMetrics.RadialRadius*s/wh[1]-150d/1080)<1e-10,"010 radial radius proportional "+wh[0]);
                check(Math.Abs(OverlayMetrics.ButtonHeight*s/wh[1]-50d/1080)<1e-10,"010 hit target proportional "+wh[0]);
                check(Math.Abs(OverlayMetrics.FontSize*s/wh[1]-17d/1080)<1e-10,"010 typography proportional "+wh[0]);
            }
            check(OverlayMetrics.Scale(3840,1080)==1,"010 scale follows calibrated viewport, not ultrawide window width");
        }
    }
}
