using Strata.Css.Expressions;

namespace Strata.Css;

/// <summary>
/// A parsed <c>[expr]</c> typed predicate (e.g. <c>Process[CPU &gt; 50 and
/// Name.StartsWith("chr")]</c>).
/// </summary>
/// <remarks>
/// Parsing the expression text into an <see cref="ExprNode"/> AST is AOT-clean — the
/// tokenizer and parser use no reflection. <em>Evaluation</em> needs reflection over the
/// node's <see cref="ITreeNode.Underlying"/> type, so it is routed through the opt-in
/// <see cref="CssPredicates"/> hook. A stylesheet that uses <c>[expr]</c> without calling
/// <see cref="CssPredicates.Enable"/> throws a clear error at match time rather than
/// silently mis-matching.
/// </remarks>
internal sealed class TypedPredicate
{
    public TypedPredicate(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        Expression = expression;
        Ast = ExprParser.Parse(expression);
    }

    public string Expression { get; }

    internal ExprNode Ast { get; }

    public bool Evaluate(object? underlying)
    {
        if (underlying is null)
        {
            return false;
        }

        var evaluator = CssPredicates.Evaluator
            ?? throw new InvalidOperationException(
                $"Selector uses a typed predicate '[{Expression}]' but [expr] support is " +
                "not enabled. Call CssPredicates.Enable() once at startup. " +
                "(Note: [expr] evaluation uses reflection and is not trim-safe.)");

        return evaluator(Ast, underlying);
    }
}
