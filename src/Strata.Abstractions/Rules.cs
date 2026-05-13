namespace Strata;

/// <summary>A rule pairs a selector with a list of property declarations.</summary>
public interface IRule
{
    /// <summary>The selector that determines which nodes this rule applies to.</summary>
    ISelector Selector { get; }

    /// <summary>The declarations this rule contributes when matched.</summary>
    IReadOnlyList<Declaration> Declarations { get; }

    /// <summary>
    /// Position in the originating stylesheet, used as the stable cascade tie-breaker.
    /// Larger value wins on ties.
    /// </summary>
    int SourceOrder { get; }
}

/// <summary>A single property declaration within an <see cref="IRule"/>.</summary>
/// <param name="Property">Property name (case-sensitive; lowercase by convention).</param>
/// <param name="Value">Typed property value.</param>
/// <param name="Important">Whether this declaration carries <c>!important</c>.</param>
public readonly record struct Declaration(
    string Property,
    IPropertyValue Value,
    bool Important);

/// <summary>
/// A collection of rules from a single source (file, stream, embedded literal). Stylesheets
/// are versioned so the cascade engine can detect mutation and re-cascade incrementally.
/// </summary>
public interface IStylesheet
{
    /// <summary>All rules in this stylesheet, in source order.</summary>
    IReadOnlyList<IRule> Rules { get; }

    /// <summary>
    /// Monotonically increasing version. Increments on any edit. The cascade engine uses
    /// this to detect change without rule-by-rule comparison.
    /// </summary>
    int Version { get; }
}
