namespace Strata;

/// <summary>
/// Adapters that support incremental cascade updates implement this to publish a stream
/// of tree mutations. Adapters that don't are statically re-cascaded.
/// </summary>
/// <remarks>
/// The interface is intentionally minimal: <see cref="IObservable{T}"/> is part of the BCL
/// (<c>System.IObservable</c>), so <c>Strata.Abstractions</c> can express the contract
/// without depending on <c>System.Reactive</c>.
/// </remarks>
public interface INodeMutationSource
{
    /// <summary>An observable stream of tree changes.</summary>
    IObservable<TreeChange> Changes { get; }
}

/// <summary>A single mutation to a tree.</summary>
public abstract record TreeChange(ITreeNode Node)
{
    /// <summary>A node was inserted under its parent.</summary>
    /// <param name="Node">The newly-inserted node.</param>
    /// <param name="PreviousSibling">The sibling immediately before <paramref name="Node"/>, or <see langword="null"/> if first.</param>
    public sealed record Inserted(ITreeNode Node, ITreeNode? PreviousSibling)
        : TreeChange(Node);

    /// <summary>A node was removed from its parent.</summary>
    public sealed record Removed(ITreeNode Node) : TreeChange(Node);

    /// <summary>A class label was added to or removed from a node.</summary>
    /// <param name="Node">The affected node.</param>
    /// <param name="Class">The class that changed.</param>
    /// <param name="Added"><see langword="true"/> if added; <see langword="false"/> if removed.</param>
    public sealed record ClassChanged(ITreeNode Node, string Class, bool Added)
        : TreeChange(Node);

    /// <summary>A pseudo-state was toggled on a node.</summary>
    /// <param name="Node">The affected node.</param>
    /// <param name="State">The pseudo-state that changed (e.g. <c>"focused"</c>).</param>
    /// <param name="Added"><see langword="true"/> if added; <see langword="false"/> if removed.</param>
    public sealed record PseudoStateChanged(ITreeNode Node, string State, bool Added)
        : TreeChange(Node);

    /// <summary>An attribute value changed on a node.</summary>
    /// <param name="Node">The affected node.</param>
    /// <param name="Attribute">The attribute that changed.</param>
    public sealed record AttributeChanged(ITreeNode Node, string Attribute)
        : TreeChange(Node);
}
