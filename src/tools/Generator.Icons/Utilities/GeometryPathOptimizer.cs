using System.Globalization;
using System.Text.RegularExpressions;

namespace Generator.Icons.Utilities;

public static partial class GeometryPathOptimizer
{
    public static string Optimize(string geometryPath, int decimals = 2)
    {
        return NumberRegex().Replace(geometryPath, match =>
        {
            var value = double.Parse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture);

            var rounded = Math.Round(value, decimals);

            if (rounded == 0)
            {
                rounded = 0; // avoid "-0"
            }

            return rounded.ToString("0.##########", CultureInfo.InvariantCulture);
        });
    }

    [GeneratedRegex(@"-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")]
    internal static partial Regex NumberRegex();
}
