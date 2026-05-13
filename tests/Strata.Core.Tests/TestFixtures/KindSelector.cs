namespace Strata.Core.Tests.TestFixtures;

/// <summary>
/// Minimal hand-written selector for tests: matches when <see cref="ITreeNode.Kind"/> equals
/// a target, optionally also requiring a class or attribute equality. No combinators.
/// Phase 0 stand-in until <c>Strata.Css</c> ships.
/// </summary>
internal sealed class KindSelector : ISelector
{
    private readonly string _kind;
    private readonly string? _requiredClass;
    private readonly (string Name, object? Value)? _attribute;

    public KindSelector(string kind, Specificity specificity, string? requiredClass = null, (string, object?)? attribute = null)
    {
        _kind = kind;
        _requiredClass = requiredClass;
        _attribute = attribute;
        Specificity = specificity;
    }

    public Specificity Specificity { get; }

    public bool Matches(ITreeNode node, out MatchContext context)
    {
        context = MatchContext.Empty;
        if (!string.Equals(node.Kind, _kind, StringComparison.Ordinal))
        {
            return false;
        }

        if (_requiredClass is not null && !node.Classes.Contains(_requiredClass))
        {
            return false;
        }

        if (_attribute is { } attr)
        {
            if (!node.TryGetAttribute(attr.Name, out var value))
            {
                return false;
            }

            if (!Equals(value, attr.Value))
            {
                return false;
            }
        }

        return true;
    }

    public IEnumerable<Match> Find(ITreeNode root)
    {
        if (Matches(root, out var ctx))
        {
            yield return new Match(root, ctx);
        }

        foreach (var child in root.Children)
        {
            foreach (var m in Find(child))
            {
                yield return m;
            }
        }
    }
}
