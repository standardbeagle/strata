using Json.Path;

namespace Strata.JsonPath;

/// <summary>
/// Derives a CSS-style <see cref="Specificity"/> triple from a parsed JSONPath.
/// </summary>
/// <remarks>
/// <para>
/// JSONPath has no native specificity model — this mapping is deliberately chosen and documented
/// (docs/04-plan.md §Phase 9 risk; tech-design §12 Q1). The mapping mirrors CSS intuition:
/// </para>
/// <list type="bullet">
///   <item><term>A</term><description>name and index selectors (the most specific addressing,
///   analogous to a CSS id) — a path that names exact steps is the most targeted.</description></item>
///   <item><term>B</term><description>filter selectors <c>[?...]</c> (predicate addressing,
///   analogous to a CSS attribute/pseudo-class).</description></item>
///   <item><term>C</term><description>wildcards <c>*</c> and slices <c>[a:b]</c> (the broadest
///   addressing, analogous to a CSS type selector).</description></item>
/// </list>
/// <para>
/// Recursive-descent segments (<c>..</c>) contribute through whatever selectors they carry.
/// Because CSS and JSONPath specificity are computed on different axes, a stylesheet that mixes
/// the two languages can produce cascade orderings that surprise either mental model; this is an
/// accepted edge case documented for users.
/// </para>
/// </remarks>
internal static class JsonPathSpecificity
{
    public static Specificity Compute(Json.Path.JsonPath path)
    {
        var a = 0;
        var b = 0;
        var c = 0;

        if (path.Segments is { } segments)
        {
            foreach (var segment in segments)
            {
                foreach (var selector in segment.Selectors)
                {
                    switch (selector)
                    {
                        case NameSelector:
                        case IndexSelector:
                            a++;
                            break;
                        case FilterSelector:
                            b++;
                            break;
                        case WildcardSelector:
                        case SliceSelector:
                            c++;
                            break;
                    }
                }
            }
        }

        return new Specificity(a, b, c);
    }
}
