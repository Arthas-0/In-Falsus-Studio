using System;
using System.Collections.Generic;
using System.Globalization;

namespace InFalsusStudio.Core
{
    public struct FloorComboGlyph
    {
        public int Digit;
        public double Left, Right, Near, Far, Height;
        public double U0, U1, V0, V1;
    }

    /// <summary>Numeral-only world-plane layout. Counts come from the existing timeline;
    /// this class does not calculate judgement, score, or animation state.</summary>
    public static class FloorComboLayout
    {
        public const double CenterZ = 4.4;
        public const double DigitWidth = .78;
        public const double DigitDepth = 1.10;
        // Quads overlap ONLY in the empty atlas margins, giving normal digit spacing.
        public const double Gap = -.10;
        public const double FloorLift = .010;
        public const int AtlasColumns = 5;
        public const int AtlasRows = 2;
        public const int CellWidth = 256;
        public const int CellHeight = 320;

        public static List<FloorComboGlyph> Build(int count, double centralHalfWidth)
        {
            if (double.IsNaN(centralHalfWidth) || double.IsInfinity(centralHalfWidth) || centralHalfWidth <= 0)
                throw new ArgumentOutOfRangeException("centralHalfWidth");
            var result = new List<FloorComboGlyph>();
            if (count <= 0) return result;
            string digits = count.ToString(CultureInfo.InvariantCulture);
            double scale = centralHalfWidth / 2.0;
            double naturalWidth = digits.Length * DigitWidth + (digits.Length - 1) * Gap;
            double fit = Math.Min(1, 3.12 / naturalWidth);
            double width = DigitWidth * scale * fit, gap = Gap * scale * fit;
            double depth = DigitDepth * scale * fit;
            double total = (digits.Length * width) + ((digits.Length - 1) * gap);
            double near = CenterZ - depth * .5, far = CenterZ + depth * .5;
            for (int i = 0; i < digits.Length; i++)
            {
                int digit = digits[i] - '0', column = digit % AtlasColumns, row = digit / AtlasColumns;
                double x = -total * .5 + i * (width + gap);
                result.Add(new FloorComboGlyph {
                    Digit = digit, Left = x, Right = x + width, Near = near, Far = far, Height = FloorLift * scale,
                    U0 = (double)column / AtlasColumns, U1 = (double)(column + 1) / AtlasColumns,
                    // PNG row zero is at the top; Unity UV zero is at the bottom.
                    V0 = 1.0 - (double)(row + 1) / AtlasRows, V1 = 1.0 - (double)row / AtlasRows
                });
            }
            return result;
        }
    }
}
