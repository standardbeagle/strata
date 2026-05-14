using Strata.Core;

namespace Strata.Css;

/// <summary>
/// Parses a Strata-flavored CSS document into an <see cref="IStylesheet"/>.
/// </summary>
/// <remarks>
/// Grammar (informal):
/// <code>
/// stylesheet := ( rule | comment )*
/// rule       := selector-list '{' declaration* '}'
/// declaration:= IDENT ':' value ( '!important' )? ';'
/// comment    := '/' '*' ... '*' '/'
/// </code>
/// <para>Property values are passed verbatim (with whitespace trimmed) to the
/// corresponding <see cref="IPropertyDescriptor.Parse"/> registered in the
/// <see cref="IPropertyRegistry"/>. Unknown properties raise
/// <see cref="FormatException"/>; callers that want lenient behavior can wrap their
/// registry with a "warn-and-skip" decorator.</para>
/// </remarks>
public sealed class CssStylesheetParser
{
    private readonly CssSelectorLanguage _selectors;
    private readonly IPropertyRegistry _properties;

    /// <summary>Create a stylesheet parser using a CSS selector language and a property registry.</summary>
    public CssStylesheetParser(CssSelectorLanguage selectors, IPropertyRegistry properties)
    {
        ArgumentNullException.ThrowIfNull(selectors);
        ArgumentNullException.ThrowIfNull(properties);
        _selectors = selectors;
        _properties = properties;
    }

    /// <summary>Parse a stylesheet source string.</summary>
    public IStylesheet Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var s = source.AsSpan();
        var i = 0;
        var rules = new List<IRule>();
        var order = 0;

        while (i < s.Length)
        {
            SkipTrivia(s, ref i);
            if (i >= s.Length)
            {
                break;
            }

            var selectorEnd = IndexOfTopLevel(s, i, '{');
            if (selectorEnd < 0)
            {
                throw new FormatException(
                    $"Expected '{{' to open a rule body near position {i}.");
            }

            var selectorText = s[i..selectorEnd].ToString().Trim();
            i = selectorEnd + 1;

            var bodyEnd = IndexOfTopLevel(s, i, '}');
            if (bodyEnd < 0)
            {
                throw new FormatException(
                    $"Unclosed rule body opened near position {selectorEnd}.");
            }

            var body = s[i..bodyEnd].ToString();
            i = bodyEnd + 1;

            var declarations = ParseDeclarations(body);
            var selectors = _selectors.ParseList(selectorText);

            // Per spec §5.2: each comma-separated selector becomes its own rule with the
            // same declaration block but its own SourceOrder and Specificity.
            foreach (var sel in selectors)
            {
                rules.Add(new Rule(sel, declarations, order++));
            }
        }

        // Version is a hash-equivalent integer of the rule count + content order. Real
        // hot-reload callers re-construct the stylesheet from a newer source; the cascade
        // engine reads Version, not content.
        return new Stylesheet(rules, version: rules.Count);
    }

    private List<Declaration> ParseDeclarations(string body)
    {
        var s = body.AsSpan();
        var i = 0;
        var declarations = new List<Declaration>();

        while (i < s.Length)
        {
            SkipTrivia(s, ref i);
            if (i >= s.Length)
            {
                break;
            }

            // Property name.
            if (!IsIdentStart(s[i]))
            {
                throw new FormatException(
                    $"Expected property name at position {i} in declaration block.");
            }

            var nameStart = i;
            while (i < s.Length && IsIdentPart(s[i]))
            {
                i++;
            }

            var propertyName = s[nameStart..i].ToString();
            SkipTrivia(s, ref i);
            if (i >= s.Length || s[i] != ':')
            {
                throw new FormatException(
                    $"Expected ':' after property '{propertyName}'.");
            }

            i++;
            SkipTrivia(s, ref i);

            // Value: consume until ';' or '}'-equivalent end-of-body, supporting !important.
            var valueStart = i;
            while (i < s.Length && s[i] != ';')
            {
                i++;
            }

            var rawValue = s[valueStart..i].ToString().TrimEnd();
            var important = false;
            if (rawValue.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
            {
                important = true;
                rawValue = rawValue[..^"!important".Length].TrimEnd();
            }

            if (!_properties.TryGet(propertyName, out var descriptor))
            {
                throw new FormatException(
                    $"Unknown property '{propertyName}'. Register a descriptor via IPropertyRegistry.");
            }

            var value = descriptor.Parse(rawValue.AsSpan());
            declarations.Add(new Declaration(propertyName, value, important));

            if (i < s.Length && s[i] == ';')
            {
                i++;
            }
        }

        return declarations;
    }

    private static int IndexOfTopLevel(ReadOnlySpan<char> s, int start, char target)
    {
        var depth = 0;
        for (var i = start; i < s.Length; i++)
        {
            switch (s[i])
            {
                case '/' when i + 1 < s.Length && s[i + 1] == '*':
                    i += 2;
                    while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/'))
                    {
                        i++;
                    }

                    if (i + 1 < s.Length)
                    {
                        i++; // land on '/'
                    }

                    break;
                case '(': case '[':
                    depth++;
                    break;
                case ')': case ']':
                    depth--;
                    break;
                case '"': case '\'':
                    {
                        var quote = s[i];
                        i++;
                        while (i < s.Length && s[i] != quote)
                        {
                            if (s[i] == '\\' && i + 1 < s.Length)
                            {
                                i++;
                            }

                            i++;
                        }
                    }

                    break;
                default:
                    if (depth == 0 && s[i] == target)
                    {
                        return i;
                    }

                    break;
            }
        }

        return -1;
    }

    private static void SkipTrivia(ReadOnlySpan<char> s, ref int i)
    {
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i]))
            {
                i++;
                continue;
            }

            if (i + 1 < s.Length && s[i] == '/' && s[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/'))
                {
                    i++;
                }

                if (i + 1 < s.Length)
                {
                    i += 2;
                }
                else
                {
                    throw new FormatException("Unterminated block comment.");
                }

                continue;
            }

            break;
        }
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '-';

    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';
}
