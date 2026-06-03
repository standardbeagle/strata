namespace Strata.Dsl;

/// <summary>
/// A mutable Strata element built by the PowerShell DSL. Implements <see cref="ITreeNode"/>
/// with reference identity so it stays stable across cascade runs, and exposes mutable class,
/// pseudo-state, and child collections so later sub-projects (focus, live state) can mutate it
/// in place.
/// </summary>
public sealed class StrataElement : ITreeNode
{
    private readonly List<StrataElement> _children = new();
    private readonly HashSet<string> _classes;
    private readonly HashSet<string> _pseudoStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _attributes;

    /// <summary>Create an element with a kind and optional id, classes, and attributes.</summary>
    public StrataElement(
        string kind,
        string? id = null,
        IEnumerable<string>? classes = null,
        IDictionary<string, object?>? attributes = null)
    {
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Id = id;
        _classes = classes is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(
                classes.Where(c => !string.IsNullOrWhiteSpace(c)),
                StringComparer.Ordinal);
        _attributes = attributes is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(attributes, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public string? Id { get; }

    /// <inheritdoc />
    public IReadOnlySet<string> Classes => _classes;

    /// <inheritdoc />
    public IReadOnlySet<string> PseudoStates => _pseudoStates;

    /// <inheritdoc />
    public ITreeNode? Parent { get; private set; }

    /// <inheritdoc />
    public IEnumerable<ITreeNode> Children => _children;

    /// <inheritdoc />
    public object? Underlying => null;

    /// <inheritdoc />
    public bool TryGetAttribute(string name, out object? value)
        => _attributes.TryGetValue(name, out value);

    /// <summary>Append <paramref name="child"/> and set its parent to this element.</summary>
    /// <returns>This element, to allow fluent chaining.</returns>
    public StrataElement Add(StrataElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        child.Parent = this;
        _children.Add(child);
        return this;
    }
}
