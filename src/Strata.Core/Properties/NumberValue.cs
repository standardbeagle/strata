using System.Globalization;

namespace Strata.Core.Properties;

/// <summary>
/// A numeric property value. Backed by <see cref="double"/> to handle both integer and
/// fractional terminal-cell expressions (e.g. <c>1.5</c>, <c>10</c>).
/// </summary>
public readonly record struct NumberValue(double Value) : IPropertyValue
{
    /// <inheritdoc/>
    public Type Type => typeof(double);
}

/// <summary>Descriptor for numeric properties.</summary>
public sealed class NumberPropertyDescriptor : IPropertyDescriptor
{
    /// <summary>Create a numeric property descriptor.</summary>
    public NumberPropertyDescriptor(string name, double initial, bool inherits)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Initial = new NumberValue(initial);
        Inherits = inherits;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public Type ValueType => typeof(double);

    /// <inheritdoc/>
    public bool Inherits { get; }

    /// <inheritdoc/>
    public IPropertyValue Initial { get; }

    /// <inheritdoc/>
    public IPropertyValue Parse(ReadOnlySpan<char> source)
    {
        var trimmed = source.Trim();
        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            throw new FormatException($"'{trimmed}' is not a valid number for property '{Name}'.");
        }

        return new NumberValue(v);
    }
}
