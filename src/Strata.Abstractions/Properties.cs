namespace Strata;

/// <summary>
/// Describes a single property name in the cascade: its value type, parser, initial value,
/// and whether it inherits to descendants when no local declaration matches.
/// </summary>
public interface IPropertyDescriptor
{
    /// <summary>Property name. Case-sensitive. Lowercase by convention.</summary>
    string Name { get; }

    /// <summary>The CLR type of values produced by <see cref="Parse"/>.</summary>
    Type ValueType { get; }

    /// <summary>
    /// Whether this property cascades to descendants when no local declaration is present.
    /// </summary>
    bool Inherits { get; }

    /// <summary>The initial (default) value when no rule declares this property.</summary>
    IPropertyValue Initial { get; }

    /// <summary>Parse a textual value into the typed representation.</summary>
    IPropertyValue Parse(ReadOnlySpan<char> source);
}

/// <summary>
/// A typed property value. Implementations MUST be value types or interned reference types;
/// allocating per-cascade is forbidden for built-in property types (NFR-3).
/// </summary>
public interface IPropertyValue
{
    /// <summary>The CLR type of the underlying value.</summary>
    Type Type { get; }
}

/// <summary>
/// Registry of property descriptors keyed by property name. Selector languages and projections
/// consult the registry to parse and validate declarations.
/// </summary>
public interface IPropertyRegistry
{
    /// <summary>Register a property descriptor. Duplicate names overwrite; callers warn first.</summary>
    void Register(IPropertyDescriptor descriptor);

    /// <summary>Look up a descriptor by property name.</summary>
    bool TryGet(string name, out IPropertyDescriptor descriptor);

    /// <summary>All registered descriptors.</summary>
    IEnumerable<IPropertyDescriptor> All { get; }
}
