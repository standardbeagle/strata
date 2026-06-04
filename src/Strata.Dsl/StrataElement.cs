using Strata.Interaction;

namespace Strata.Dsl;

/// <summary>
/// A mutable Strata element built by the PowerShell DSL. Implements <see cref="ITreeNode"/>
/// with reference identity so it stays stable across cascade runs, and exposes mutable class,
/// pseudo-state, and child collections so later sub-projects (focus, live state) can mutate it
/// in place.
/// </summary>
public sealed class StrataElement : ITreeNode, IPseudoStateMutable
{
    private readonly List<StrataElement> _children = new();
    private readonly HashSet<string> _classes;
    private HashSet<string>? _boundClasses;
    private IReadOnlySet<string> _effectiveClasses;
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
        _effectiveClasses = _classes;
        _attributes = attributes is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(attributes, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public string? Id { get; }

    /// <inheritdoc />
    public IReadOnlySet<string> Classes => _effectiveClasses;

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

    /// <summary>Set or replace an attribute in place. Data binding writes resolved values here.</summary>
    public void SetAttribute(string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        _attributes[name] = value;
    }

    /// <summary>
    /// Replace the data-bound class set (from a <c>bind-class</c> binding) while preserving the
    /// element's static classes. Re-cascading after this applies the matching <c>.class</c> rules,
    /// so a store value can drive styling (e.g. <c>.up</c> / <c>.down</c>).
    /// </summary>
    public void SetBoundClasses(IEnumerable<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        _boundClasses ??= new HashSet<string>(StringComparer.Ordinal);
        _boundClasses.Clear();
        foreach (var token in tokens)
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                _boundClasses.Add(token);
            }
        }

        if (_boundClasses.Count == 0)
        {
            _effectiveClasses = _classes;
            return;
        }

        var combined = new HashSet<string>(_classes, StringComparer.Ordinal);
        combined.UnionWith(_boundClasses);
        _effectiveClasses = combined;
    }

    /// <inheritdoc />
    public bool AddPseudoState(string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return _pseudoStates.Add(state);
    }

    /// <inheritdoc />
    public bool RemovePseudoState(string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return _pseudoStates.Remove(state);
    }
}
