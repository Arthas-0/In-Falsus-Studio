using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 internal static class SkyMeshCache {
  internal static bool Enabled=true;
  internal static bool Active {get{var boot=TimingGroups.Boot;return Enabled&&boot!=null&&boot.History!=null&&boot.History.InTransaction&&GroupRuntime.Get(boot,"dragEvent")!=null&&!((StudioTransport)GroupRuntime.Get(boot,"transport")).Playing;}}
 }
}