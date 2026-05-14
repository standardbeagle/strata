namespace Strata.Css.Expressions;

/// <summary>AST nodes for the AOT-clean Strata predicate DSL used inside <c>[expr]</c>.</summary>
internal abstract class ExprNode;

internal sealed class LiteralNode(object? value) : ExprNode
{
    public object? Value { get; } = value;
}

internal sealed class IdentNode(string name) : ExprNode
{
    public string Name { get; } = name;
}

internal sealed class MemberAccessNode(ExprNode target, string name) : ExprNode
{
    public ExprNode Target { get; } = target;

    public string Name { get; } = name;
}

internal sealed class MethodCallNode(ExprNode target, string method, ExprNode[] arguments) : ExprNode
{
    public ExprNode Target { get; } = target;

    public string Method { get; } = method;

    public ExprNode[] Arguments { get; } = arguments;
}

internal enum BinaryOp
{
    Equals,
    NotEquals,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
}

internal sealed class BinaryNode(BinaryOp op, ExprNode left, ExprNode right) : ExprNode
{
    public BinaryOp Op { get; } = op;

    public ExprNode Left { get; } = left;

    public ExprNode Right { get; } = right;
}

internal enum LogicalOp
{
    And,
    Or,
}

internal sealed class LogicalNode(LogicalOp op, ExprNode left, ExprNode right) : ExprNode
{
    public LogicalOp Op { get; } = op;

    public ExprNode Left { get; } = left;

    public ExprNode Right { get; } = right;
}

internal sealed class NotNode(ExprNode operand) : ExprNode
{
    public ExprNode Operand { get; } = operand;
}
