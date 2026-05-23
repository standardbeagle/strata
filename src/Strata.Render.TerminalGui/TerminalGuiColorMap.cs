using Strata.Properties.Styling;
using TgColor = Terminal.Gui.Color;

namespace Strata.Render.TerminalGui;

/// <summary>
/// Maps Strata <see cref="ColorValue"/> onto Terminal.Gui <see cref="TgColor"/>.
/// </summary>
/// <remarks>
/// Terminal.Gui owns terminal capability detection and color downgrade — we hand it a 24-bit
/// color and let its driver degrade as the terminal requires. Alpha is not represented in the
/// terminal: a non-opaque color is treated as "use the fallback" so the cell keeps a sensible
/// default rather than rendering an alpha-blended value the driver cannot express.
/// </remarks>
internal static class TerminalGuiColorMap
{
    /// <summary>
    /// Convert a Strata color to a Terminal.Gui color, substituting <paramref name="fallback"/>
    /// when the source is not opaque (alpha is meaningless on a terminal cell).
    /// </summary>
    public static TgColor ToTerminalGui(ColorValue value, TgColor fallback)
        => value.A == 0 ? fallback : new TgColor(value.R, value.G, value.B);
}
