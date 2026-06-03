namespace Strata.Dsl;

/// <summary>
/// The shared text selector for Strata projections: a <c>Graph</c> element renders its bound
/// <c>data</c> as a sparkline; every other element renders its <c>text</c> attribute. Both the
/// render-once <see cref="StrataConsole"/> and the live <c>StrataLiveHost</c> use this so
/// widget text is produced one way.
/// </summary>
public static class StrataText
{
    /// <summary>Produce the display string for <paramref name="node"/>.</summary>
    public static string ForNode(ITreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Kind == "Graph")
        {
            return node.TryGetAttribute("data", out var data)
                ? Sparkline.Render(Sparkline.Coerce(data))
                : string.Empty;
        }

        return node.TryGetAttribute("text", out var text) ? text?.ToString() ?? string.Empty : string.Empty;
    }
}
