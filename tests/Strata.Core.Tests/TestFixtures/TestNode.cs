namespace Strata.Core.Tests.TestFixtures;

/// <summary>
/// Minimal in-memory tree node for unit tests. Identity = reference equality, which
/// satisfies the spec's "two instances representing the same logical node compare equal"
/// requirement trivially.
/// </summary>
internal sealed class TestNode : ITreeNode
{
    private readonly Dictionary<string, object?> _attributes;
    private readonly List<TestNode> _children = new();

    public TestNode(string kind, string? id = null, IEnumerable<string>? classes = null, IEnumerable<string>? pseudoStates = null, IDictionary<string, object?>? attributes = null)
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

    public TestNode Add(TestNode child)
    {
        child.Parent = this;
        _children.Add(child);
        return this;
    }

    public bool TryGetAttribute(string name, out object? value)
        => _attributes.TryGetValue(name, out value);
}
