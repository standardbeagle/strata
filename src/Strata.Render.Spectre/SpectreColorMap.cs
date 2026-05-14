using Spectre.Console;
using Strata.Properties.Styling;

namespace Strata.Render.Spectre;

/// <summary>
/// Maps Strata <see cref="ColorValue"/> onto Spectre.Console <see cref="Color"/>.
/// </summary>
/// <remarks>
/// Spectre owns terminal capability detection and color downgrade — we hand it a
/// 24-bit color and let it degrade to 256/16/8-color or monochrome as the terminal
/// requires. Alpha is not represented in the terminal; a non-opaque color is treated
/// as "no color" so the cell keeps its existing background.
/// </remarks>
internal static class SpectreColorMap
{
    public static Color? ToSpectre(ColorValue value)
    {
        if (value.A == 0)
        {
            return null;
        }

        return new Color(value.R, value.G, value.B);
    }
}
