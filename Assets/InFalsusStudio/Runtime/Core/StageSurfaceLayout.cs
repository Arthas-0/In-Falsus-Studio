using System;
using System.Collections.Generic;

namespace InFalsusStudio.Core
{
    public enum StagePanelKind { Lane, Gutter, Apron }

    /// <summary>One disjoint X/Z rectangle of the stage surface. Lane -1 denotes a gutter.
    /// No rectangle lies underneath a lane. Elevation is supplied by StageSpace.Surface.</summary>
    public struct StageSurfacePanel
    {
        public StagePanelKind Kind;
        public int Lane;
        public double Left, Right, Near, Far;
        public double Area { get { return (Right - Left) * (Far - Near); } }
    }

    public static class StageSurfaceLayout
    {
        public const double CentralInset = .025;
        public const double TileLength = 1.6;
        public const double ApronLength = .3;

        /// <summary>Partition the visible six-lane surface instead of stacking a dark
        /// underlay, coplanar lane quads, and almost coplanar decorative triangles.</summary>
        public static List<StageSurfacePanel> Build(double halfWidth, double sideRun, double near, double far)
        {
            if (!Finite(halfWidth) || !Finite(sideRun) || !Finite(near) || !Finite(far) ||
                halfWidth <= 0 || sideRun <= 0 || far <= near)
                throw new ArgumentOutOfRangeException("Invalid stage dimensions.");
            int rows = (int)Math.Ceiling((far - near) / TileLength);
            if (rows < 1 || rows > 4096)
                throw new ArgumentOutOfRangeException("Stage surface exceeds the tessellation budget.");
            double unit = halfWidth * .5, inset = unit * CentralInset;
            var panels = new List<StageSurfacePanel>(rows * 6 + 6);
            panels.Add(Panel(StagePanelKind.Apron, -1, -halfWidth, halfWidth, near - ApronLength, near));
            // Five non-overlapping black lane dividers; NOT a full-floor black sheet.
            for (int boundary = 0; boundary <= 4; boundary++)
            {
                double x = -halfWidth + boundary * unit;
                double left = boundary == 0 ? x : x - inset;
                double right = boundary == 4 ? x : x + inset;
                panels.Add(Panel(StagePanelKind.Gutter, -1, left, right, near, far));
            }
            for (int lane = 0; lane < 6; lane++)
            {
                double left, right;
                if (lane == 0) { left = -halfWidth - sideRun; right = -halfWidth; }
                else if (lane == 5) { left = halfWidth; right = halfWidth + sideRun; }
                else { left = -halfWidth + (lane - 1) * unit + inset; right = -halfWidth + lane * unit - inset; }
                for (int row = 0; row < rows; row++)
                {
                    double z0 = near + row * TileLength;
                    double z1 = Math.Min(far, near + (row + 1) * TileLength);
                    panels.Add(Panel(StagePanelKind.Lane, lane, left, right, z0, z1));
                }
            }
            return panels;
        }
        private static StageSurfacePanel Panel(StagePanelKind kind, int lane, double left, double right, double near, double far)
        { return new StageSurfacePanel { Kind = kind, Lane = lane, Left = left, Right = right, Near = near, Far = far }; }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
