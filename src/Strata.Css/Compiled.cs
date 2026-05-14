namespace Strata.Css;

internal enum Combinator
{
    Descendant,
    Child,
    AdjacentSibling,
    GeneralSibling,
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
    public string? Kind { get; init; }
    public bool IsUniversal { get; init; }
    public string? Id { get; init; }
    public required string[] Classes { get; init; }
    public required AttributeMatcher[] Attributes { get; init; }
    public required PseudoEntry[] Pseudos { get; init; }
    public required TypedPredicate[] TypedPredicates { get; init; }
}

internal sealed class AttributeMatcher
{
    public required string Name { get; init; }
    public AttrOp Op { get; init; }
    public string? Value { get; init; }
}

internal abstract class PseudoEntry
{
    public abstract Specificity Specificity { get; }
}

internal sealed class SimplePseudo(string name) : PseudoEntry
{
    public string Name { get; } = name;

    public override Specificity Specificity => new(0, 1, 0);
}

internal sealed class NotPseudo(ComplexSelector[] inner) : PseudoEntry
{
    public ComplexSelector[] Inner { get; } = inner;

    public override Specificity Specificity => MaxOf(Inner);

    internal static Specificity MaxOf(ComplexSelector[] selectors)
    {
        var max = Strata.Specificity.Zero;
        foreach (var s in selectors)
        {
            if (s.Specificity > max)
            {
                max = s.Specificity;
            }
        }

        return max;
    }
}

internal sealed class IsPseudo(ComplexSelector[] inner) : PseudoEntry
{
    public ComplexSelector[] Inner { get; } = inner;

    public override Specificity Specificity => NotPseudo.MaxOf(Inner);
}

internal sealed class WherePseudo(ComplexSelector[] inner) : PseudoEntry
{
    public ComplexSelector[] Inner { get; } = inner;

    public override Specificity Specificity => Strata.Specificity.Zero;
}

internal sealed class HasPseudo(ComplexSelector inner) : PseudoEntry
{
    public ComplexSelector Inner { get; } = inner;

    public override Specificity Specificity => Inner.Specificity;
}

internal sealed class NthChildPseudo(int a, int b) : PseudoEntry
{
    public int A { get; } = a;

    public int B { get; } = b;

    public override Specificity Specificity => new(0, 1, 0);
}

internal sealed class ComplexSelector
{
    public required CompoundSelector[] Parts { get; init; }
    public required Combinator[] Combinators { get; init; }
    public Specificity Specificity { get; init; }
}
