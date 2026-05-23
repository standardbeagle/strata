using Facebook.Yoga;
using Strata.Core.Properties;
using Strata.Properties.Styling;
using YogaNode = Facebook.Yoga.Node;

namespace Strata.Layout.Yoga;

/// <summary>
/// Builds a parallel Yoga tree from a styled Strata tree and computes per-node box rectangles
/// in integer terminal cells. The mapping from computed style to Yoga node properties follows
/// tech-design §4.1.
/// </summary>
/// <remarks>
/// <para><b>Cell rounding.</b> Yoga returns <see cref="float"/> rects; the terminal addresses
/// integer cells only. Each edge is rounded to the nearest cell, and widths/heights are derived
/// as <c>round(right) - round(left)</c> so adjacent boxes share an exact boundary and there is no
/// sub-cell drift (the second documented Phase 4 risk).</para>
///
/// <para><b>Yoga copy-on-write.</b> <see cref="LayoutAlgorithm.CalculateLayout"/> may clone child
/// nodes internally, so the <see cref="YogaNode"/> references captured at build time are not the
/// ones that carry results. Rects are therefore recovered by index-walking from the root with
/// <see cref="YogaNode.GetChild"/>, zipped against the Strata children in the same build order.</para>
///
/// <para><b>Grid via flex emulation.</b> Yoga.Net 3.2.3 exposes the CSS-Grid API surface
/// (<c>Display.Grid</c>, grid templates, grid lines) but its layout algorithm does not honor it —
/// grid children collapse to the container origin. Per the Phase 4 risk note in docs/04-plan.md
/// ("fall back to flex-only for v1.0 and document grid as v1.1"), <c>display: grid</c> is
/// emulated with a wrapping flex row: each direct child is given an explicit width from the
/// <c>grid-template-columns</c> track at its column index and a height from the
/// <c>grid-template-rows</c> track at its row index, with <c>flex-shrink: 0</c> so the column
/// widths hold; Yoga's flex-wrap then flows the cells into rows. This produces a real
/// multi-column terminal grid. Native grid placement (spanning, named lines) is deferred to
/// v1.1 once the port's grid algorithm lands.</para>
/// </remarks>
public sealed class YogaLayoutPass
{
    /// <summary>
    /// Compute layout for the tree rooted at <paramref name="root"/> using <paramref name="cascade"/>'s
    /// computed values and <paramref name="available"/> as the root's available cell area.
    /// </summary>
    /// <returns>
    /// A <see cref="LayoutResult"/> with absolute integer-cell rects, or
    /// <see cref="LayoutResult.TrivialResult"/> when no node declares a layout-affecting property.
    /// </returns>
    public static LayoutResult Compute(ITreeNode root, ICascadeResult cascade, Size available)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(cascade);

        if (!AnyNodeAffectsLayout(root, cascade))
        {
            return LayoutResult.TrivialResult;
        }

        // Build the parallel Yoga tree in the same child order as the Strata tree, so rects can
        // be recovered later by index-walking from the root (see class remarks on copy-on-write).
        var yogaRoot = BuildNode(root, cascade);

        var availWidth = available.IsWidthBounded ? available.Width : float.NaN;
        var availHeight = available.IsHeightBounded ? available.Height : float.NaN;
        LayoutAlgorithm.CalculateLayout(yogaRoot, availWidth, availHeight, Direction.LTR);

        var rects = new Dictionary<ITreeNode, Rect>();
        var clipped = new HashSet<ITreeNode>();
        Recover(root, yogaRoot, originX: 0, originY: 0, rects, clipped);

        return new LayoutResult(rects, clipped, trivial: false);
    }

    private static YogaNode BuildNode(ITreeNode node, ICascadeResult cascade)
    {
        var yoga = new YogaNode();
        ApplyStyle(yoga, node, cascade);

        var isGrid = Enum(node, cascade, LayoutProperties.Display) == "grid";
        var columns = isGrid ? TrackList(node, cascade, LayoutProperties.GridTemplateColumns) : default;
        var rows = isGrid ? TrackList(node, cascade, LayoutProperties.GridTemplateRows) : default;

        var index = 0;
        foreach (var child in node.Children)
        {
            var childYoga = BuildNode(child, cascade);
            if (isGrid)
            {
                ApplyGridCell(childYoga.Style, index, columns, rows);
            }

            yoga.InsertChild(childYoga, (UIntPtr)index);
            index++;
        }

        return yoga;
    }

    /// <summary>
    /// Size a grid cell's Yoga node from its parent track lists (grid-as-flex emulation).
    /// Column index = <paramref name="childIndex"/> mod column-count; row index = childIndex /
    /// column-count, clamped to the last declared row track. Cells whose track is <c>auto</c> or
    /// missing are left flexible.
    /// </summary>
    private static void ApplyGridCell(Style style, int childIndex, TrackListValue columns, TrackListValue rows)
    {
        var columnCount = columns.Tracks.IsDefaultOrEmpty ? 1 : columns.Tracks.Length;

        if (!columns.Tracks.IsDefaultOrEmpty)
        {
            var col = columns.Tracks[childIndex % columnCount];
            ApplyDimension(style, Dimension.Width, col);
            style.FlexShrink = FloatOptional.Zero;
            style.FlexGrow = FloatOptional.Zero;
        }

        if (!rows.Tracks.IsDefaultOrEmpty)
        {
            var rowIndex = Math.Min(childIndex / columnCount, rows.Tracks.Length - 1);
            ApplyDimension(style, Dimension.Height, rows.Tracks[rowIndex]);
        }
    }

    private static void Recover(
        ITreeNode node,
        YogaNode yoga,
        int originX,
        int originY,
        Dictionary<ITreeNode, Rect> rects,
        HashSet<ITreeNode> clipped)
    {
        var layout = yoga.Layout;

        // Round edges independently so adjacent boxes share an exact integer boundary.
        var left = originX + Round(layout.Position(PhysicalEdge.Left));
        var top = originY + Round(layout.Position(PhysicalEdge.Top));
        var right = left + Round(layout.Dimension(Dimension.Width));
        var bottom = top + Round(layout.Dimension(Dimension.Height));

        var rect = new Rect(left, top, right - left, bottom - top);
        rects[node] = rect;

        if (layout.HadOverflow())
        {
            clipped.Add(node);
        }

        var i = 0;
        foreach (var child in node.Children)
        {
            var childYoga = yoga.GetChild((UIntPtr)i)
                ?? throw new InvalidOperationException(
                    $"Yoga child at index {i} was missing after layout; tree build is out of sync.");
            Recover(child, childYoga, left, top, rects, clipped);
            i++;
        }
    }

    private static void ApplyStyle(YogaNode yoga, ITreeNode node, ICascadeResult cascade)
    {
        var style = yoga.Style;

        // display. Grid is emulated as a wrapping flex row (see class remarks): the native Yoga
        // grid algorithm is non-functional in this port.
        var display = Enum(node, cascade, LayoutProperties.Display);
        style.Display = display == "none" ? Display.None : Display.Flex;

        if (display == "grid")
        {
            style.FlexDirection = FlexDirection.Row;
            style.FlexWrap = Wrap.Wrap;
            style.AlignContent = Align.FlexStart;
        }
        else if (display == "block")
        {
            style.FlexDirection = FlexDirection.Column;
        }
        else if (display == "flex")
        {
            style.FlexDirection = Enum(node, cascade, LayoutProperties.FlexDirection) switch
            {
                "row" => FlexDirection.Row,
                "row-reverse" => FlexDirection.RowReverse,
                "column" => FlexDirection.Column,
                "column-reverse" => FlexDirection.ColumnReverse,
                _ => FlexDirection.Row,
            };
        }

        style.AlignItems = Enum(node, cascade, LayoutProperties.AlignItems) switch
        {
            "flex-start" => Align.FlexStart,
            "center" => Align.Center,
            "flex-end" => Align.FlexEnd,
            "baseline" => Align.Baseline,
            _ => Align.Stretch,
        };

        style.JustifyContent = Enum(node, cascade, LayoutProperties.JustifyContent) switch
        {
            "center" => Justify.Center,
            "flex-end" => Justify.FlexEnd,
            "space-between" => Justify.SpaceBetween,
            "space-around" => Justify.SpaceAround,
            "space-evenly" => Justify.SpaceEvenly,
            _ => Justify.FlexStart,
        };

        // flex item sizing
        style.FlexGrow = new FloatOptional((float)Number(node, cascade, LayoutProperties.FlexGrow));
        style.FlexShrink = new FloatOptional((float)Number(node, cascade, LayoutProperties.FlexShrink));

        var basis = Length(node, cascade, LayoutProperties.FlexBasis);
        if (basis.Unit != LengthUnit.Auto)
        {
            style.FlexBasis = ToSizeLength(basis);
        }

        ApplyDimension(style, Dimension.Width, Length(node, cascade, LayoutProperties.Width));
        ApplyDimension(style, Dimension.Height, Length(node, cascade, LayoutProperties.Height));
        ApplyMinDimension(style, Dimension.Width, Length(node, cascade, LayoutProperties.MinWidth));
        ApplyMinDimension(style, Dimension.Height, Length(node, cascade, LayoutProperties.MinHeight));
        ApplyMaxDimension(style, Dimension.Width, Length(node, cascade, LayoutProperties.MaxWidth));
        ApplyMaxDimension(style, Dimension.Height, Length(node, cascade, LayoutProperties.MaxHeight));

        // box spacing
        ApplyEdge(style, Edge.All, Length(node, cascade, StylingProperties.Padding), padding: true);
        ApplyEdge(style, Edge.All, Length(node, cascade, StylingProperties.Margin), padding: false);

        // gaps: gap sets both axes; row-gap / column-gap override per axis.
        var gap = Length(node, cascade, LayoutProperties.Gap);
        if (gap.Unit == LengthUnit.Cells && gap.Value > 0)
        {
            style.SetGap(Gutter.All, StyleLength.Points((float)gap.Value));
        }

        var rowGap = Length(node, cascade, LayoutProperties.RowGap);
        if (rowGap.Unit == LengthUnit.Cells && rowGap.Value > 0)
        {
            style.SetGap(Gutter.Row, StyleLength.Points((float)rowGap.Value));
        }

        var colGap = Length(node, cascade, LayoutProperties.ColumnGap);
        if (colGap.Unit == LengthUnit.Cells && colGap.Value > 0)
        {
            style.SetGap(Gutter.Column, StyleLength.Points((float)colGap.Value));
        }

        // positioning
        var position = Enum(node, cascade, LayoutProperties.Position);
        style.PositionType = position switch
        {
            "absolute" => PositionType.Absolute,
            "relative" => PositionType.Relative,
            _ => PositionType.Static,
        };

        if (position != "static")
        {
            ApplyInset(style, Edge.Left, Length(node, cascade, LayoutProperties.Left));
            ApplyInset(style, Edge.Top, Length(node, cascade, LayoutProperties.Top));
            ApplyInset(style, Edge.Right, Length(node, cascade, LayoutProperties.Right));
            ApplyInset(style, Edge.Bottom, Length(node, cascade, LayoutProperties.Bottom));
        }

        // Grid track lists are consumed per-child in ApplyGridCell, not on the container —
        // the native Yoga grid algorithm is non-functional in this port (see class remarks).
    }

    private static void ApplyDimension(Style style, Dimension axis, LengthValue length)
    {
        if (length.Unit != LengthUnit.Auto)
        {
            style.SetDimension(axis, ToSizeLength(length));
        }
    }

    private static void ApplyMinDimension(Style style, Dimension axis, LengthValue length)
    {
        if (length.Unit != LengthUnit.Auto)
        {
            style.SetMinDimension(axis, ToSizeLength(length));
        }
    }

    private static void ApplyMaxDimension(Style style, Dimension axis, LengthValue length)
    {
        if (length.Unit != LengthUnit.Auto)
        {
            style.SetMaxDimension(axis, ToSizeLength(length));
        }
    }

    private static void ApplyEdge(Style style, Edge edge, LengthValue length, bool padding)
    {
        if (length.Unit != LengthUnit.Cells || length.Value <= 0)
        {
            return;
        }

        var value = StyleLength.Points((float)length.Value);
        if (padding)
        {
            style.SetPadding(edge, value);
        }
        else
        {
            style.SetMargin(edge, value);
        }
    }

    private static void ApplyInset(Style style, Edge edge, LengthValue length)
    {
        if (length.Unit == LengthUnit.Cells)
        {
            style.SetPosition(edge, StyleLength.Points((float)length.Value));
        }
        else if (length.Unit == LengthUnit.Percent)
        {
            style.SetPosition(edge, StyleLength.Percent((float)length.Value));
        }
    }

    private static StyleSizeLength ToSizeLength(LengthValue length) => length.Unit switch
    {
        LengthUnit.Percent => StyleSizeLength.Percent((float)length.Value),
        LengthUnit.Cells => StyleSizeLength.Points((float)length.Value),
        _ => StyleSizeLength.OfAuto(),
    };

    // --- computed-value helpers -------------------------------------------------------------

    private static bool AnyNodeAffectsLayout(ITreeNode node, ICascadeResult cascade)
    {
        if (NodeAffectsLayout(node, cascade))
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (AnyNodeAffectsLayout(child, cascade))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NodeAffectsLayout(ITreeNode node, ICascadeResult cascade)
    {
        if (Enum(node, cascade, LayoutProperties.Display) != "block")
        {
            return true;
        }

        if (Enum(node, cascade, LayoutProperties.Position) != "static")
        {
            return true;
        }

        foreach (var prop in LayoutLengthProbes)
        {
            if (Length(node, cascade, prop).Unit != LengthUnit.Auto)
            {
                return true;
            }
        }

        if (NonZeroCells(Length(node, cascade, StylingProperties.Padding)) ||
            NonZeroCells(Length(node, cascade, StylingProperties.Margin)))
        {
            return true;
        }

        return false;
    }

    private static readonly string[] LayoutLengthProbes =
    [
        LayoutProperties.Width,
        LayoutProperties.Height,
    ];

    private static bool NonZeroCells(LengthValue length)
        => length.Unit == LengthUnit.Cells && length.Value > 0;

    private static string Enum(ITreeNode node, ICascadeResult cascade, string property)
        => cascade.GetComputed<EnumValue>(node, property).Value;

    private static double Number(ITreeNode node, ICascadeResult cascade, string property)
        => cascade.GetComputed<NumberValue>(node, property).Value;

    private static LengthValue Length(ITreeNode node, ICascadeResult cascade, string property)
        => cascade.GetComputed<LengthValue>(node, property);

    private static TrackListValue TrackList(ITreeNode node, ICascadeResult cascade, string property)
        => cascade.GetComputed<TrackListValue>(node, property);

    private static int Round(float value)
        => float.IsNaN(value) ? 0 : (int)MathF.Round(value, MidpointRounding.AwayFromZero);
}
