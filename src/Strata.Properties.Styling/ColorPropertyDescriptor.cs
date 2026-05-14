namespace Strata.Properties.Styling;

/// <summary>Descriptor for a color-valued property.</summary>
public sealed class ColorPropertyDescriptor : IPropertyDescriptor
{
    /// <summary>Create a color descriptor.</summary>
    public ColorPropertyDescriptor(string name, ColorValue initial, bool inherits)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Initial = initial;
        Inherits = inherits;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public Type ValueType => typeof(ColorValue);

    /// <inheritdoc/>
    public bool Inherits { get; }

    /// <inheritdoc/>
    public IPropertyValue Initial { get; }

    /// <inheritdoc/>
    public IPropertyValue Parse(ReadOnlySpan<char> source) => ColorValue.Parse(source);
}
