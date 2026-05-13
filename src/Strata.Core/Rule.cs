namespace Strata.Core;

/// <summary>A concrete <see cref="IRule"/> pairing a selector with declarations.</summary>
public sealed class Rule : IRule
{
    /// <summary>Create a rule.</summary>
    public Rule(ISelector selector, IReadOnlyList<Declaration> declarations, int sourceOrder)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(declarations);
        Selector = selector;
        Declarations = declarations;
        SourceOrder = sourceOrder;
    }

    /// <inheritdoc/>
    public ISelector Selector { get; }

    /// <inheritdoc/>
    public IReadOnlyList<Declaration> Declarations { get; }

    /// <inheritdoc/>
    public int SourceOrder { get; }
}
