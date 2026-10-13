#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio.EditorTools
{
    /// <summary>The user-supplied image is assigned automatically and rechecked for every
    /// Windows build, including the user's existing repaired Build08 entry.</summary>
    [InitializeOnLoad]
    public static class StudioBranding101
    {
        public const string IconPath = "Assets/InFalsusStudio/Branding/InfalsusStudioAppIcon.png";
        static StudioBranding101() { EditorApplication.delayCall += ApplyAfterImport; }
        private static void ApplyAfterImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += ApplyAfterImport; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath) == null) return;
            try { EnsureConfigured(); }
            catch (Exception ex) { Debug.LogWarning("Infalsus Studio icon setup: " + ex.Message); }
        }
        [MenuItem("Tools/InFalsus Studio/Apply Bundled Infalsus Studio Icon")]
        public static void EnsureConfigured()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null || icon.width != icon.height || icon.width < 256)
                throw new BuildFailedException("The bundled square application icon is missing or not imported: " + IconPath);
            var target = NamedBuildTarget.Standalone;
            int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Application);
            if (sizes == null || sizes.Length == 0)
                throw new BuildFailedException("Unity reported no Standalone application-icon slots.");
            Texture2D[] old = PlayerSettings.GetIcons(target, IconKind.Application);
            if (old != null && old.Length == sizes.Length && old.All(x => x == icon)) return;
            PlayerSettings.SetIcons(target, Enumerable.Repeat(icon, sizes.Length).ToArray(), IconKind.Application);
            AssetDatabase.SaveAssets();
            Debug.Log("Infalsus Studio: supplied application icon assigned to " + sizes.Length + " Standalone slots. Rebuild to update the exe.");
        }
    }
    public sealed class StudioBrandingBuildCheck101 : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return -1000; } }
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.StandaloneWindows64 || report.summary.platform == BuildTarget.StandaloneWindows)
                StudioBranding101.EnsureConfigured();
        }
    }
}
#endif
