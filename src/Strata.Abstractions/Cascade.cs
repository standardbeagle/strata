namespace Strata;

/// <summary>
/// The cascade engine resolves a tree + stylesheet into a per-node computed value space
/// according to specificity, importance, and source-order rules.
/// </summary>
public interface ICascade
{
    /// <summary>Initial computation against a tree and stylesheet.</summary>
    ICascadeResult Compute(ITreeNode root, IStylesheet stylesheet);

    /// <summary>
    /// Incrementally update a prior result against a list of tree changes and, optionally,
    /// a new stylesheet. Implementations MUST avoid recomputing subtrees that cannot have
    /// been affected by any of the changes.
    /// </summary>
    ICascadeResult Update(
        ICascadeResult prior,
        IReadOnlyList<TreeChange> treeChanges,
        IStylesheet? newStylesheet = null);
}

/// <summary>
/// The immutable result of a cascade computation. Provides per-node access to matched rules,
/// computed values, and origin diagnostics.
/// </summary>
public interface ICascadeResult
{
    /// <summary>Stylesheet version this result was computed against.</summary>
    int StylesheetVersion { get; }

    /// <summary>
    /// Resolved value for a property on a specific node, walking inheritance and falling back
    /// to the descriptor's initial value when no rule declares it.
    /// </summary>
    TValue GetComputed<TValue>(ITreeNode node, string property)
        where TValue : IPropertyValue;

    /// <summary>
    /// All rules that matched <paramref name="node"/>, ordered by cascade precedence
    /// (winner first).
    /// </summary>
    IReadOnlyList<RuleApplication> GetMatchedRules(ITreeNode node);

    /// <summary>
    /// For diagnostics: describe why a property has its current value on a given node.
    /// </summary>
    PropertyOrigin GetOrigin(ITreeNode node, string property);
}

/// <summary>A rule that matched a specific node, together with its match context.</summary>
public readonly record struct RuleApplication(IRule Rule, MatchContext Context);

/// <summary>How a property's current value was determined for a node.</summary>
/// <param name="Kind">Whether the value was declared, inherited, or fell back to initial.</param>
/// <param name="Rule">The winning rule when <paramref name="Kind"/> is <see cref="OriginKind.Declared"/>; otherwise <see langword="null"/>.</param>
/// <param name="InheritedFrom">The ancestor that provided the value when <paramref name="Kind"/> is <see cref="OriginKind.Inherited"/>; otherwise <see langword="null"/>.</param>
public readonly record struct PropertyOrigin(
    OriginKind Kind,
    IRule? Rule,
    ITreeNode? InheritedFrom);

/// <summary>How a property's value was produced.</summary>
public enum OriginKind
{
    /// <summary>Value was declared by a rule matching this node directly.</summary>
    Declared,

    /// <summary>Value was inherited from an ancestor.</summary>
    Inherited,

    /// <summary>Value fell back to the descriptor's initial value.</summary>
    Initial,
}
