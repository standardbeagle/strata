namespace Strata.Layout.Yoga.Tests;

/// <summary>Minimal in-memory tree node for layout tests. Identity = reference equality.</summary>
internal sealed class LayoutTestNode : ITreeNode
{
    private readonly List<LayoutTestNode> _children = new();

    public LayoutTestNode(string kind, string? id = null, IEnumerable<string>? classes = null)
    {
        Kind = kind;
        Id = id;
        Classes = (classes ?? Enumerable.Empty<string>()).ToHashSet();
    }

    public string Kind { get; }

    public string? Id { get; }

    public IReadOnlySet<string> Classes { get; }

    public IReadOnlySet<string> PseudoStates { get; } = new HashSet<string>();

    public ITreeNode? Parent { get; private set; }

    public IEnumerable<ITreeNode> Children => _children;

    public object? Underlying => this;

    public LayoutTestNode Add(LayoutTestNode child)
    {
        child.Parent = this;
        _children.Add(child);
        return this;
    }

    public bool TryGetAttribute(string name, out object? value)
    {
        value = null;
        return false;
    }
}
