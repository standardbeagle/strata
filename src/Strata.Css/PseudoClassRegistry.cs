namespace Strata.Css;

/// <summary>
/// Registry of <em>simple</em> (non-functional) pseudo-class predicates. Functional forms
/// (<c>:nth-child(an+b)</c>, <c>:not(...)</c>, <c>:is(...)</c>, <c>:has(...)</c>) are
/// resolved at parse time and not handled here.
/// </summary>
public interface IPseudoClassRegistry
{
    /// <summary>Register a simple pseudo-class predicate.</summary>
    void Register(string name, Func<ITreeNode, bool> predicate);

    /// <summary>Evaluate a registered pseudo-class. Throws if the name is unknown.</summary>
    bool Test(string name, ITreeNode node);

    /// <summary>True if <paramref name="name"/> is registered.</summary>
    bool Contains(string name);
}

/// <summary>Default <see cref="IPseudoClassRegistry"/> seeded with the spec's built-ins.</summary>
public sealed class PseudoClassRegistry : IPseudoClassRegistry
{
    private readonly Dictionary<string, Func<ITreeNode, bool>> _predicates = new(StringComparer.Ordinal);

    /// <summary>Create a registry seeded with built-in pseudo-classes.</summary>
    public static PseudoClassRegistry CreateDefault()
    {
        var r = new PseudoClassRegistry();
        r.Register("focused", n => n.PseudoStates.Contains("focused"));
        r.Register("selected", n => n.PseudoStates.Contains("selected"));
        r.Register("hovered", n => n.PseudoStates.Contains("hovered"));
        r.Register("expanded", n => n.PseudoStates.Contains("expanded"));
        r.Register("root", n => n.Parent is null);
        r.Register("empty", n => !n.Children.Any());
        r.Register("first-child", IsFirstChild);
        r.Register("last-child", IsLastChild);
        r.Register("only-child", n => IsFirstChild(n) && IsLastChild(n));
        return r;
    }

    /// <inheritdoc/>
    public void Register(string name, Func<ITreeNode, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(predicate);
        _predicates[name] = predicate;
    }

    /// <inheritdoc/>
    public bool Test(string name, ITreeNode node)
    {
        if (!_predicates.TryGetValue(name, out var predicate))
        {
            throw new InvalidOperationException(
                $"Unknown pseudo-class ':{name}'. Register it via PseudoClassRegistry.Register.");
        }

        return predicate(node);
    }

    /// <inheritdoc/>
    public bool Contains(string name) => _predicates.ContainsKey(name);

    private static bool IsFirstChild(ITreeNode node)
    {
        if (node.Parent is null)
        {
            return false;
        }

        using var e = node.Parent.Children.GetEnumerator();
        return e.MoveNext() && (ReferenceEquals(e.Current, node) || node.Equals(e.Current));
    }

    private static bool IsLastChild(ITreeNode node)
    {
        if (node.Parent is null)
        {
            return false;
        }

        ITreeNode? last = null;
        foreach (var c in node.Parent.Children)
        {
            last = c;
        }

        return last is not null && (ReferenceEquals(last, node) || node.Equals(last));
    }
}
