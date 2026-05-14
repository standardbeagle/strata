using Spectre.Console;
using Spectre.Console.Rendering;
using Strata.Core.Properties;
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
/// style, and decoration come from the cascade; box properties (padding/margin) are
/// honored once the Yoga layout pass lands in Phase 4.
///
/// <para>The projection is pure: given the same <c>(root, cascade)</c> it produces an
/// equivalent renderable, with no side effects.</para>
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
        return Render(root, cascade);
    }

    private IRenderable Render(ITreeNode node, ICascadeResult cascade)
    {
        var children = node.Children.ToList();
        if (children.Count == 0)
        {
            return RenderLeaf(node, cascade);
        }

        var rendered = new List<IRenderable>(children.Count);
        foreach (var child in children)
        {
            rendered.Add(Render(child, cascade));
        }

        return new Rows(rendered);
    }

    private Text RenderLeaf(ITreeNode node, ICascadeResult cascade)
    {
        var text = TextSelector(node);
        var style = BuildStyle(node, cascade);
        return new Text(text, style);
    }

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

    private static string DefaultText(ITreeNode node)
        => node.Underlying?.ToString() ?? node.Kind;
}
