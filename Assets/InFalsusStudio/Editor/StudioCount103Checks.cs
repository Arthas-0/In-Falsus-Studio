#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio.EditorTools
{
    public static class StudioCount103Checks
    {
        [MenuItem("Tools/InFalsus Studio/Run 1.0.3 Count and Import Checks")]
        public static void Run()
        {
            int count=0;Action<bool,string> check=(ok,name)=>{count++;if(!ok)throw new InvalidOperationException("FAILED: "+name);};
            Release103SelfTests.Run(check);
            Release103SelfTests.RunPairs(Path.Combine(Application.dataPath,"InFalsusStudio/Editor/CountRegression103"),check);
            Debug.Log("1.0.3 actual C# count/import checks passed: "+count+". Candidate counts are not game-verified totals. Unity rendering/audio remain separate tests.");
        }
        [MenuItem("Tools/InFalsus Studio/Inspect 1.0.3 Loaded Count")]
        public static void Inspect()
        {
            var app=UnityEngine.Object.FindFirstObjectByType<StudioBootstrap>();
            if(!EditorApplication.isPlaying||app==null||app.History==null){Debug.LogWarning("Enter Play and open a chart first.");return;}
            var doc=app.History.Document;var events=doc.ReadEvents();var count=TickCountResearch.Analyze(events);var timeline=new ProvisionalComboTimeline(events);
            Debug.Log("1.0.3 COUNT: supported="+count.Supported+" | recovered legacy BPM="+doc.RecoveredIcpTimingCount+
                " | BPM="+events.Count(e=>e.Kind==EventKind.Bpm)+" | track="+events.Count(e=>e.Kind==EventKind.Track)+
                " | objects="+events.Count(e=>e.IsNote)+" | candidate="+count.Total+" | now="+timeline.At(app.CurrentMs).Total+"\n"+count.Warning);
        }
    }
}
#endif
