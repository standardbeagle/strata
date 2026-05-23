using Strata.Core;
using Strata.Core.Properties;

namespace Strata.Properties.Styling;

/// <summary>
/// The box-layout property set consumed by the Yoga layout pass (Phase 4): flexbox and grid
/// container/item properties, sizing, positioning, and gaps. Call <see cref="RegisterAll"/>
/// to add every descriptor to an <see cref="IPropertyRegistry"/> before parsing a stylesheet
/// that drives layout.
/// </summary>
/// <remarks>
/// None of these properties inherit — layout is structural, computed per node from its own
/// declarations and its position among siblings. Lengths use <see cref="LengthValue"/> so
/// <c>auto</c>, cells, <c>%</c>, and <c>fr</c> are all expressible. Track lists
/// (<c>grid-template-columns</c>/<c>-rows</c>) parse as space-separated length tokens via
/// <see cref="TrackListValue"/>.
/// </remarks>
public static class LayoutProperties
{
    /// <summary>Layout mode: <c>block</c>, <c>flex</c>, <c>grid</c>, or <c>none</c>.</summary>
    public const string Display = "display";

    /// <summary>Flex main axis: <c>row</c>, <c>row-reverse</c>, <c>column</c>, <c>column-reverse</c>.</summary>
    public const string FlexDirection = "flex-direction";

    /// <inheritdoc cref="Display"/>
    public const string FlexGrow = "flex-grow";

    /// <inheritdoc cref="Display"/>
    public const string FlexShrink = "flex-shrink";

    /// <inheritdoc cref="Display"/>
    public const string FlexBasis = "flex-basis";

    /// <summary>Cross-axis alignment of items.</summary>
    public const string AlignItems = "align-items";

    /// <summary>Main-axis distribution of items.</summary>
    public const string JustifyContent = "justify-content";

    /// <inheritdoc cref="Display"/>
    public const string Width = "width";

    /// <inheritdoc cref="Display"/>
    public const string Height = "height";

    /// <inheritdoc cref="Display"/>
    public const string MinWidth = "min-width";

    /// <inheritdoc cref="Display"/>
    public const string MaxWidth = "max-width";

    /// <inheritdoc cref="Display"/>
    public const string MinHeight = "min-height";

    /// <inheritdoc cref="Display"/>
    public const string MaxHeight = "max-height";

    /// <summary>Positioning scheme: <c>static</c>, <c>relative</c>, <c>absolute</c>.</summary>
    public const string Position = "position";

    /// <inheritdoc cref="Position"/>
    public const string Top = "top";

    /// <inheritdoc cref="Position"/>
    public const string Right = "right";

    /// <inheritdoc cref="Position"/>
    public const string Bottom = "bottom";

    /// <inheritdoc cref="Position"/>
    public const string Left = "left";

    /// <summary>Gap on both axes between flex/grid items.</summary>
    public const string Gap = "gap";

    /// <inheritdoc cref="Gap"/>
    public const string RowGap = "row-gap";

    /// <inheritdoc cref="Gap"/>
    public const string ColumnGap = "column-gap";

    /// <summary>Grid column track sizes (space-separated lengths/fr).</summary>
    public const string GridTemplateColumns = "grid-template-columns";

    /// <inheritdoc cref="GridTemplateColumns"/>
    public const string GridTemplateRows = "grid-template-rows";

    private static readonly string[] DisplayValues = ["block", "flex", "grid", "none"];
    private static readonly string[] FlexDirectionValues = ["row", "row-reverse", "column", "column-reverse"];
    private static readonly string[] AlignItemsValues = ["stretch", "flex-start", "center", "flex-end", "baseline"];
    private static readonly string[] JustifyContentValues =
        ["flex-start", "center", "flex-end", "space-between", "space-around", "space-evenly"];
    private static readonly string[] PositionValues = ["static", "relative", "absolute"];

    private static readonly LengthValue ZeroCells = new(0, LengthUnit.Cells);

    /// <summary>Register all layout descriptors into <paramref name="registry"/>.</summary>
    public static void RegisterAll(IPropertyRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(new EnumPropertyDescriptor(Display, "block", inherits: false, DisplayValues));
        registry.Register(new EnumPropertyDescriptor(FlexDirection, "row", inherits: false, FlexDirectionValues));
        registry.Register(new EnumPropertyDescriptor(AlignItems, "stretch", inherits: false, AlignItemsValues));
        registry.Register(new EnumPropertyDescriptor(JustifyContent, "flex-start", inherits: false, JustifyContentValues));
        registry.Register(new EnumPropertyDescriptor(Position, "static", inherits: false, PositionValues));

        registry.Register(new NumberPropertyDescriptor(FlexGrow, 0, inherits: false));
        registry.Register(new NumberPropertyDescriptor(FlexShrink, 1, inherits: false));

        registry.Register(new LengthPropertyDescriptor(FlexBasis, LengthValue.Auto, inherits: false));
        registry.Register(new LengthPropertyDescriptor(Width, LengthValue.Auto, inherits: false));
        registry.Register(new LengthPropertyDescriptor(Height, LengthValue.Auto, inherits: false));
        registry.Register(new LengthPropertyDescriptor(MinWidth, LengthValue.Auto, inherits: false));
        registry.Register(new LengthPropertyDescriptor(MaxWidth, LengthValue.Auto, inherits: false));
        registry.Register(new LengthPropertyDescriptor(MinHeight, LengthValue.Auto, inherits: false));
        registry.Register(new LengthPropertyDescriptor(MaxHeight, LengthValue.Auto, inherits: false));

        registry.Register(new LengthPropertyDescriptor(Top, LengthValue.Auto, inherits: false));
        registry.Register(new LengthPropertyDescriptor(Right, LengthValue.Auto, inherits: false));
        registry.Register(new LengthPropertyDescriptor(Bottom, LengthValue.Auto, inherits: false));
        registry.Register(new LengthPropertyDescriptor(Left, LengthValue.Auto, inherits: false));

        registry.Register(new LengthPropertyDescriptor(Gap, ZeroCells, inherits: false));
        registry.Register(new LengthPropertyDescriptor(RowGap, ZeroCells, inherits: false));
        registry.Register(new LengthPropertyDescriptor(ColumnGap, ZeroCells, inherits: false));

        registry.Register(new TrackListPropertyDescriptor(GridTemplateColumns));
        registry.Register(new TrackListPropertyDescriptor(GridTemplateRows));
    }
}
