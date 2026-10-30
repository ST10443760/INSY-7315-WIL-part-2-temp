using System.Globalization;
using System.Text;
using ThriveWellness.Models;

namespace ThriveWellness.Services
{
    // Lays out the revenue line graph as SVG coordinates within a viewBox of
    // the given size: the line and the area under it, a marker per point,
    // horizontal gridlines on "nice" rand ticks (ChartScale), and x-axis
    // labels thinned to at most maxXLabels. Pure, so the view just draws
    // what's here.
    public static class RevenueChart
    {
        private const int Left = 64, Right = 16, Top = 16, Bottom = 32;

        public record Point(double X, double Y, string LongLabel, decimal Amount);
        public record Tick(double Y, string Label);
        public record XLabel(double X, string Label);

        public class Geometry
        {
            public int Width { get; init; }
            public int Height { get; init; }
            public IReadOnlyList<Point> Points { get; init; } = Array.Empty<Point>();

            // Empty for a single point: just its marker is drawn.
            public string LinePath { get; init; } = string.Empty;
            public string AreaPath { get; init; } = string.Empty;
            public IReadOnlyList<Tick> YTicks { get; init; } = Array.Empty<Tick>();
            public IReadOnlyList<XLabel> XLabels { get; init; } = Array.Empty<XLabel>();
            public double ZeroY { get; init; }
            public double PlotLeft => Left;
            public double PlotRight => Width - Right;
        }

        public static Geometry Build(IReadOnlyList<RevenuePoint> series, int width, int height, int maxXLabels)
        {
            if (series.Count == 0)
            {
                return new Geometry { Width = width, Height = height };
            }

            var scale = ChartScale.For(series.Min(p => p.Amount), series.Max(p => p.Amount));
            var plotWidth = width - Left - Right;
            var plotHeight = height - Top - Bottom;

            // Coordinates are only drawing positions, so double is fine
            // here; every amount stays a decimal.
            double YOf(decimal amount) =>
                Top + (double)((scale.Max - amount) / (scale.Max - scale.Min)) * plotHeight;
            double XOf(int index) =>
                series.Count == 1 ? Left + plotWidth / 2.0 : Left + plotWidth * index / (double)(series.Count - 1);

            var points = series.Select((p, i) => new Point(XOf(i), YOf(p.Amount), p.LongLabel, p.Amount)).ToList();
            var zeroY = YOf(0m);

            string line = string.Empty, area = string.Empty;
            if (points.Count > 1)
            {
                var path = new StringBuilder();
                for (var i = 0; i < points.Count; i++)
                {
                    path.Append(i == 0 ? "M" : " L").Append(Num(points[i].X)).Append(' ').Append(Num(points[i].Y));
                }
                line = path.ToString();
                area = $"{line} L{Num(points[^1].X)} {Num(zeroY)} L{Num(points[0].X)} {Num(zeroY)} Z";
            }

            var every = Math.Max(1, (int)Math.Ceiling(series.Count / (double)Math.Max(1, maxXLabels)));
            var xLabels = series
                .Select((p, i) => (p, i))
                .Where(x => x.i % every == 0)
                .Select(x => new XLabel(XOf(x.i), x.p.Label))
                .ToList();

            return new Geometry
            {
                Width = width,
                Height = height,
                Points = points,
                LinePath = line,
                AreaPath = area,
                YTicks = scale.Ticks.Select(t => new Tick(YOf(t), AxisLabel(t))).ToList(),
                XLabels = xLabels,
                ZeroY = zeroY
            };
        }

        // "R2 000" on the axis - whole rands unless the step needs cents.
        public static string AxisLabel(decimal amount)
        {
            var formatted = Money.Format(amount);
            return amount == decimal.Truncate(amount) ? formatted[..^3] : formatted;
        }

        // SVG coordinates, always with a full stop.
        public static string Num(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
