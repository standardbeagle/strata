using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Strata.Css.Expressions;

/// <summary>
/// Tree-walking interpreter for <see cref="ExprNode"/> ASTs.
/// </summary>
/// <remarks>
/// AOT-compatible: no <c>LambdaExpression.Compile()</c>, no <c>Expression.Lambda</c>,
/// no dynamic code emission. Member lookups go through
/// <see cref="Type.GetProperty(string)"/> / <see cref="Type.GetMethod(string, Type[])"/>;
/// results are cached per <c>(Type, Name)</c>.
///
/// <para>Trim story: identifiers resolve against the runtime type of the supplied target
/// object. Stylesheets that use <c>[expr]</c> against types whose properties have been
/// trimmed will fail at match time with <see cref="MissingMemberException"/>. To preserve
/// members under trimming, annotate the source type with
/// <see cref="DynamicallyAccessedMembersAttribute"/> on the adapter registration site.</para>
/// </remarks>
internal sealed class ExprEvaluator
{
    private readonly ExprNode _root;
    private readonly ConcurrentDictionary<(Type, string), PropertyInfo?> _propCache = new();
    private readonly ConcurrentDictionary<(Type, string, int), MethodInfo?> _methodCache = new();

    public ExprEvaluator(ExprNode root)
    {
        _root = root;
    }

    [RequiresUnreferencedCode(
        "Strata.Css [expr] DSL uses reflection-based property/method lookup. " +
        "Trim-aware callers should preserve referenced members on the source type.")]
    public bool EvaluateBool(object target) => Truthy(Evaluate(_root, target));

    [RequiresUnreferencedCode("Reflection-based member access; preserve members via DynamicallyAccessedMembersAttribute on the source type.")]
    private object? Evaluate(ExprNode node, object target) => node switch
    {
        LiteralNode l => l.Value,
        IdentNode id => ResolveIdent(id.Name, target),
        MemberAccessNode m => Member(Evaluate(m.Target, target), m.Name),
        MethodCallNode call => Call(call, target),
        BinaryNode b => Compare(b.Op, Evaluate(b.Left, target), Evaluate(b.Right, target)),
        LogicalNode lg => Logical(lg, target),
        NotNode n => !Truthy(Evaluate(n.Operand, target)),
        _ => throw new InvalidOperationException($"Unknown expression node {node.GetType().Name}"),
    };

    [RequiresUnreferencedCode("Reflection-based property access.")]
    private object? ResolveIdent(string name, object target) => Member(target, name);

    [RequiresUnreferencedCode("Reflection-based property access.")]
    private object? Member(object? receiver, string name)
    {
        if (receiver is null)
        {
            return null;
        }

        var type = receiver.GetType();
        var prop = _propCache.GetOrAdd((type, name),
            static key => GetProperty(key.Item1, key.Item2));
        if (prop is null)
        {
            throw new MissingMemberException(type.FullName, name);
        }

        return prop.GetValue(receiver);
    }

    [RequiresUnreferencedCode("Reflection-based property lookup is not trim-safe by default.")]
    private static PropertyInfo? GetProperty(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type,
        string name)
        => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

    [RequiresUnreferencedCode("Reflection-based method dispatch.")]
    private object? Call(MethodCallNode call, object target)
    {
        var receiver = Evaluate(call.Target, target);
        if (receiver is null)
        {
            return null;
        }

        var args = new object?[call.Arguments.Length];
        var argTypes = new Type[call.Arguments.Length];
        for (var i = 0; i < args.Length; i++)
        {
            args[i] = Evaluate(call.Arguments[i], target);
            argTypes[i] = args[i]?.GetType() ?? typeof(object);
        }

        var key = (receiver.GetType(), call.Method, args.Length);
        var method = _methodCache.GetOrAdd(key, _ => FindMethod(receiver.GetType(), call.Method, argTypes));
        if (method is null)
        {
            throw new MissingMethodException(receiver.GetType().FullName, call.Method);
        }

        return method.Invoke(receiver, args);
    }

    [RequiresUnreferencedCode("Reflection-based method lookup is not trim-safe by default.")]
    private static MethodInfo? FindMethod(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type type,
        string name,
        Type[] argTypes)
    {
        // Try exact match first.
        var exact = type.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase,
            binder: null,
            types: argTypes,
            modifiers: null);
        if (exact is not null)
        {
            return exact;
        }

        // Loose match by name + arity.
        foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase))
        {
            if (!string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (m.GetParameters().Length == argTypes.Length)
            {
                return m;
            }
        }

        return null;
    }

    [RequiresUnreferencedCode("Inner evaluation chains reflection.")]
    private bool Logical(LogicalNode node, object target)
    {
        var left = Truthy(Evaluate(node.Left, target));
        return node.Op switch
        {
            LogicalOp.And => left && Truthy(Evaluate(node.Right, target)),
            LogicalOp.Or => left || Truthy(Evaluate(node.Right, target)),
            _ => false,
        };
    }

    private static bool Compare(BinaryOp op, object? left, object? right) => op switch
    {
        BinaryOp.Equals => ValuesEqual(left, right),
        BinaryOp.NotEquals => !ValuesEqual(left, right),
        BinaryOp.Less => Numeric(left) < Numeric(right),
        BinaryOp.LessOrEqual => Numeric(left) <= Numeric(right),
        BinaryOp.Greater => Numeric(left) > Numeric(right),
        BinaryOp.GreaterOrEqual => Numeric(left) >= Numeric(right),
        _ => false,
    };

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            return Numeric(left) == Numeric(right);
        }

        return Equals(left, right);
    }

    private static bool IsNumeric(object o) => o is int or long or short or byte or float or double or decimal;

    private static double Numeric(object? o) => o switch
    {
        null => throw new InvalidOperationException("Cannot compare null numerically."),
        int i => i,
        long l => l,
        short s => s,
        byte b => b,
        float f => f,
        double d => d,
        decimal m => (double)m,
        string s => double.Parse(s, CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"Cannot coerce '{o.GetType().Name}' to number."),
    };

    private static bool Truthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        int i => i != 0,
        long l => l != 0,
        double d => d != 0,
        string s => s.Length > 0,
        _ => true,
    };
}
