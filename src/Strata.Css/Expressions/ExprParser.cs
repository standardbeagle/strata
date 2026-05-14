using System.Globalization;

namespace Strata.Css.Expressions;

/// <summary>
/// Hand-written parser for the Strata predicate DSL inside <c>[expr]</c>.
/// </summary>
/// <remarks>
/// Grammar:
/// <code>
/// expr     := or_expr
/// or_expr  := and_expr ( 'or' and_expr )*
/// and_expr := unary ( 'and' unary )*
/// unary    := 'not' unary | comparison
/// comparison := primary ( compare_op primary )?
/// primary  := atom postfix*
/// postfix  := '.' IDENT ( '(' args? ')' )?
/// atom     := NUMBER | STRING | 'true' | 'false' | 'null' | IDENT | '(' expr ')'
/// args     := expr (',' expr)*
/// compare_op := '==' | '!=' | '&lt;' | '&lt;=' | '&gt;' | '&gt;='
/// </code>
/// Keywords are case-sensitive lowercase: <c>and</c>, <c>or</c>, <c>not</c>, <c>true</c>,
/// <c>false</c>, <c>null</c>.
/// </remarks>
internal static class ExprParser
{
    public static ExprNode Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokens = Tokenize(source);
        var i = 0;
        var node = ParseOr(tokens, ref i);
        if (i < tokens.Count)
        {
            throw new FormatException(
                $"Unexpected token '{tokens[i]}' in predicate expression at position {tokens[i].Position}.");
        }

        return node;
    }

    private static ExprNode ParseOr(List<Token> t, ref int i)
    {
        var left = ParseAnd(t, ref i);
        while (i < t.Count && t[i].Kind == TokenKind.Keyword && t[i].Text == "or")
        {
            i++;
            var right = ParseAnd(t, ref i);
            left = new LogicalNode(LogicalOp.Or, left, right);
        }

        return left;
    }

    private static ExprNode ParseAnd(List<Token> t, ref int i)
    {
        var left = ParseUnary(t, ref i);
        while (i < t.Count && t[i].Kind == TokenKind.Keyword && t[i].Text == "and")
        {
            i++;
            var right = ParseUnary(t, ref i);
            left = new LogicalNode(LogicalOp.And, left, right);
        }

        return left;
    }

    private static ExprNode ParseUnary(List<Token> t, ref int i)
    {
        if (i < t.Count && t[i].Kind == TokenKind.Keyword && t[i].Text == "not")
        {
            i++;
            return new NotNode(ParseUnary(t, ref i));
        }

        return ParseComparison(t, ref i);
    }

    private static ExprNode ParseComparison(List<Token> t, ref int i)
    {
        var left = ParsePrimary(t, ref i);
        if (i >= t.Count || t[i].Kind != TokenKind.CompareOp)
        {
            return left;
        }

        var op = t[i].Text switch
        {
            "==" => BinaryOp.Equals,
            "!=" => BinaryOp.NotEquals,
            "<" => BinaryOp.Less,
            "<=" => BinaryOp.LessOrEqual,
            ">" => BinaryOp.Greater,
            ">=" => BinaryOp.GreaterOrEqual,
            _ => throw new FormatException($"Unexpected operator '{t[i].Text}'."),
        };
        i++;
        var right = ParsePrimary(t, ref i);
        return new BinaryNode(op, left, right);
    }

    private static ExprNode ParsePrimary(List<Token> t, ref int i)
    {
        var node = ParseAtom(t, ref i);

        while (i < t.Count && t[i].Kind == TokenKind.Dot)
        {
            i++;
            if (i >= t.Count || t[i].Kind != TokenKind.Identifier)
            {
                throw new FormatException("Expected member name after '.'.");
            }

            var memberName = t[i].Text;
            i++;

            if (i < t.Count && t[i].Kind == TokenKind.LParen)
            {
                i++;
                var args = new List<ExprNode>();
                if (i < t.Count && t[i].Kind != TokenKind.RParen)
                {
                    args.Add(ParseOr(t, ref i));
                    while (i < t.Count && t[i].Kind == TokenKind.Comma)
                    {
                        i++;
                        args.Add(ParseOr(t, ref i));
                    }
                }

                if (i >= t.Count || t[i].Kind != TokenKind.RParen)
                {
                    throw new FormatException("Expected ')' in method call.");
                }

                i++;
                node = new MethodCallNode(node, memberName, args.ToArray());
            }
            else
            {
                node = new MemberAccessNode(node, memberName);
            }
        }

        return node;
    }

    private static ExprNode ParseAtom(List<Token> t, ref int i)
    {
        if (i >= t.Count)
        {
            throw new FormatException("Unexpected end of expression.");
        }

        var tok = t[i];
        switch (tok.Kind)
        {
            case TokenKind.Number:
                i++;
                return new LiteralNode(ParseNumber(tok.Text));
            case TokenKind.String:
                i++;
                return new LiteralNode(tok.Text);
            case TokenKind.Keyword:
                switch (tok.Text)
                {
                    case "true":
                        i++;
                        return new LiteralNode(true);
                    case "false":
                        i++;
                        return new LiteralNode(false);
                    case "null":
                        i++;
                        return new LiteralNode(null);
                }

                throw new FormatException($"Unexpected keyword '{tok.Text}' at position {tok.Position}.");
            case TokenKind.Identifier:
                i++;
                return new IdentNode(tok.Text);
            case TokenKind.LParen:
                i++;
                var node = ParseOr(t, ref i);
                if (i >= t.Count || t[i].Kind != TokenKind.RParen)
                {
                    throw new FormatException("Expected ')'.");
                }

                i++;
                return node;
            default:
                throw new FormatException($"Unexpected token '{tok.Text}' at position {tok.Position}.");
        }
    }

    private static object ParseNumber(string text)
    {
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return l <= int.MaxValue && l >= int.MinValue ? (int)l : l;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }

        throw new FormatException($"Invalid number literal '{text}'.");
    }

    private enum TokenKind
    {
        Identifier,
        Keyword,
        Number,
        String,
        CompareOp,
        LParen,
        RParen,
        Comma,
        Dot,
    }

    private readonly record struct Token(TokenKind Kind, string Text, int Position)
    {
        public override string ToString() => Text;
    }

    private static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var s = source.AsSpan();
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            var start = i;
            if (char.IsLetter(c) || c == '_')
            {
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_'))
                {
                    i++;
                }

                var text = s[start..i].ToString();
                tokens.Add(IsKeyword(text)
                    ? new Token(TokenKind.Keyword, text, start)
                    : new Token(TokenKind.Identifier, text, start));
                continue;
            }

            if (char.IsDigit(c) || (c == '-' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                if (s[i] == '-')
                {
                    i++;
                }

                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Number, s[start..i].ToString(), start));
                continue;
            }

            if (c == '"' || c == '\'')
            {
                var quote = c;
                i++;
                var sbStart = i;
                var sb = new System.Text.StringBuilder();
                while (i < s.Length && s[i] != quote)
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                    {
                        sb.Append(s[i + 1]);
                        i += 2;
                        continue;
                    }

                    sb.Append(s[i]);
                    i++;
                }

                if (i >= s.Length)
                {
                    throw new FormatException("Unterminated string literal in predicate.");
                }

                i++; // consume closing quote
                tokens.Add(new Token(TokenKind.String, sb.ToString(), sbStart));
                continue;
            }

            switch (c)
            {
                case '(':
                    tokens.Add(new Token(TokenKind.LParen, "(", i));
                    i++;
                    continue;
                case ')':
                    tokens.Add(new Token(TokenKind.RParen, ")", i));
                    i++;
                    continue;
                case ',':
                    tokens.Add(new Token(TokenKind.Comma, ",", i));
                    i++;
                    continue;
                case '.':
                    tokens.Add(new Token(TokenKind.Dot, ".", i));
                    i++;
                    continue;
                case '=':
                    if (i + 1 < s.Length && s[i + 1] == '=')
                    {
                        tokens.Add(new Token(TokenKind.CompareOp, "==", i));
                        i += 2;
                        continue;
                    }

                    throw new FormatException($"Single '=' is not a valid operator. Use '==' for equality.");
                case '!':
                    if (i + 1 < s.Length && s[i + 1] == '=')
                    {
                        tokens.Add(new Token(TokenKind.CompareOp, "!=", i));
                        i += 2;
                        continue;
                    }

                    throw new FormatException($"Unexpected '!' at position {i}.");
                case '<':
                    if (i + 1 < s.Length && s[i + 1] == '=')
                    {
                        tokens.Add(new Token(TokenKind.CompareOp, "<=", i));
                        i += 2;
                        continue;
                    }

                    tokens.Add(new Token(TokenKind.CompareOp, "<", i));
                    i++;
                    continue;
                case '>':
                    if (i + 1 < s.Length && s[i + 1] == '=')
                    {
                        tokens.Add(new Token(TokenKind.CompareOp, ">=", i));
                        i += 2;
                        continue;
                    }

                    tokens.Add(new Token(TokenKind.CompareOp, ">", i));
                    i++;
                    continue;
                default:
                    throw new FormatException($"Unexpected character '{c}' at position {i}.");
            }
        }

        return tokens;
    }

    private static bool IsKeyword(string text)
        => text is "and" or "or" or "not" or "true" or "false" or "null";
}
