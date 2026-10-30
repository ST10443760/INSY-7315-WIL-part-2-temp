namespace ThriveWellness.Services
{
    // "Nice" round axis ticks for a chart, for example 0, 2 000, 4 000 ...
    // rather than 0, 1 860, 3 720. Always includes zero, so a graph that
    // dips below zero shows where zero is. Pure decimal arithmetic, no
    // floating point.
    public static class ChartScale
    {
        public record Scale(decimal Min, decimal Max, decimal Step, IReadOnlyList<decimal> Ticks);

        public static Scale For(decimal low, decimal high, int targetIntervals = 5)
        {
            var min = Math.Min(0m, low);
            var max = Math.Max(0m, high);
            if (max == min)
            {
                // Everything is zero: show a small empty axis rather than
                // dividing by zero.
                max = min + 100m;
            }

            var step = NiceStep((max - min) / targetIntervals);
            var niceMin = Math.Floor(min / step) * step;
            var niceMax = Math.Ceiling(max / step) * step;

            var ticks = new List<decimal>();
            for (var tick = niceMin; tick <= niceMax; tick += step)
            {
                ticks.Add(tick);
            }

            return new Scale(niceMin, niceMax, step, ticks);
        }

        // The smallest of 1, 2, 5 or 10 (times a power of ten) that is at
        // least the rough step.
        public static decimal NiceStep(decimal rough)
        {
            if (rough <= 0)
            {
                return 1m;
            }

            var magnitude = 1m;
            while (magnitude * 10 <= rough)
            {
                magnitude *= 10;
            }
            while (magnitude > rough)
            {
                magnitude /= 10;
            }

            var fraction = rough / magnitude;
            var nice = fraction <= 1 ? 1m : fraction <= 2 ? 2m : fraction <= 5 ? 5m : 10m;
            return nice * magnitude;
        }
    }
}
