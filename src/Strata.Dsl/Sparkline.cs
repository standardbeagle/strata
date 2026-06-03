using System.Globalization;
using System.Text;

namespace Strata.Dsl;

/// <summary>
/// Renders a numeric series as a single-line block-character sparkline, scaled between the
/// series minimum and maximum. Used by the <c>Graph</c> widget.
/// </summary>
public static class Sparkline
{
    private static readonly char[] Bars = { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

    /// <summary>Render <paramref name="values"/> as a sparkline string; empty input yields "".</summary>
    public static string Render(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            return string.Empty;
        }

        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var v in values)
        {
            if (v < min)
            {
                min = v;
            }

            if (v > max)
            {
                max = v;
            }
        }

        var range = max - min;
        var builder = new StringBuilder(values.Count);
        foreach (var v in values)
        {
            // Flat series (range 0) renders as the lowest bar.
            var index = range <= 0 ? 0 : (int)Math.Round((v - min) / range * (Bars.Length - 1));
            index = Math.Clamp(index, 0, Bars.Length - 1);
            builder.Append(Bars[index]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Coerce a heterogeneous attribute value (double[], object[] of numbers, JSON array) into a
    /// <see cref="double"/> list for rendering. Non-numeric entries are skipped.
    /// </summary>
    public static IReadOnlyList<double> Coerce(object? value)
    {
        switch (value)
        {
            case null:
                return Array.Empty<double>();
            case IReadOnlyList<double> doubles:
                return doubles;
            case IEnumerable<double> seq:
                return seq.ToList();
            case System.Collections.IEnumerable items:
                var result = new List<double>();
                foreach (var item in items)
                {
                    if (item is not null && TryToDouble(item, out var d))
                    {
                        result.Add(d);
                    }
                }
                return result;
            default:
                return Array.Empty<double>();
        }
    }

    private static bool TryToDouble(object item, out double value)
    {
        switch (item)
        {
            case double d: value = d; return true;
            case int i: value = i; return true;
            case long l: value = l; return true;
            case float f: value = f; return true;
            case decimal m: value = (double)m; return true;
            default:
                return double.TryParse(
                    item.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
    }
}
