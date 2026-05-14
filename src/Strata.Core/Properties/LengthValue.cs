using System.Globalization;

namespace Strata.Core.Properties;

/// <summary>Discriminator for the unit of a <see cref="LengthValue"/>.</summary>
public enum LengthUnit
{
    /// <summary>Auto-sized.</summary>
    Auto,

    /// <summary>Integer terminal cells.</summary>
    Cells,

    /// <summary>Percentage of available space (0..100).</summary>
    Percent,

    /// <summary>Flex/grid fraction unit.</summary>
    Fr,
}

/// <summary>
/// A length value in terminal cells, percentage, fr-units, or <c>auto</c>.
/// </summary>
public readonly record struct LengthValue(double Value, LengthUnit Unit) : IPropertyValue
{
    /// <summary>Auto-sized length.</summary>
    public static LengthValue Auto { get; } = new(0, LengthUnit.Auto);

    /// <inheritdoc/>
    public Type Type => typeof(LengthValue);
}

/// <summary>Descriptor for length-valued properties.</summary>
public sealed class LengthPropertyDescriptor : IPropertyDescriptor
{
    /// <summary>Create a length descriptor.</summary>
    public LengthPropertyDescriptor(string name, LengthValue initial, bool inherits)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Initial = initial;
        Inherits = inherits;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public Type ValueType => typeof(LengthValue);

    /// <inheritdoc/>
    public bool Inherits { get; }

    /// <inheritdoc/>
    public IPropertyValue Initial { get; }

    /// <inheritdoc/>
    public IPropertyValue Parse(ReadOnlySpan<char> source)
    {
        var t = source.Trim();
        if (t.SequenceEqual("auto"))
        {
            return LengthValue.Auto;
        }

        // Recognized suffixes: %, fr. Otherwise plain cells.
        if (t.Length > 0 && t[^1] == '%')
        {
            var num = t[..^1].Trim();
            return new LengthValue(ParseNumber(num, Name), LengthUnit.Percent);
        }

        if (t.Length > 2 && t.EndsWith("fr", StringComparison.OrdinalIgnoreCase))
        {
            var num = t[..^2].Trim();
            return new LengthValue(ParseNumber(num, Name), LengthUnit.Fr);
        }

        return new LengthValue(ParseNumber(t, Name), LengthUnit.Cells);
    }

    private static double ParseNumber(ReadOnlySpan<char> s, string property)
    {
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            throw new FormatException($"'{s}' is not a valid length for property '{property}'.");
        }

        return v;
    }
}
