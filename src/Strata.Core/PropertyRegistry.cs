namespace Strata.Core;

/// <summary>
/// Thread-unsafe (single-threaded per cascade per NFR-6) registry of property descriptors,
/// keyed by case-sensitive property name. Later <see cref="Register"/> calls overwrite earlier
/// ones — callers are responsible for warning on conflicts.
/// </summary>
public sealed class PropertyRegistry : IPropertyRegistry
{
    private readonly Dictionary<string, IPropertyDescriptor> _byName = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void Register(IPropertyDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _byName[descriptor.Name] = descriptor;
    }

    /// <inheritdoc/>
    public bool TryGet(string name, out IPropertyDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _byName.TryGetValue(name, out descriptor!);
    }

    /// <inheritdoc/>
    public IEnumerable<IPropertyDescriptor> All => _byName.Values;
}
