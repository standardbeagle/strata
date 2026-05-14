namespace Strata.Css;

/// <summary>
/// Hand-written parser for the Strata CSS selector subset.
/// </summary>
/// <remarks>
/// Supports: type, universal, id, class, attribute (5 ops), combinators (descendant /
/// child / adj-sibling / gen-sibling), simple pseudo-classes (<c>:name</c>), and the
/// functional pseudo-classes <c>:not</c>, <c>:is</c>, <c>:where</c>, <c>:has</c>,
/// <c>:nth-child(an+b)</c>.
/// <para>Typed predicates (<c>[expr]</c>) and pseudo-elements (<c>::</c>) are explicit
/// failures for now.</para>
/// </remarks>
internal static class SelectorParser
{
    public static ComplexSelector Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var src = source.AsSpan();
        var i = 0;
        var result = ParseComplex(src, ref i, source);
        SkipWhitespace(src, ref i);
        if (i < src.Length)
        {
            throw new FormatException(
                $"Unexpected trailing characters at position {i} in selector '{source}'.");
        }

        return result;
    }

    /// <summary>Parse a comma-separated list of complex selectors (no top-level commas left unconsumed).</summary>
    public static ComplexSelector[] ParseSelectorList(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var src = source.AsSpan();
        var i = 0;
        var list = new List<ComplexSelector>();
        while (i < src.Length)
        {
            SkipWhitespace(src, ref i);
            list.Add(ParseComplex(src, ref i, source));
            SkipWhitespace(src, ref i);
            if (i < src.Length && src[i] == ',')
            {
                i++;
                continue;
            }

            break;
        }

        if (i < src.Length)
        {
            throw new FormatException(
                $"Unexpected trailing characters at position {i} in selector list '{source}'.");
        }

        return list.ToArray();
    }

    private static ComplexSelector ParseComplex(ReadOnlySpan<char> src, ref int i, string source)
    {
        var parts = new List<CompoundSelector>();
        var combinators = new List<Combinator>();

        SkipWhitespace(src, ref i);
        if (i >= src.Length || IsStopChar(src[i]))
        {
            throw new FormatException("Empty selector.");
        }

        parts.Add(ParseCompound(src, ref i));

        while (i < src.Length && !IsStopChar(src[i]))
        {
            var hadWs = HasWhitespace(src, i);
            SkipWhitespace(src, ref i);
            if (i >= src.Length || IsStopChar(src[i]))
            {
                break;
            }

            Combinator combinator;
            switch (src[i])
            {
                case '>':
                    combinator = Combinator.Child;
                    i++;
                    SkipWhitespace(src, ref i);
                    break;
                case '+':
                    combinator = Combinator.AdjacentSibling;
                    i++;
                    SkipWhitespace(src, ref i);
                    break;
                case '~':
                    combinator = Combinator.GeneralSibling;
                    i++;
                    SkipWhitespace(src, ref i);
                    break;
                default:
                    if (!hadWs)
                    {
                        throw new FormatException(
                            $"Unexpected character '{src[i]}' at position {i} in selector '{source}'.");
                    }

                    combinator = Combinator.Descendant;
                    break;
            }

            combinators.Add(combinator);
            parts.Add(ParseCompound(src, ref i));
        }

        parts.Reverse();
        combinators.Reverse();

        return new ComplexSelector
        {
            Parts = parts.ToArray(),
            Combinators = combinators.ToArray(),
            Specificity = ComputeSpecificity(parts),
        };
    }

    private static CompoundSelector ParseCompound(ReadOnlySpan<char> src, ref int i)
    {
        string? kind = null;
        var isUniversal = false;
        string? id = null;
        var classes = new List<string>();
        var attributes = new List<AttributeMatcher>();
        var typedPredicates = new List<TypedPredicate>();
        var pseudos = new List<PseudoEntry>();
        var any = false;

        while (i < src.Length)
        {
            var c = src[i];
            if (c == '*')
            {
                if (any)
                {
                    throw new FormatException($"Universal '*' must lead a compound (position {i}).");
                }

                isUniversal = true;
                i++;
                any = true;
            }
            else if (IsIdentStart(c))
            {
                if (kind is not null || isUniversal)
                {
                    break;
                }

                if (any)
                {
                    break;
                }

                kind = ReadIdent(src, ref i);
                any = true;
            }
            else if (c == '#')
            {
                i++;
                if (id is not null)
                {
                    throw new FormatException($"Duplicate '#id' at position {i}.");
                }

                id = ReadIdent(src, ref i);
                any = true;
            }
            else if (c == '.')
            {
                i++;
                classes.Add(ReadIdent(src, ref i));
                any = true;
            }
            else if (c == '[')
            {
                i++;
                ParseBracketed(src, ref i, attributes, typedPredicates);
                any = true;
            }
            else if (c == ':')
            {
                i++;
                if (i < src.Length && src[i] == ':')
                {
                    throw new FormatException("Pseudo-elements (::name) are not supported.");
                }

                pseudos.Add(ParsePseudo(src, ref i));
                any = true;
            }
            else
            {
                break;
            }
        }

        if (!any)
        {
            throw new FormatException($"Expected simple selector at position {i}.");
        }

        return new CompoundSelector
        {
            Kind = kind,
            IsUniversal = isUniversal && kind is null,
            Id = id,
            Classes = classes.ToArray(),
            Attributes = attributes.ToArray(),
            TypedPredicates = typedPredicates.ToArray(),
            Pseudos = pseudos.ToArray(),
        };
    }

    /// <summary>
    /// Read the content of <c>[...]</c> and dispatch to either <see cref="AttributeMatcher"/>
    /// (simple <c>attr (op value)?</c>) or <see cref="TypedPredicate"/> (expression).
    /// Heuristic: if the content matches <c>IDENT (op (STRING|IDENT))?</c> exactly, it's
    /// simple. Otherwise it's an expression. This matches spec §3.1's distinction.
    /// </summary>
    private static void ParseBracketed(
        ReadOnlySpan<char> src,
        ref int i,
        List<AttributeMatcher> attributes,
        List<TypedPredicate> typedPredicates)
    {
        // Scan to matching ']' with quote + paren awareness; capture content.
        var start = i;
        var depth = 0;
        while (i < src.Length)
        {
            var c = src[i];
            if (c == '"' || c == '\'')
            {
                i++;
                while (i < src.Length && src[i] != c)
                {
                    if (src[i] == '\\' && i + 1 < src.Length)
                    {
                        i++;
                    }

                    i++;
                }

                if (i < src.Length)
                {
                    i++;
                }

                continue;
            }

            if (c == '(' || c == '[')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }
            else if (c == ']')
            {
                if (depth == 0)
                {
                    break;
                }

                depth--;
            }

            i++;
        }

        if (i >= src.Length || src[i] != ']')
        {
            throw new FormatException("Unclosed '['.");
        }

        var content = src[start..i].ToString();
        i++; // consume ']'

        if (TryParseSimpleAttribute(content, out var attr))
        {
            attributes.Add(attr);
        }
        else
        {
            typedPredicates.Add(new TypedPredicate(content.Trim()));
        }
    }

    /// <summary>
    /// Try parsing <paramref name="content"/> as the simple attribute form
    /// <c>IDENT (op (STRING|IDENT))?</c>. Returns false if the content uses any other
    /// operator or extra tokens — caller treats those as a typed predicate.
    /// </summary>
    private static bool TryParseSimpleAttribute(string content, out AttributeMatcher attr)
    {
        attr = null!;
        var src = content.AsSpan();
        var i = 0;
        SkipWhitespace(src, ref i);
        if (i >= src.Length || !IsIdentStart(src[i]))
        {
            return false;
        }

        var name = ReadIdent(src, ref i);
        SkipWhitespace(src, ref i);

        if (i >= src.Length)
        {
            attr = new AttributeMatcher { Name = name, Op = AttrOp.Exists };
            return true;
        }

        // Operator must be exactly one of the five simple forms.
        AttrOp op;
        if (i + 1 < src.Length && src[i + 1] == '=')
        {
            op = src[i] switch
            {
                '^' => AttrOp.StartsWith,
                '$' => AttrOp.EndsWith,
                '*' => AttrOp.Contains,
                _ => default,
            };
            if (op == default && src[i] != '=')
            {
                return false;
            }

            i += 2;
        }
        else if (src[i] == '=')
        {
            op = AttrOp.Equals;
            i++;
        }
        else
        {
            return false; // not simple — typed predicate
        }

        SkipWhitespace(src, ref i);
        if (i >= src.Length)
        {
            return false;
        }

        string value;
        if (src[i] == '"' || src[i] == '\'')
        {
            try
            {
                value = ReadString(src, ref i);
            }
            catch (FormatException)
            {
                return false;
            }
        }
        else if (IsIdentStart(src[i]))
        {
            value = ReadIdent(src, ref i);
        }
        else
        {
            return false;
        }

        SkipWhitespace(src, ref i);
        if (i < src.Length)
        {
            return false; // trailing tokens → typed predicate
        }

        attr = new AttributeMatcher { Name = name, Op = op, Value = value };
        return true;
    }

    private static PseudoEntry ParsePseudo(ReadOnlySpan<char> src, ref int i)
    {
        var name = ReadIdent(src, ref i);

        if (i >= src.Length || src[i] != '(')
        {
            return new SimplePseudo(name);
        }

        // Functional pseudo: read balanced-paren argument string.
        i++; // consume '('
        var argStart = i;
        var depth = 1;
        while (i < src.Length && depth > 0)
        {
            switch (src[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth == 0)
                    {
                        goto done;
                    }

                    break;
            }

            i++;
        }

        done:
        if (depth != 0)
        {
            throw new FormatException("Unbalanced parentheses in functional pseudo.");
        }

        var args = src[argStart..i].ToString();
        i++; // consume ')'

        return name switch
        {
            "not" => new NotPseudo(ParseSelectorListInside(args)),
            "is" => new IsPseudo(ParseSelectorListInside(args)),
            "where" => new WherePseudo(ParseSelectorListInside(args)),
            "has" => new HasPseudo(ParseComplexInside(args)),
            "nth-child" => ParseNthChild(args),
            _ => throw new FormatException($"Unknown functional pseudo-class ':{name}(...)'."),
        };
    }

    private static ComplexSelector[] ParseSelectorListInside(string args) => ParseSelectorList(args);

    private static ComplexSelector ParseComplexInside(string args)
    {
        var src = args.AsSpan();
        var i = 0;
        SkipWhitespace(src, ref i);
        var result = ParseComplex(src, ref i, args);
        SkipWhitespace(src, ref i);
        if (i < src.Length)
        {
            throw new FormatException(":has() expects a single relative selector, not a list.");
        }

        return result;
    }

    private static NthChildPseudo ParseNthChild(string args)
    {
        var s = args.Trim();
        switch (s.ToLowerInvariant())
        {
            case "odd":
                return new NthChildPseudo(2, 1);
            case "even":
                return new NthChildPseudo(2, 0);
        }

        // Forms: 'n', '-n', 'an', 'an+b', 'an-b', 'b'.
        int a = 0, b = 0;
        var idx = 0;
        var lower = s.ToLowerInvariant();
        var nPos = lower.IndexOf('n');

        if (nPos < 0)
        {
            // pure constant
            if (!int.TryParse(s, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out b))
            {
                throw new FormatException($":nth-child argument '{s}' is not valid.");
            }

            return new NthChildPseudo(0, b);
        }

        // coefficient before 'n'
        var coeff = lower[..nPos];
        a = coeff switch
        {
            "" or "+" => 1,
            "-" => -1,
            _ => int.Parse(coeff, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture),
        };

        idx = nPos + 1;
        // optional + or - then digits
        while (idx < s.Length && char.IsWhiteSpace(s[idx]))
        {
            idx++;
        }

        if (idx < s.Length)
        {
            var sign = 1;
            if (s[idx] == '+')
            {
                idx++;
            }
            else if (s[idx] == '-')
            {
                sign = -1;
                idx++;
            }

            while (idx < s.Length && char.IsWhiteSpace(s[idx]))
            {
                idx++;
            }

            var rest = s[idx..].Trim();
            if (rest.Length > 0)
            {
                b = sign * int.Parse(rest, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return new NthChildPseudo(a, b);
    }

    private static Specificity ComputeSpecificity(List<CompoundSelector> parts)
    {
        var spec = Strata.Specificity.Zero;
        foreach (var p in parts)
        {
            if (p.Id is not null)
            {
                spec += new Specificity(1, 0, 0);
            }

            // Typed predicates count like attribute selectors per spec §3.1 (B component).
            spec += new Specificity(0, p.Classes.Length + p.Attributes.Length + p.TypedPredicates.Length, 0);

            if (p.Kind is not null)
            {
                spec += new Specificity(0, 0, 1);
            }

            foreach (var pseudo in p.Pseudos)
            {
                spec += pseudo.Specificity;
            }
        }

        return spec;
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '-';

    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';

    private static bool IsStopChar(char c) => c == ',' || c == ')';

    private static bool HasWhitespace(ReadOnlySpan<char> src, int i)
        => i < src.Length && (src[i] == ' ' || src[i] == '\t' || src[i] == '\n' || src[i] == '\r');

    private static string ReadIdent(ReadOnlySpan<char> src, ref int i)
    {
        if (i >= src.Length || !IsIdentStart(src[i]))
        {
            throw new FormatException($"Expected identifier at position {i}.");
        }

        var start = i;
        i++;
        while (i < src.Length && IsIdentPart(src[i]))
        {
            i++;
        }

        return src[start..i].ToString();
    }

    private static string ReadString(ReadOnlySpan<char> src, ref int i)
    {
        var quote = src[i];
        i++;
        var start = i;
        while (i < src.Length && src[i] != quote)
        {
            if (src[i] == '\\' && i + 1 < src.Length)
            {
                i += 2;
                continue;
            }

            i++;
        }

        if (i >= src.Length)
        {
            throw new FormatException("Unterminated string in selector.");
        }

        var value = src[start..i].ToString();
        i++;
        return value;
    }

    private static void SkipWhitespace(ReadOnlySpan<char> src, ref int i)
    {
        while (i < src.Length && (src[i] == ' ' || src[i] == '\t' || src[i] == '\n' || src[i] == '\r'))
        {
            i++;
        }
    }
}
