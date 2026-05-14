namespace Strata.Css;

internal enum Combinator
{
    Descendant,   // space
    Child,        // >
    AdjacentSibling, // +
    GeneralSibling,  // ~
}

internal enum AttrOp
{
    Exists,
    Equals,
    StartsWith,
    EndsWith,
    Contains,
}

internal sealed class CompoundSelector
{
    public string? Kind { get; init; }       // null = universal
    public bool IsUniversal { get; init; }
    public string? Id { get; init; }
    public required string[] Classes { get; init; }
    public required AttributeMatcher[] Attributes { get; init; }
    public required string[] PseudoClasses { get; init; }
}

internal sealed class AttributeMatcher
{
    public required string Name { get; init; }
    public AttrOp Op { get; init; }
    public string? Value { get; init; }
}

internal sealed class ComplexSelector
{
    /// <summary>
    /// Compound parts ordered <em>right-to-left</em>. <c>Parts[0]</c> is the subject.
    /// </summary>
    public required CompoundSelector[] Parts { get; init; }

    /// <summary>
    /// Combinator between <c>Parts[i]</c> and <c>Parts[i+1]</c> — i.e. how to walk from
    /// the subject (right) toward an ancestor/sibling (left).
    /// </summary>
    public required Combinator[] Combinators { get; init; }

    public Specificity Specificity { get; init; }
}
