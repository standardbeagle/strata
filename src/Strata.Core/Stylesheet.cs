namespace Strata.Core;

/// <summary>
/// A concrete <see cref="IStylesheet"/> that wraps a fixed list of rules.
/// </summary>
/// <remarks>
/// <see cref="Stylesheet"/> is immutable. To represent a hot-reloaded stylesheet, construct
/// a new instance with an incremented version; the cascade engine detects the change via
/// <see cref="IStylesheet.Version"/> equality.
/// </remarks>
public sealed class Stylesheet : IStylesheet
{
    /// <summary>
    /// Create a stylesheet from an ordered list of rules. <see cref="IRule.SourceOrder"/> is
    /// expected to match each rule's position in the list; callers can use
    /// <see cref="OrderRules"/> to enforce this.
    /// </summary>
    public Stylesheet(IReadOnlyList<IRule> rules, int version)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Rules = rules;
        Version = version;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IRule> Rules { get; }

    /// <inheritdoc/>
    public int Version { get; }

    /// <summary>
    /// Reassigns <see cref="IRule.SourceOrder"/> by position, returning a fresh list of
    /// <see cref="Rule"/> instances. Useful when assembling rules from multiple sources.
    /// </summary>
    public static IReadOnlyList<IRule> OrderRules(IEnumerable<IRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var ordered = new List<IRule>();
        var i = 0;
        foreach (var r in rules)
        {
            ordered.Add(new Rule(r.Selector, r.Declarations, i));
            i++;
        }

        return ordered;
    }
}
