namespace Strata.Css;

/// <summary>
/// Hand-written parser for the Strata CSS selector subset. Parses a single complex
/// selector (no commas) into a <see cref="ComplexSelector"/>.
/// </summary>
/// <remarks>
/// Grammar (subset of CSS Selectors Level 4):
/// <code>
/// complex   := compound ( combinator compound )*
/// combinator:= WS | '>' | '+' | '~'
/// compound  := simple+
/// simple    := type | universal | id | class | attribute | pseudo-class
/// type      := IDENT
/// universal := '*'
/// id        := '#' IDENT
/// class     := '.' IDENT
/// attribute := '[' IDENT ( op ( STRING | IDENT ) )? ']'
/// op        := '=' | '^=' | '$=' | '*='
/// pseudo    := ':' IDENT
/// </code>
/// Functional pseudo-classes (<c>:not</c>, <c>:is</c>, <c>:where</c>, <c>:has</c>,
/// <c>:nth-child</c>) and typed predicates (<c>[expr]</c>) are recognized as syntax errors
/// for now — they land in a follow-up Phase 1 chunk.
/// </remarks>
internal static class SelectorParser
{
    public static ComplexSelector Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var src = source.AsSpan();
        var i = 0;

        var parts = new List<CompoundSelector>();
        var combinators = new List<Combinator>();

        SkipWhitespace(src, ref i);
        if (i >= src.Length)
        {
            throw new FormatException("Empty selector.");
        }

        // First compound (subject when there's no combinator yet, otherwise leftmost ancestor).
        parts.Add(ParseCompound(src, ref i));

        while (i < src.Length)
        {
            var startedWithWs = src[i] == ' ' || src[i] == '\t' || src[i] == '\n' || src[i] == '\r';
            SkipWhitespace(src, ref i);
            if (i >= src.Length)
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
                    if (!startedWithWs)
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

        // Reverse so Parts[0] is the subject (rightmost compound).
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
        var pseudos = new List<string>();
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
                if (any && kind is null && !isUniversal)
                {
                    // Bare identifier after another simple selector with no leading type — invalid.
                    throw new FormatException($"Unexpected identifier at position {i}.");
                }

                if (kind is not null || isUniversal)
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
                attributes.Add(ParseAttribute(src, ref i));
                any = true;
            }
            else if (c == ':')
            {
                i++;
                if (i < src.Length && src[i] == ':')
                {
                    throw new FormatException("Pseudo-elements (::name) are not supported.");
                }

                var name = ReadIdent(src, ref i);

                // Reject functional pseudo-classes for now.
                if (i < src.Length && src[i] == '(')
                {
                    throw new FormatException(
                        $"Functional pseudo-class ':{name}(...)' is not yet supported. " +
                        "Planned for follow-up Phase 1 chunk.");
                }

                pseudos.Add(name);
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
            PseudoClasses = pseudos.ToArray(),
        };
    }

    private static AttributeMatcher ParseAttribute(ReadOnlySpan<char> src, ref int i)
    {
        SkipWhitespace(src, ref i);
        if (i >= src.Length || !IsIdentStart(src[i]))
        {
            throw new FormatException($"Expected attribute name at position {i}.");
        }

        var name = ReadIdent(src, ref i);
        SkipWhitespace(src, ref i);

        if (i < src.Length && src[i] == ']')
        {
            i++;
            return new AttributeMatcher { Name = name, Op = AttrOp.Exists };
        }

        AttrOp op;
        if (i + 1 < src.Length && src[i + 1] == '=')
        {
            op = src[i] switch
            {
                '^' => AttrOp.StartsWith,
                '$' => AttrOp.EndsWith,
                '*' => AttrOp.Contains,
                _ => throw new FormatException($"Invalid attribute operator at position {i}."),
            };
            i += 2;
        }
        else if (i < src.Length && src[i] == '=')
        {
            op = AttrOp.Equals;
            i++;
        }
        else
        {
            throw new FormatException($"Expected attribute operator at position {i}.");
        }

        SkipWhitespace(src, ref i);

        string value;
        if (i < src.Length && (src[i] == '"' || src[i] == '\''))
        {
            value = ReadString(src, ref i);
        }
        else
        {
            value = ReadIdent(src, ref i);
        }

        SkipWhitespace(src, ref i);
        if (i >= src.Length || src[i] != ']')
        {
            throw new FormatException($"Expected ']' at position {i}.");
        }

        i++;
        return new AttributeMatcher { Name = name, Op = op, Value = value };
    }

    private static Specificity ComputeSpecificity(List<CompoundSelector> parts)
    {
        var a = 0; var b = 0; var c = 0;
        foreach (var p in parts)
        {
            if (p.Id is not null)
            {
                a++;
            }

            b += p.Classes.Length;
            b += p.Attributes.Length;
            b += p.PseudoClasses.Length;

            if (p.Kind is not null)
            {
                c++;
            }
            // Universal contributes 0.
        }

        return new Specificity(a, b, c);
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '-';

    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';

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
        i++; // consume closing quote
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
