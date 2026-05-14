namespace Strata.Core.Properties;

/// <summary>A plain string property value.</summary>
public readonly record struct StringValue(string Text) : IPropertyValue
{
    /// <inheritdoc/>
    public Type Type => typeof(string);
}

/// <summary>Descriptor for string-valued properties.</summary>
public sealed class StringPropertyDescriptor : IPropertyDescriptor
{
    /// <summary>Create a string property descriptor.</summary>
    public StringPropertyDescriptor(string name, string initial, bool inherits)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(initial);
        Name = name;
        Initial = new StringValue(initial);
        Inherits = inherits;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public Type ValueType => typeof(string);

    /// <inheritdoc/>
    public bool Inherits { get; }

    /// <inheritdoc/>
    public IPropertyValue Initial { get; }

    /// <inheritdoc/>
    public IPropertyValue Parse(ReadOnlySpan<char> source)
    {
        var trimmed = source.Trim();

        // Strip surrounding quotes if present.
        if (trimmed.Length >= 2)
        {
            var q = trimmed[0];
            if ((q == '"' || q == '\'') && trimmed[^1] == q)
            {
                return new StringValue(trimmed[1..^1].ToString());
            }
        }

        return new StringValue(trimmed.ToString());
    }
}
