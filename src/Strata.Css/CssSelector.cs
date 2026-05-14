namespace Strata.Css;

/// <summary>
/// An <see cref="ISelector"/> produced from a single CSS-subset selector source string
/// (no commas — comma-separated selector lists are split at the stylesheet level).
/// </summary>
public sealed class CssSelector : ISelector
{
    private readonly ComplexSelector _compiled;
    private readonly IPseudoClassRegistry _pseudos;
    private readonly string _source;

    internal CssSelector(string source, ComplexSelector compiled, IPseudoClassRegistry pseudos)
    {
        _source = source;
        _compiled = compiled;
        _pseudos = pseudos;
    }

    /// <inheritdoc/>
    public Specificity Specificity => _compiled.Specificity;

    /// <summary>The original selector source for diagnostics.</summary>
    public string Source => _source;

    /// <inheritdoc/>
    public bool Matches(ITreeNode node, out MatchContext context)
    {
        ArgumentNullException.ThrowIfNull(node);
        context = MatchContext.Empty;
        return SelectorMatcher.Matches(_compiled, node, _pseudos);
    }

    /// <inheritdoc/>
    public IEnumerable<Match> Find(ITreeNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (Matches(root, out var ctx))
        {
            yield return new Match(root, ctx);
        }

        foreach (var child in root.Children)
        {
            foreach (var m in Find(child))
            {
                yield return m;
            }
        }
    }
}

/// <summary>The CSS subset <see cref="ISelectorLanguage"/>.</summary>
public sealed class CssSelectorLanguage : ISelectorLanguage
{
    private readonly IPseudoClassRegistry _pseudos;

    /// <summary>Create a language using the default pseudo-class registry.</summary>
    public CssSelectorLanguage()
        : this(PseudoClassRegistry.CreateDefault())
    {
    }

    /// <summary>Create a language using a caller-supplied pseudo-class registry.</summary>
    public CssSelectorLanguage(IPseudoClassRegistry pseudos)
    {
        ArgumentNullException.ThrowIfNull(pseudos);
        _pseudos = pseudos;
    }

    /// <inheritdoc/>
    public string Name => "css";

    /// <summary>The pseudo-class registry this language consults at match time.</summary>
    public IPseudoClassRegistry PseudoClasses => _pseudos;

    /// <inheritdoc/>
    public ISelector Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var compiled = SelectorParser.Parse(source);
        return new CssSelector(source, compiled, _pseudos);
    }

    /// <summary>
    /// Parse a comma-separated selector list, returning one <see cref="ISelector"/> per
    /// item. Use this to expand a CSS selector list into separate rules per spec §5.2.
    /// </summary>
    public IReadOnlyList<ISelector> ParseList(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var items = SplitTopLevelCommas(source);
        var result = new ISelector[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            result[i] = Parse(items[i]);
        }

        return result;
    }

    private static string[] SplitTopLevelCommas(string source)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < source.Length; i++)
        {
            switch (source[i])
            {
                case '(': case '[': depth++; break;
                case ')': case ']': depth--; break;
                case ',' when depth == 0:
                    parts.Add(source[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        parts.Add(source[start..].Trim());
        return parts.ToArray();
    }
}
