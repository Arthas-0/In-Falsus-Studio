using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    /// <summary>Shared head policy: geometric joins for automatic SkyAreas,
    /// first chronological source for explicit groups.</summary>
    public sealed class SkyConnections
    {
        public readonly HashSet<int> Starts;
        public const double TimeTolerance=SkyGroups.TimeTolerance,SpaceTolerance=SkyGroups.SpaceTolerance;
        public SkyConnections(IEnumerable<SpcEvent> source){Starts=SkyGroups.Heads(source);}
    }
    public static class AudioVoiceBudget
    {
        public const int MusicPriority=0,HoldPriority=48,ShotPriority=160;
        // Reserve the music and both loop beds, plus headroom for the Editor.
        public static int Shots(int realVoices){return Math.Max(0,Math.Min(20,realVoices-7));}
        public static int Holds(int realVoices){return realVoices>=7?4:Math.Max(0,realVoices-2);}
    }
}
