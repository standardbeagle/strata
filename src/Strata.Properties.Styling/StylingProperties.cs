using Strata.Core;
using Strata.Core.Properties;

namespace Strata.Properties.Styling;

/// <summary>
/// The common styling property set. Call <see cref="RegisterAll"/> to add every descriptor
/// to an <see cref="IPropertyRegistry"/> before parsing a styling stylesheet.
/// </summary>
/// <remarks>
/// Inheritance follows CSS conventions where they make sense for a terminal:
/// text-affecting properties (<c>color</c>, <c>font-weight</c>, <c>font-style</c>,
/// <c>text-decoration</c>, <c>wrap</c>) inherit; box properties (<c>background</c>,
/// <c>overflow</c>, <c>padding</c>, <c>margin</c>) do not.
/// </remarks>
public static class StylingProperties
{
    /// <summary>Property name constants.</summary>
    public const string Color = "color";

    /// <inheritdoc cref="Color"/>
    public const string Background = "background";

    /// <inheritdoc cref="Color"/>
    public const string FontWeight = "font-weight";

    /// <inheritdoc cref="Color"/>
    public const string FontStyle = "font-style";

    /// <inheritdoc cref="Color"/>
    public const string TextDecoration = "text-decoration";

    /// <inheritdoc cref="Color"/>
    public const string Wrap = "wrap";

    /// <inheritdoc cref="Color"/>
    public const string Overflow = "overflow";

    /// <inheritdoc cref="Color"/>
    public const string Padding = "padding";

    /// <inheritdoc cref="Color"/>
    public const string Margin = "margin";

    private static readonly string[] FontWeightValues = ["normal", "bold"];
    private static readonly string[] FontStyleValues = ["normal", "italic"];
    private static readonly string[] TextDecorationValues = ["none", "underline", "strikethrough"];
    private static readonly string[] WrapValues = ["wrap", "nowrap"];
    private static readonly string[] OverflowValues = ["visible", "hidden", "ellipsis"];

    /// <summary>Register all styling descriptors into <paramref name="registry"/>.</summary>
    public static void RegisterAll(IPropertyRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(new ColorPropertyDescriptor(Color, new ColorValue(170, 170, 170), inherits: true));
        registry.Register(new ColorPropertyDescriptor(Background, ColorValue.Transparent, inherits: false));

        registry.Register(new EnumPropertyDescriptor(
            FontWeight, initial: "normal", inherits: true, allowedValues: FontWeightValues));

        registry.Register(new EnumPropertyDescriptor(
            FontStyle, initial: "normal", inherits: true, allowedValues: FontStyleValues));

        registry.Register(new EnumPropertyDescriptor(
            TextDecoration, initial: "none", inherits: true, allowedValues: TextDecorationValues));

        registry.Register(new EnumPropertyDescriptor(
            Wrap, initial: "wrap", inherits: true, allowedValues: WrapValues));

        registry.Register(new EnumPropertyDescriptor(
            Overflow, initial: "visible", inherits: false, allowedValues: OverflowValues));

        registry.Register(new LengthPropertyDescriptor(
            Padding, new LengthValue(0, LengthUnit.Cells), inherits: false));

        registry.Register(new LengthPropertyDescriptor(
            Margin, new LengthValue(0, LengthUnit.Cells), inherits: false));
    }

    /// <summary>Create a fresh registry pre-populated with every styling descriptor.</summary>
    public static IPropertyRegistry CreateRegistry()
    {
        var registry = new PropertyRegistry();
        RegisterAll(registry);
        return registry;
    }
}
