using Spectre.Console;
using Spectre.Console.Rendering;
using Strata.Core.Properties;
using Strata.Layout.Yoga;
using Strata.Properties.Styling;

namespace Strata.Render.Spectre;

/// <summary>
/// Projects a styled Strata tree into a Spectre.Console <see cref="IRenderable"/> for
/// inline (non-full-screen) console output.
/// </summary>
/// <remarks>
/// Walks the tree depth-first. Leaf nodes (no children) emit a styled text run built
/// from their <c>Underlying</c> object's string form. Container nodes emit their
/// children stacked vertically via a <see cref="Rows"/> renderable. Color, weight,
/// style, and decoration come from the cascade.
///
/// <para>When a <see cref="LayoutResult"/> is supplied (the Phase 4 overload
/// <see cref="Project(ITreeNode, ICascadeResult, LayoutResult)"/>), layout is honored:
/// a <c>display: grid</c> container emits a Spectre <see cref="Grid"/> sized to its
/// <c>grid-template-columns</c>, and a container holding absolutely-positioned children
/// emits a <see cref="Canvas"/>-free composition that places each child at its computed
/// rect origin via a <see cref="Layout"/>-style absolute layer (built with
/// <see cref="Grid"/> padding). The no-layout overload renders in document order.</para>
///
/// <para>The projection is pure: given the same inputs it produces an equivalent renderable,
/// with no side effects.</para>
/// </remarks>
public sealed class SpectreProjection : IProjection<IRenderable>
{
    /// <summary>
    /// Optional hook to turn a node's underlying object into display text. Defaults to
    /// <see cref="object.ToString"/> on <see cref="ITreeNode.Underlying"/>.
    /// </summary>
    public Func<ITreeNode, string> TextSelector { get; init; } = DefaultText;

    /// <inheritdoc/>
    public IRenderable Project(ITreeNode root, ICascadeResult cascade)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(cascade);
        return Render(root, cascade, layout: null);
    }

    /// <summary>
    /// Project the styled tree, honoring the computed <paramref name="layout"/>: grid containers
    /// render as Spectre grids and absolutely-positioned children are placed at their rects.
    /// A <see cref="LayoutResult.Trivial"/> layout falls back to document-order rendering.
    /// </summary>
    public IRenderable Project(ITreeNode root, ICascadeResult cascade, LayoutResult layout)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(cascade);
        ArgumentNullException.ThrowIfNull(layout);
        return Render(root, cascade, layout.Trivial ? null : layout);
    }

    private IRenderable Render(ITreeNode node, ICascadeResult cascade, LayoutResult? layout)
    {
        var content = RenderContent(node, cascade, layout);

        // A Dialog/Modal/Popup node floats its content inside a bordered, titled box — the static
        // equivalent of the Terminal.Gui Window the interactive projection raises for the same kind.
        return IsDialog(node) ? WrapDialog(node, content) : content;
    }

    private IRenderable RenderContent(ITreeNode node, ICascadeResult cascade, LayoutResult? layout)
    {
        var children = node.Children.ToList();
        if (children.Count == 0)
        {
            return RenderLeaf(node, cascade);
        }

        if (layout is not null)
        {
            var display = Display(node, cascade);
            if (display == "grid")
            {
                return RenderGrid(node, children, cascade, layout);
            }

            if (children.Any(c => Position(c, cascade) == "absolute"))
            {
                return RenderAbsolute(children, cascade, layout);
            }
        }

        var rendered = new List<IRenderable>(children.Count);
        foreach (var child in children)
        {
            rendered.Add(Render(child, cascade, layout));
        }

        return new Rows(rendered);
    }

    /// <summary>
    /// Render a <c>display: grid</c> container as a Spectre <see cref="Grid"/>. The column count
    /// comes from <c>grid-template-columns</c> (defaulting to a single column); children fill
    /// row-major, matching the layout pass's grid-as-flex emulation.
    /// </summary>
    private Grid RenderGrid(
        ITreeNode node,
        List<ITreeNode> children,
        ICascadeResult cascade,
        LayoutResult layout)
    {
        var columns = GridColumnCount(node, cascade);
        var grid = new Grid();
        for (var c = 0; c < columns; c++)
        {
            // Per-column alignment from the column's first cell's text-align (children fill
            // row-major, so children[c] is the first cell in column c). Lets a stylesheet
            // right-align numeric columns: cells tagged text-align:right -> Justify.Right.
            var column = new GridColumn();
            if (c < children.Count)
            {
                column.Alignment = cascade.GetComputed<EnumValue>(children[c], StylingProperties.TextAlign).Value switch
                {
                    "right" => Justify.Right,
                    "center" => Justify.Center,
                    _ => Justify.Left,
                };
            }

            grid.AddColumn(column);
        }

        for (var i = 0; i < children.Count; i += columns)
        {
            var rowCells = new IRenderable[Math.Min(columns, children.Count - i)];
            for (var c = 0; c < rowCells.Length; c++)
            {
                rowCells[c] = Render(children[i + c], cascade, layout);
            }

            grid.AddRow(rowCells);
        }

        return grid;
    }

    /// <summary>
    /// Render a container with absolutely-positioned children onto a <see cref="Canvas"/>-style
    /// layer: in-flow children stack as rows, and each absolute child is overlaid at its computed
    /// rect using leading blank lines and left padding so the annotation lands at (X, Y).
    /// </summary>
    private Rows RenderAbsolute(
        List<ITreeNode> children,
        ICascadeResult cascade,
        LayoutResult layout)
    {
        var layers = new List<IRenderable>();

        var flow = children.Where(c => Position(c, cascade) != "absolute").ToList();
        if (flow.Count > 0)
        {
            layers.Add(new Rows(flow.Select(c => Render(c, cascade, layout))));
        }

        // Absolutely-positioned children compose on top in ascending z-index order: a higher
        // z-index paints later, so its layer lands last and reads as the frontmost overlay.
        // OrderBy is stable, so equal z-indexes keep document order — the prior behavior.
        var absolute = children
            .Where(c => Position(c, cascade) == "absolute")
            .OrderBy(c => ZIndex(c, cascade));

        foreach (var child in absolute)
        {
            var rect = layout.GetRect(child);
            var inner = Render(child, cascade, layout);
            var placed = new Padder(inner).PadLeft(Math.Max(0, rect.X)).PadTop(Math.Max(0, rect.Y));
            layers.Add(placed);
        }

        return new Rows(layers);
    }

    private Text RenderLeaf(ITreeNode node, ICascadeResult cascade)
    {
        var text = TextSelector(node);
        var style = BuildStyle(node, cascade);

        // A Button leaf wears bracket chrome — the conventional terminal button affordance —
        // so it reads as an actionable control rather than a bare label. Its :focused style
        // (e.g. an inverted color rule) still applies through BuildStyle.
        if (IsButton(node))
        {
            return new Text($"[ {text} ]", style);
        }

        return new Text(text, style);
    }

    /// <summary>Wrap dialog content in a bordered, titled panel — a floating dialog box.</summary>
    private static Panel WrapDialog(ITreeNode node, IRenderable content)
    {
        var panel = new Panel(content)
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader(DialogTitle(node)),
        };
        return panel;
    }

    private static string DialogTitle(ITreeNode node)
        => node.TryGetAttribute("Title", out var title) && title is not null
            ? title.ToString() ?? node.Kind
            : node.Kind;

    private static bool IsButton(ITreeNode node)
        => string.Equals(node.Kind, "Button", StringComparison.OrdinalIgnoreCase);

    private static bool IsDialog(ITreeNode node)
        => string.Equals(node.Kind, "Dialog", StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.Kind, "Modal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.Kind, "Popup", StringComparison.OrdinalIgnoreCase);

    private static double ZIndex(ITreeNode node, ICascadeResult cascade)
        => cascade.TryGetComputed<NumberValue>(node, LayoutProperties.ZIndex, out var z) ? z.Value : 0d;

    private static Style BuildStyle(ITreeNode node, ICascadeResult cascade)
    {
        var color = cascade.GetComputed<ColorValue>(node, StylingProperties.Color);
        var background = cascade.GetComputed<ColorValue>(node, StylingProperties.Background);
        var weight = cascade.GetComputed<EnumValue>(node, StylingProperties.FontWeight);
        var fontStyle = cascade.GetComputed<EnumValue>(node, StylingProperties.FontStyle);
        var decoration = cascade.GetComputed<EnumValue>(node, StylingProperties.TextDecoration);

        var decorations = Decoration.None;
        if (string.Equals(weight.Value, "bold", StringComparison.OrdinalIgnoreCase))
        {
            decorations |= Decoration.Bold;
        }

        if (string.Equals(fontStyle.Value, "italic", StringComparison.OrdinalIgnoreCase))
        {
            decorations |= Decoration.Italic;
        }

        decorations |= decoration.Value switch
        {
            "underline" => Decoration.Underline,
            "strikethrough" => Decoration.Strikethrough,
            _ => Decoration.None,
        };

        return new Style(
            foreground: SpectreColorMap.ToSpectre(color),
            background: SpectreColorMap.ToSpectre(background),
            decoration: decorations);
    }

    private static string Display(ITreeNode node, ICascadeResult cascade)
        => cascade.GetComputed<EnumValue>(node, LayoutProperties.Display).Value;

    private static string Position(ITreeNode node, ICascadeResult cascade)
        => cascade.GetComputed<EnumValue>(node, LayoutProperties.Position).Value;

    private static int GridColumnCount(ITreeNode node, ICascadeResult cascade)
    {
        var tracks = cascade.GetComputed<TrackListValue>(node, LayoutProperties.GridTemplateColumns);
        return tracks.Tracks.IsDefaultOrEmpty ? 1 : tracks.Tracks.Length;
    }

    private static string DefaultText(ITreeNode node)
        => node.Underlying?.ToString() ?? node.Kind;
}
