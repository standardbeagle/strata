namespace Strata.Interaction.Tests;

/// <summary>
/// Minimal in-memory tree node for interaction tests. Identity = reference equality, matching the
/// pattern in the other Strata test projects.
/// </summary>
internal sealed class InteractionTestNode : ITreeNode
{
    private readonly Dictionary<string, object?> _attributes;
    private readonly List<InteractionTestNode> _children = new();

    public InteractionTestNode(
        string kind,
        string? id = null,
        IEnumerable<string>? classes = null,
        IEnumerable<string>? pseudoStates = null,
        IDictionary<string, object?>? attributes = null)
    {
        Kind = kind;
        Id = id;
        Classes = (classes ?? Enumerable.Empty<string>()).ToHashSet();
        PseudoStates = (pseudoStates ?? Enumerable.Empty<string>()).ToHashSet();
        _attributes = attributes is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(attributes, StringComparer.Ordinal);
    }

    public string Kind { get; }

    public string? Id { get; }

    public IReadOnlySet<string> Classes { get; }

    public IReadOnlySet<string> PseudoStates { get; }

    public ITreeNode? Parent { get; private set; }

    public IEnumerable<ITreeNode> Children => _children;

    public object? Underlying => this;

    public InteractionTestNode Add(InteractionTestNode child)
    {
        child.Parent = this;
        _children.Add(child);
        return this;
    }

    public bool TryGetAttribute(string name, out object? value)
        => _attributes.TryGetValue(name, out value);
}
