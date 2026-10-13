using System;
using System.Linq;

namespace InFalsusStudio.Core
{
    /// <summary>Pure C# tests; a provided test is not an assertion that Unity has run it.</summary>
    public static class Visual102SelfTests
    {
        public static void Run(Action<bool,string> check)
        {
            check(ReleaseIdentity.Version==ReleaseIdentity.DisplayVersion,"current patch display/build version consistent");
            var panels=StageSurfaceLayout.Build(2,.8252873,-1.5,65);
            check(panels.Count==258,"default stage has 252 lane tiles, five gutters, one apron");
            check(panels.Count(p=>p.Kind==StagePanelKind.Gutter)==5,"exactly five central gutters");
            check(panels.Count(p=>p.Kind==StagePanelKind.Apron)==1,"near apron exists only before stageNear");
            check(panels.All(p=>p.Area>0),"all stage rectangles have positive area");
            double target=(4+2*.8252873)*66.5+4*.3;
            check(Math.Abs(panels.Sum(p=>p.Area)-target)<1e-8,"partition equals the exact six-lane footprint");
            double overlaps=0;
            for(int i=0;i<panels.Count;i++)for(int j=i+1;j<panels.Count;j++)
            {
                var a=panels[i];var b=panels[j];
                overlaps+=Math.Max(0,Math.Min(a.Right,b.Right)-Math.Max(a.Left,b.Left))*
                    Math.Max(0,Math.Min(a.Far,b.Far)-Math.Max(a.Near,b.Near));
            }
            check(overlaps<1e-8,"no positive-area overlap between any opaque surface rectangles");
            for(int lane=0;lane<6;lane++)
            {
                var tiles=panels.Where(p=>p.Lane==lane).OrderBy(p=>p.Near).ToArray();
                check(tiles.Length==42,"lane "+lane+" tile count");
                check(Math.Abs(tiles[0].Near+1.5)<1e-10&&Math.Abs(tiles[41].Far-65)<1e-10,"lane "+lane+" retains near/far");
                check(Enumerable.Range(1,41).All(i=>Math.Abs(tiles[i].Near-tiles[i-1].Far)<1e-10),"lane "+lane+" has no longitudinal gap");
            }
            foreach(double x in new[]{-2.50,-1.823,-1.011,-.581,-.004,.013,.73,1.256,1.981,2.41})
            foreach(double z in new[]{-1.231,.871,17.239,32.831,64.513})
                check(panels.Count(p=>x>p.Left&&x<p.Right&&z>p.Near&&z<p.Far)==1,"one surface only at "+x+","+z);
            bool rejected=false;try{StageSurfaceLayout.Build(0,.8,-1,60);}catch(ArgumentOutOfRangeException){rejected=true;}
            check(rejected,"invalid stage width rejected");
            var scaled=StageSurfaceLayout.Build(3.4,.72,-2,27.75);
            check(Math.Abs(scaled.Where(p=>p.Lane==0).First().Right+3.4)<1e-10,"custom-width side seam unchanged");

            check(FloorComboLayout.Build(0,2).Count==0,"zero combo is hidden");
            check(FloorComboLayout.Build(-1,2).Count==0,"negative combo is hidden");
            foreach(int count in new[]{1,7,10,89,867,897,2169,100000,int.MaxValue})
            {
                var glyphs=FloorComboLayout.Build(count,2);
                check(string.Concat(glyphs.Select(g=>g.Digit.ToString(System.Globalization.CultureInfo.InvariantCulture)))==count.ToString(System.Globalization.CultureInfo.InvariantCulture),"exact digits for "+count);
                check(Math.Abs(glyphs.First().Left+glyphs.Last().Right)<1e-10,"combo horizontally centered: "+count);
                check(glyphs.All(g=>g.Left>=-1.56000001&&g.Right<=1.56000001),"long counts fit central floor: "+count);
                check(glyphs.All(g=>g.Height==.01&&g.Far>g.Near&&g.Right>g.Left),"flat X/Z glyphs: "+count);
                check(glyphs.All(g=>g.U0>=0&&g.U1<=1&&g.V0>=0&&g.V1<=1),"UV inside atlas: "+count);
            }
            var seven=FloorComboLayout.Build(7,2)[0];
            check(Math.Abs(seven.U0-.4)<1e-10&&seven.V0==0&&seven.V1==.5,"digit 7 is bottom atlas row column 2");
            var triple=FloorComboLayout.Build(897,2);
            check(Math.Abs(triple[0].Near-3.85)<1e-10&&Math.Abs(triple[0].Far-4.95)<1e-10,"reference floor numeral depth");
            var camera=CameraCalibration.Reference();
            var bottom=camera.Project(triple[0].Left,.01,triple[0].Near);
            var top=camera.Project(triple[0].Left,.01,triple[0].Far);
            check(top.Y<bottom.Y,"far glyph row is visually above near row");
            check(Math.Abs(top.X-1024)<Math.Abs(bottom.X-1024),"glyph perspective narrows toward distance");
            double normalized=camera.Project(0,.01,FloorComboLayout.CenterZ).Y/1152;
            check(normalized>.45&&normalized<.49,"numeral row is above sky judgement in reference view");
            check(Math.Abs(PreviewScore.At(1334,1334)-100001334)==0,"score kernel unchanged");
        }
    }
}
