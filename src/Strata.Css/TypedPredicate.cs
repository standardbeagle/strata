using System.Collections.Concurrent;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

namespace Strata.Css;

/// <summary>
/// Compiled predicate for the <c>[expr]</c> attribute form
/// (e.g. <c>Process[CPU &gt; 50 and Name.StartsWith("chr")]</c>).
/// </summary>
/// <remarks>
/// Compilation is lazy and cached per concrete <see cref="Type"/> of
/// <see cref="ITreeNode.Underlying"/>. The expression syntax is
/// <see cref="System.Linq.Dynamic.Core"/>'s lambda-expression dialect.
///
/// <para><b>AOT note (NFR-1 / spec §7.3):</b> Dynamic.Core uses
/// <see cref="LambdaExpression.Compile()"/> internally. As of .NET 10 + Dynamic.Core
/// 1.7+, compiled expressions work under Native AOT for non-trim-aware types. This
/// path is reachable only when a stylesheet uses the <c>[expr]</c> form; stylesheets
/// that stick to plain <c>[attr op value]</c> remain fully AOT-clean.</para>
/// </remarks>
internal sealed class TypedPredicate
{
    private readonly ConcurrentDictionary<Type, Func<object, bool>> _cache = new();

    public TypedPredicate(string expression)
    {
        Expression = expression;
    }

    public string Expression { get; }

    public bool Evaluate(object? underlying)
    {
        if (underlying is null)
        {
            return false;
        }

        var compiled = _cache.GetOrAdd(underlying.GetType(), CompileFor);
        return compiled(underlying);
    }

    private Func<object, bool> CompileFor(Type sourceType)
    {
        // Compile a lambda of shape: (sourceType obj) => bool
        var lambda = DynamicExpressionParser.ParseLambda(
            new ParsingConfig(),
            createParameterCtor: true,
            itType: sourceType,
            resultType: typeof(bool),
            expression: Expression);

        var compiled = lambda.Compile();
        return obj => (bool)compiled.DynamicInvoke(obj)!;
    }
}
