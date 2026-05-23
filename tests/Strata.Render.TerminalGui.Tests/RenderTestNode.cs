using Strata.Interaction;

namespace Strata.Render.TerminalGui.Tests;

/// <summary>
/// Mutable in-memory tree node for projection/reconciliation tests. Identity is reference equality
/// (the contract the projection's node→view map depends on), and children can be added and removed
/// after construction so a test can drive a re-cascade against a changed tree. Implements
/// <see cref="IPseudoStateMutable"/> so the live-input tests can move focus through a
/// <see cref="FocusController"/>.
/// </summary>
internal sealed class RenderTestNode : ITreeNode, IPseudoStateMutable
{
    private readonly Dictionary<string, object?> _attributes;
    private readonly List<RenderTestNode> _children = new();
    private readonly HashSet<string> _pseudoStates;

    public RenderTestNode(
        string kind,
        string? id = null,
        IEnumerable<string>? classes = null,
        IDictionary<string, object?>? attributes = null,
        string? text = null)
    {
        Kind = kind;
        Id = id;
        Classes = (classes ?? Enumerable.Empty<string>()).ToHashSet();
        _pseudoStates = new HashSet<string>();
        _attributes = attributes is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(attributes, StringComparer.Ordinal);
        Text = text;
    }

    /// <summary>Display text the projection's default text selector reads via <see cref="Underlying"/>.</summary>
    public string? Text { get; set; }

    public string Kind { get; }

    public string? Id { get; }

    public IReadOnlySet<string> Classes { get; }

    public IReadOnlySet<string> PseudoStates => _pseudoStates;

    public bool AddPseudoState(string state) => _pseudoStates.Add(state);

    public bool RemovePseudoState(string state) => _pseudoStates.Remove(state);

    public ITreeNode? Parent { get; private set; }

    public IEnumerable<ITreeNode> Children => _children;

    public object? Underlying => this;

    public RenderTestNode Add(RenderTestNode child)
    {
        child.Parent = this;
        _children.Add(child);
        return this;
    }

    public void Remove(RenderTestNode child)
    {
        if (_children.Remove(child))
        {
            child.Parent = null;
        }
    }

    public bool TryGetAttribute(string name, out object? value)
        => _attributes.TryGetValue(name, out value);

    public override string ToString() => Text ?? Kind;
}
