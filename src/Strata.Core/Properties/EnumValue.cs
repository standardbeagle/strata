namespace Strata.Core.Properties;

/// <summary>An enum-string property value (e.g. <c>display: flex</c>, <c>display: grid</c>).</summary>
public readonly record struct EnumValue(string Value) : IPropertyValue
{
    /// <inheritdoc/>
    public Type Type => typeof(string);
}

/// <summary>
/// Descriptor for a property whose values are drawn from a fixed, case-insensitive set
/// of identifiers (e.g. <c>display: block | flex | grid | none</c>).
/// </summary>
public sealed class EnumPropertyDescriptor : IPropertyDescriptor
{
    private readonly HashSet<string> _allowed;

    /// <summary>Create an enum descriptor.</summary>
    public EnumPropertyDescriptor(
        string name,
        string initial,
        bool inherits,
        IEnumerable<string> allowedValues)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(allowedValues);
        Name = name;
        _allowed = new HashSet<string>(allowedValues, StringComparer.OrdinalIgnoreCase);
        if (!_allowed.Contains(initial))
        {
            throw new ArgumentException(
                $"Initial value '{initial}' is not in the allowed set for '{name}'.", nameof(initial));
        }

        Initial = new EnumValue(initial);
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
        var t = source.Trim().ToString();
        if (!_allowed.Contains(t))
        {
            throw new FormatException(
                $"'{t}' is not a valid value for '{Name}'. Allowed: {string.Join(", ", _allowed)}.");
        }

        return new EnumValue(t);
    }
}
