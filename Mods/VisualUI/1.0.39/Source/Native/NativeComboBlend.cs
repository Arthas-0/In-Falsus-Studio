using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
namespace InFalsusStudio.NativeVisual {
// Native FastText blends in Gamma (original PlayerSettings=0). The editor
// renders in Linear. Capture the stage before notes, reproduce that Gamma
// blend locally and convert only the numeral pixels back to the scene space.
internal static class NativeComboBlend {
 static AssetBundle bundle;
 internal static Shader Shader;
 static Camera view;
 static Material comboMaterial;
 static bool wanted,subscribed;
 static CopyStagePass pass;
 internal static void Init(string presentationFolder){
  string path=Path.Combine(Path.GetDirectoryName(presentationFolder),"Assets","combo-gamma.bundle");
  bundle=AssetBundle.LoadFromFile(path);if(!bundle)throw new Exception("Native combo color compatibility bundle missing");
  Shader=bundle.LoadAsset<Shader>("native/compatibility/combo");if(!Shader||!Shader.isSupported)throw new Exception("Native combo Gamma blend shader unsupported");
  pass=new CopyStagePass();RenderPipelineManager.beginCameraRendering+=Begin;subscribed=true;
 }
 internal static void View(Camera camera){view=camera;}
 internal static void Bind(Material material){comboMaterial=material;}
 internal static void Request(bool active){wanted=active;}
 static void Begin(ScriptableRenderContext context,Camera camera){
  if(!wanted||camera!=view||!NativeVisualPlugin.Active||!NativeVisualPlugin.ComboEnabled.Value)return;
  camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
 }
 sealed class CopyStagePass:ScriptableRenderPass {
  sealed class CopyData {internal TextureHandle source;}
  RenderTexture texture;
  RTHandle handle;
  RTHandle source;
  internal CopyStagePass(){renderPassEvent=RenderPassEvent.BeforeRenderingTransparents;ConfigureInput(ScriptableRenderPassInput.Color);}
  public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData){
   var resources=frameData.Get<UniversalResourceData>();var input=resources.activeColorTexture;var desc=graph.GetTextureDesc(input);desc.name="Native combo stage color";desc.clearBuffer=false;desc.msaaSamples=MSAASamples.None;
   var output=graph.CreateTexture(desc);
   if(comboMaterial)comboMaterial.SetVector("_NativeComboViewport",new Vector4(desc.width,desc.height,QualitySettings.activeColorSpace==ColorSpace.Linear?1:0,0));
   CopyData data;
   using(var builder=graph.AddRasterRenderPass<CopyData>("Native combo Gamma background",out data)){
    data.source=input;builder.UseTexture(input,AccessFlags.Read);builder.SetRenderAttachment(output,0);builder.SetGlobalTextureAfterPass(output,UnityEngine.Shader.PropertyToID("_NativeComboBackground"));builder.AllowPassCulling(false);
    builder.SetRenderFunc<CopyData>(delegate(CopyData d,RasterGraphContext context){Blitter.BlitTexture(context.cmd,d.source,new Vector4(1,1,0,0),0,false);});
   }
  }
  public override void OnCameraSetup(CommandBuffer cmd,ref RenderingData data){
   source=view.GetUniversalAdditionalCameraData().scriptableRenderer.cameraColorTargetHandle;
   if(source==null||!source.rt)throw new Exception("Combo color capture requires the camera intermediate target");
   var desc=source.rt.descriptor;desc.depthBufferBits=0;desc.msaaSamples=1;desc.bindMS=false;desc.useMipMap=false;desc.autoGenerateMips=false;
   if(texture&&(texture.width!=desc.width||texture.height!=desc.height||texture.graphicsFormat!=desc.graphicsFormat)){Release();}
   if(!texture){texture=new RenderTexture(desc);texture.name="Native combo background before notes";texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;if(!texture.Create())throw new Exception("Combo background allocation failed");handle=RTHandles.Alloc(texture);}
   if(comboMaterial)comboMaterial.SetVector("_NativeComboViewport",new Vector4(texture.width,texture.height,QualitySettings.activeColorSpace==ColorSpace.Linear?1:0,0));
  }
  public override void Execute(ScriptableRenderContext context,ref RenderingData data){
   if(!texture||handle==null)return;
   var cmd=CommandBufferPool.Get("Native combo Gamma background");
   try{Blitter.BlitCameraTexture(cmd,source,handle);cmd.SetGlobalTexture("_NativeComboBackground",handle.nameID);context.ExecuteCommandBuffer(cmd);}finally{CommandBufferPool.Release(cmd);}
  }
  internal void Release(){if(handle!=null){handle.Release();handle=null;}if(texture){texture.Release();UnityEngine.Object.Destroy(texture);texture=null;}}
 }
 internal static void Dispose(){if(subscribed){RenderPipelineManager.beginCameraRendering-=Begin;subscribed=false;}wanted=false;view=null;if(pass!=null){pass.Release();pass=null;}Shader=null;if(bundle){bundle.Unload(false);bundle=null;}}
}
}
