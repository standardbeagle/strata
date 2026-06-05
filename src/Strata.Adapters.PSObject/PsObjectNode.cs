using System.Management.Automation;
using System.Runtime.CompilerServices;

namespace Strata.Adapters.PSObject;

/// <summary>
/// <see cref="ITreeNode"/> wrapping a PowerShell <see cref="System.Management.Automation.PSObject"/>.
/// </summary>
/// <remarks>
/// Identity: two <see cref="PsObjectNode"/> instances wrapping the same logical
/// <see cref="System.Management.Automation.PSObject"/> compare equal — the adapter caches
/// wrappers via <see cref="ConditionalWeakTable{TKey,TValue}"/>, so reference equality of
/// the wrapper is sufficient.
///
/// <para>Kind / Id / Classes / PseudoStates are resolved by the
/// <see cref="PsObjectTreeAdapter"/> using caller-configured selector delegates; the
/// node is the immutable cached result of those resolutions.</para>
/// </remarks>
public sealed class PsObjectNode : ITreeNode, IKindHierarchy
{
    internal PsObjectNode(
        global::System.Management.Automation.PSObject source,
        PsObjectTreeAdapter adapter,
        PsObjectNode? parent,
        string kind,
        IReadOnlyList<string> kindHierarchy,
        string? id,
        IReadOnlySet<string> classes,
        IReadOnlySet<string> pseudoStates)
    {
        Source = source;
        _adapter = adapter;
        Parent = parent;
        Kind = kind;
        KindHierarchy = kindHierarchy;
        Id = id;
        Classes = classes;
        PseudoStates = pseudoStates;
    }

    private readonly PsObjectTreeAdapter _adapter;

    /// <summary>The wrapped <see cref="System.Management.Automation.PSObject"/>.</summary>
    public global::System.Management.Automation.PSObject Source { get; }

    /// <inheritdoc/>
    public string Kind { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> KindHierarchy { get; }

    /// <inheritdoc/>
    public string? Id { get; }

    /// <inheritdoc/>
    public IReadOnlySet<string> Classes { get; }

    /// <inheritdoc/>
    public IReadOnlySet<string> PseudoStates { get; }

    /// <inheritdoc/>
    public ITreeNode? Parent { get; }

    /// <inheritdoc/>
    public IEnumerable<ITreeNode> Children => _adapter.EnumerateChildren(this);

    /// <inheritdoc/>
    public object? Underlying => Source.BaseObject;

    /// <inheritdoc/>
    public bool TryGetAttribute(string name, out object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        var prop = Source.Properties[name];
        if (prop is null)
        {
            value = null;
            return false;
        }

        value = prop.Value;
        return true;
    }
}

internal sealed class EmptyStringSet : IReadOnlySet<string>
{
    public static readonly EmptyStringSet Instance = new();

    private EmptyStringSet() { }

    public int Count => 0;

    public bool Contains(string item) => false;

    public IEnumerator<string> GetEnumerator() => System.Linq.Enumerable.Empty<string>().GetEnumerator();

    public bool IsProperSubsetOf(IEnumerable<string> other) => other?.Any() ?? false;

    public bool IsProperSupersetOf(IEnumerable<string> other) => false;

    public bool IsSubsetOf(IEnumerable<string> other) => true;

    public bool IsSupersetOf(IEnumerable<string> other) => !(other?.Any() ?? false);

    public bool Overlaps(IEnumerable<string> other) => false;

    public bool SetEquals(IEnumerable<string> other) => !(other?.Any() ?? false);

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
