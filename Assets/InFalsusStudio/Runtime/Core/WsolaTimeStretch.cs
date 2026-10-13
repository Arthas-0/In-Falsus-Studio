using System;

namespace InFalsusStudio.Core
{
    // Original WSOLA implementation. Original-rate grains are waveform-aligned.
    // All channels share an alignment; PCM is immutable and buffers are preallocated.
    public sealed class WsolaTimeStretch
    {
        private readonly float[] source,previous,current,output;
        private readonly double[] weights;
        private readonly int channels,sourceFrames,hop,radius,coarseStep,stride;
        private readonly double rate;
        private int outputPosition,outputOffset,grain;
        private double startSource;
        private bool first;
        public readonly int OutputFrames;
        public WsolaTimeStretch(float[] pcm,int channelCount,int frequency,double playbackRate)
        {
            if(pcm==null||channelCount<1||channelCount>8||frequency<8000||pcm.Length%channelCount!=0||pcm.Length==0)
                throw new ArgumentException("Interleaved PCM with 1..8 channels, frequency >=8000 required.");
            if(!AuthoringSafety.Finite(playbackRate)||playbackRate<.25||playbackRate>2)throw new ArgumentOutOfRangeException("playbackRate");
            source=pcm;channels=channelCount;sourceFrames=pcm.Length/channels;rate=playbackRate;
            double length=Math.Ceiling(sourceFrames/rate);if(length>int.MaxValue)throw new ArgumentException("Stretched audio exceeds the sample-count limit.");
            OutputFrames=(int)length;hop=Math.Max(128,Math.Min(2048,(int)(frequency*.020)));
            radius=Math.Max(32,(int)(frequency*.008));coarseStep=Math.Max(1,frequency/6000);stride=Math.Max(1,frequency/11025);
            previous=new float[hop*2*channels];current=new float[previous.Length];output=new float[hop*channels];
            weights=new double[hop];for(int i=0;i<hop;i++)weights[i]=.5-.5*Math.Cos(Math.PI*(i+.5)/hop);
            ResetOutputPosition(0);
        }
        public void ResetOutputPosition(int frame)
        {
            outputPosition=Math.Max(0,Math.Min(OutputFrames,frame));startSource=outputPosition*rate;
            outputOffset=hop;grain=0;first=true;
        }
        private float Sample(int frame,int channel){return frame>=0&&frame<sourceFrames?source[frame*channels+channel]:0;}
        private double Score(int frame,int nominal,int sampleStride)
        {
            double dot=0,a=0,b=0;
            for(int i=0;i<hop;i+=sampleStride)for(int ch=0;ch<channels;ch++)
            {double x=previous[(hop+i)*channels+ch],y=Sample(frame+i,ch);dot+=x*y;a+=x*x;b+=y*y;}
            return dot/Math.Sqrt(a*b+1e-20)-Math.Abs(frame-nominal)*1e-6/radius;
        }
        private void Generate()
        {
            int nominal=(int)Math.Min(sourceFrames,Math.Round(startSource+grain*hop*rate)),position=nominal;
            if(!first)
            {
                int low=Math.Max(0,nominal-radius),high=Math.Min(sourceFrames,nominal+radius),best=nominal;
                double score=Score(best,nominal,stride);
                for(int candidate=low;candidate<=high;candidate+=coarseStep)
                {double value=Score(candidate,nominal,stride);if(value>score){score=value;best=candidate;}}
                int coarse=best;score=Score(best,nominal,1);
                for(int candidate=Math.Max(low,coarse-coarseStep);candidate<=Math.Min(high,coarse+coarseStep);candidate++)
                {double value=Score(candidate,nominal,1);if(value>score){score=value;best=candidate;}}
                position=best;
            }
            for(int i=0;i<hop*2;i++)for(int ch=0;ch<channels;ch++)current[i*channels+ch]=Sample(position+i,ch);
            for(int i=0;i<hop;i++)for(int ch=0;ch<channels;ch++)
            {
                int index=i*channels+ch;
                output[index]=first?current[index]:(float)(previous[(hop+i)*channels+ch]*(1-weights[i])+current[index]*weights[i]);
            }
            Array.Copy(current,previous,current.Length);first=false;grain++;outputOffset=0;
        }
        public void Read(float[] destination)
        {
            if(destination==null||destination.Length%channels!=0)throw new ArgumentException("Complete interleaved frames required.");
            for(int i=0;i<destination.Length/channels;i++)
            {
                if(outputPosition>=OutputFrames){Array.Clear(destination,i*channels,destination.Length-i*channels);break;}
                if(outputOffset>=hop)Generate();
                Array.Copy(output,outputOffset*channels,destination,i*channels,channels);outputOffset++;outputPosition++;
            }
        }
    }
}
