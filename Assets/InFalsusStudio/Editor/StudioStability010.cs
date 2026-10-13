#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using InFalsusStudio.Core;
namespace InFalsusStudio.EditorTools
{
    public static class StudioStability010
    {
        [MenuItem("Tools/InFalsus Studio/Run v0.10 Stability Checks")]
        public static void Run()
        {
            int count=0;
            Release010SelfTests.Run((ok,name)=>{count++;if(!ok)throw new InvalidOperationException("FAILED: "+name);});
            Debug.Log("v0.10 ACTUAL C# stability checks passed: "+count+". No rendering/audio-device certification implied.");
        }
        [MenuItem("Tools/InFalsus Studio/Inspect v0.10 Runtime Audio and UI")]
        public static void Inspect()
        {
            if(!EditorApplication.isPlaying){Debug.LogWarning("Enter Play mode first.");return;}
            var studio=UnityEngine.Object.FindFirstObjectByType<StudioBootstrap>();
            if(studio==null){Debug.LogError("StudioBootstrap missing in the running scene.");return;}
            Debug.Log(studio.RuntimeStabilityReport010());
        }
    }
}
#endif
