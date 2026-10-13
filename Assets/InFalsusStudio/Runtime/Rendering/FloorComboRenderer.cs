using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    /// <summary>Faint outlined numeral atlas on the X/Z floor plane. Never a billboard
    /// or IMGUI label. No collider, shadow, input handling, or per-frame glyph allocation.</summary>
    public sealed class FloorComboRenderer : IDisposable
    {
        private readonly GameObject owner;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Material material;
        private int previousCount = -1;
        private float previousHalfWidth = -1;
        public const string ObjectName = "Floor combo - translucent numerals only";
        public const string AtlasResource = "InFalsusStudio/Hud/ComboDigits";
        public const string ShaderResource = "InFalsusStudio/FloorCombo";

        public FloorComboRenderer(Transform parent)
        {
            Shader shader = Resources.Load<Shader>(ShaderResource);
            Texture2D atlas = Resources.Load<Texture2D>(AtlasResource);
            if (shader == null || atlas == null)
                throw new InvalidOperationException("Missing floor combo shader or digit atlas. Import the complete 1.0.2 update.");
            material = new Material(shader) { name = "IF Floor combo / pale hollow numerals", renderQueue = 2940 };
            material.SetTexture("_MainTex", atlas);
            mesh = new Mesh { name = "IF Floor combo numeral quads" };
            mesh.MarkDynamic();
            owner = new GameObject(ObjectName, typeof(MeshFilter), typeof(MeshRenderer));
            owner.layer = 30;
            owner.transform.SetParent(parent, false);
            owner.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = owner.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
        }

        public void Draw(int count, bool visible, float centralHalfWidth)
        {
            renderer.enabled = visible && count > 0;
            if (!renderer.enabled) return;
            if (previousCount == count && previousHalfWidth == centralHalfWidth) return;
            var glyphs = FloorComboLayout.Build(count, centralHalfWidth);
            var vertices = new List<Vector3>(glyphs.Count * 4);
            var uvs = new List<Vector2>(glyphs.Count * 4);
            var triangles = new List<int>(glyphs.Count * 6);
            foreach (var g in glyphs)
            {
                int n = vertices.Count;
                vertices.Add(new Vector3((float)g.Left, (float)g.Height, (float)g.Near));
                vertices.Add(new Vector3((float)g.Right, (float)g.Height, (float)g.Near));
                vertices.Add(new Vector3((float)g.Right, (float)g.Height, (float)g.Far));
                vertices.Add(new Vector3((float)g.Left, (float)g.Height, (float)g.Far));
                uvs.Add(new Vector2((float)g.U0, (float)g.V0));
                uvs.Add(new Vector2((float)g.U1, (float)g.V0));
                uvs.Add(new Vector2((float)g.U1, (float)g.V1));
                uvs.Add(new Vector2((float)g.U0, (float)g.V1));
                triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 1);
                triangles.Add(n); triangles.Add(n + 3); triangles.Add(n + 2);
            }
            mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var bounds = mesh.bounds; bounds.Expand(new Vector3(.002f, .02f, .002f)); mesh.bounds = bounds;
            previousCount = count; previousHalfWidth = centralHalfWidth;
        }
        public void Dispose()
        {
            if (Application.isPlaying) { UnityEngine.Object.Destroy(owner); UnityEngine.Object.Destroy(mesh); UnityEngine.Object.Destroy(material); }
            else { UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(material); }
        }
    }
}
