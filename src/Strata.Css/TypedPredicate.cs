using System.Diagnostics.CodeAnalysis;
using Strata.Css.Expressions;

namespace Strata.Css;

/// <summary>
/// Compiled predicate for the <c>[expr]</c> attribute form
/// (e.g. <c>Process[CPU &gt; 50 and Name.StartsWith("chr")]</c>).
/// </summary>
/// <remarks>
/// Implementation is a hand-written tokenizing parser + tree-walking interpreter — fully
/// AOT-compatible. There is no <c>LambdaExpression.Compile()</c> /
/// <c>Expression.Compile()</c>, and no dynamic code generation.
///
/// <para>Member lookup uses <see cref="Type.GetProperty(string)"/> /
/// <see cref="Type.GetMethod(string, System.Type[])"/>, which is AOT-safe at runtime but is
/// trim-sensitive: callers that publish with aggressive trimming should preserve referenced
/// members on the source type via
/// <see cref="DynamicallyAccessedMembersAttribute"/>.</para>
/// </remarks>
internal sealed class TypedPredicate
{
    private readonly ExprEvaluator _evaluator;

    public TypedPredicate(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        Expression = expression;
        _evaluator = new ExprEvaluator(ExprParser.Parse(expression));
    }

    public string Expression { get; }

    [RequiresUnreferencedCode(
        "Strata.Css [expr] DSL relies on reflection over the source type. Trim-aware " +
        "callers should annotate the adapter's source type with DynamicallyAccessedMembers.")]
    public bool Evaluate(object? underlying)
    {
        if (underlying is null)
        {
            return false;
        }

        return _evaluator.EvaluateBool(underlying);
    }
}
