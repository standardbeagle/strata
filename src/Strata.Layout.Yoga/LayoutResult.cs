namespace Strata.Layout.Yoga;

/// <summary>
/// The output of a <see cref="YogaLayoutPass"/>: per-node box rectangles in integer terminal
/// cells, plus a fast-path flag for trees that declared no layout-affecting properties.
/// </summary>
/// <remarks>
/// Rects are absolute within the laid-out root (offsets accumulate down the tree). A node that
/// was part of the input tree but not laid out (e.g. the root has no children) resolves to
/// <see cref="Rect.Empty"/> via <see cref="GetRect"/>.
/// </remarks>
public sealed class LayoutResult
{
    private readonly IReadOnlyDictionary<ITreeNode, Rect> _rects;
    private readonly IReadOnlySet<ITreeNode> _clipped;

    internal LayoutResult(
        IReadOnlyDictionary<ITreeNode, Rect> rects,
        IReadOnlySet<ITreeNode> clipped,
        bool trivial)
    {
        _rects = rects;
        _clipped = clipped;
        Trivial = trivial;
    }

    /// <summary>
    /// A result for a tree that declares no layout-affecting properties. Projections MAY skip
    /// honoring rects entirely and render in document order. See tech-design §4.2.
    /// </summary>
    public static LayoutResult TrivialResult { get; } = new(
        new Dictionary<ITreeNode, Rect>(),
        new HashSet<ITreeNode>(),
        trivial: true);

    /// <summary>
    /// <see langword="true"/> when no node declared <c>display</c>, padding, margin, sizing, gap,
    /// or a positional property, so the layout pass was skipped. Projections can render in
    /// document order. See tech-design §4.2.
    /// </summary>
    public bool Trivial { get; }

    /// <summary>The computed rectangle for <paramref name="node"/>, or <see cref="Rect.Empty"/> if absent.</summary>
    public Rect GetRect(ITreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _rects.TryGetValue(node, out var rect) ? rect : Rect.Empty;
    }

    /// <summary>Whether <paramref name="node"/>'s box overflowed its container and was clipped.</summary>
    public bool IsClipped(ITreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _clipped.Contains(node);
    }
}
