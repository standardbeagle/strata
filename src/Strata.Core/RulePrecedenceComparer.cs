namespace Strata.Core;

/// <summary>
/// Sorts <see cref="MatchedRule"/> entries so the cascade winner is first.
/// </summary>
/// <remarks>
/// Comparison is per-declaration because <c>!important</c> is a per-declaration flag, not
/// a per-rule one. When iterating <em>all</em> declarations of all matched rules together,
/// use <see cref="CompareForProperty"/>; when ordering rules for diagnostic display, use
/// the <see cref="IComparer{T}.Compare"/> implementation, which conservatively ignores
/// <c>!important</c> (since it varies per declaration).
/// </remarks>
internal sealed class RulePrecedenceComparer : IComparer<MatchedRule>
{
    public static readonly RulePrecedenceComparer Instance = new();

    private RulePrecedenceComparer() { }

    public int Compare(MatchedRule x, MatchedRule y)
    {
        // Higher specificity first.
        var s = y.Specificity.CompareTo(x.Specificity);
        if (s != 0)
        {
            return s;
        }

        // Stable tie-break: larger source order first (later wins).
        return y.SourceOrder - x.SourceOrder;
    }

    /// <summary>
    /// Compares two candidate declarations for a single property. Returns negative if
    /// <paramref name="x"/> wins, positive if <paramref name="y"/> wins.
    /// </summary>
    public static int CompareForProperty(in ResolvedDeclaration x, in ResolvedDeclaration y)
    {
        // Important DESC.
        if (x.Important != y.Important)
        {
            return x.Important ? -1 : 1;
        }

        // Specificity DESC.
        var s = y.Specificity.CompareTo(x.Specificity);
        if (s != 0)
        {
            return s;
        }

        // SourceOrder DESC.
        return y.SourceOrder - x.SourceOrder;
    }
}

internal readonly record struct MatchedRule(IRule Rule, Specificity Specificity, int SourceOrder, MatchContext Context);

internal readonly record struct ResolvedDeclaration(IRule Rule, Declaration Declaration, Specificity Specificity, int SourceOrder)
{
    public bool Important => Declaration.Important;
    public string Property => Declaration.Property;
    public IPropertyValue Value => Declaration.Value;
}
