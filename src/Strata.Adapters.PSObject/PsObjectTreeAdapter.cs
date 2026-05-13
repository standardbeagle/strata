using System.Runtime.CompilerServices;

namespace Strata.Adapters.PSObject;

/// <summary>
/// Wraps PowerShell <see cref="System.Management.Automation.PSObject"/> instances as
/// <see cref="ITreeNode"/>s.
/// </summary>
/// <remarks>
/// The adapter caches wrappers per source <see cref="System.Management.Automation.PSObject"/>
/// via <see cref="ConditionalWeakTable{TKey,TValue}"/>, so identity semantics hold and the
/// cache does not prevent garbage collection of the underlying PSObject.
///
/// <para>Child enumeration is supplied by a caller-provided <see cref="ChildAccessor"/>
/// delegate. The default is no children (flat tree), suitable for pipelines like
/// <c>Get-Process | Format-Styled</c> where each PSObject is a leaf.</para>
/// </remarks>
public sealed class PsObjectTreeAdapter : ITreeAdapter<global::System.Management.Automation.PSObject>
{
    /// <summary>
    /// Returns the children of a node's underlying <see cref="System.Management.Automation.PSObject"/>.
    /// Implementations should yield each child as a <see cref="System.Management.Automation.PSObject"/>
    /// (callers may use <see cref="System.Management.Automation.PSObject.AsPSObject"/> to wrap
    /// non-PSObject values).
    /// </summary>
    public delegate IEnumerable<global::System.Management.Automation.PSObject> ChildAccessor(
        global::System.Management.Automation.PSObject parent);

    private readonly ConditionalWeakTable<global::System.Management.Automation.PSObject, PsObjectNode> _cache = new();
    private readonly ChildAccessor _childAccessor;

    /// <summary>Create an adapter with an explicit child-accessor.</summary>
    public PsObjectTreeAdapter(ChildAccessor? childAccessor = null)
    {
        _childAccessor = childAccessor ?? s_flatChildren;
    }

    private static readonly ChildAccessor s_flatChildren =
        _ => Array.Empty<global::System.Management.Automation.PSObject>();

    /// <inheritdoc/>
    public ITreeNode Wrap(global::System.Management.Automation.PSObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return WrapInternal(source, parent: null);
    }

    internal PsObjectNode WrapInternal(global::System.Management.Automation.PSObject source, PsObjectNode? parent)
    {
        if (_cache.TryGetValue(source, out var existing))
        {
            return existing;
        }

        var node = new PsObjectNode(source, this, parent);
        _cache.Add(source, node);
        return node;
    }

    internal IEnumerable<ITreeNode> EnumerateChildren(PsObjectNode node)
    {
        foreach (var child in _childAccessor(node.Source))
        {
            yield return WrapInternal(child, parent: node);
        }
    }

}
