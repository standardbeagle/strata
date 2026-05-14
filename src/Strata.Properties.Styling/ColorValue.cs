using System.Globalization;

namespace Strata.Properties.Styling;

/// <summary>
/// An RGBA color, 8 bits per channel. Strata-native — projections (e.g. Spectre.Console)
/// map this onto their own color type and apply terminal downgrade logic.
/// </summary>
public readonly record struct ColorValue(byte R, byte G, byte B, byte A = 255) : IPropertyValue
{
    /// <inheritdoc/>
    public Type Type => typeof(ColorValue);

    /// <summary>True when this color is fully opaque.</summary>
    public bool IsOpaque => A == 255;

    /// <summary>Fully transparent (used as the <c>background</c> initial value).</summary>
    public static ColorValue Transparent { get; } = new(0, 0, 0, 0);

    /// <summary>
    /// Parse a color from <c>name</c>, <c>#rgb</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>,
    /// <c>rgb(r,g,b)</c>, or <c>rgba(r,g,b,a)</c>.
    /// </summary>
    public static ColorValue Parse(ReadOnlySpan<char> source)
    {
        var s = source.Trim();
        if (s.IsEmpty)
        {
            throw new FormatException("Empty color value.");
        }

        if (s[0] == '#')
        {
            return ParseHex(s[1..]);
        }

        if (s.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) && s[^1] == ')')
        {
            return ParseRgbFunction(s[5..^1], hasAlpha: true);
        }

        if (s.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && s[^1] == ')')
        {
            return ParseRgbFunction(s[4..^1], hasAlpha: false);
        }

        if (TryGetNamed(s, out var named))
        {
            return named;
        }

        throw new FormatException($"'{s}' is not a recognized color.");
    }

    private static ColorValue ParseHex(ReadOnlySpan<char> hex)
    {
        switch (hex.Length)
        {
            case 3:
                return new ColorValue(
                    (byte)(Nibble(hex[0]) * 17),
                    (byte)(Nibble(hex[1]) * 17),
                    (byte)(Nibble(hex[2]) * 17));
            case 6:
                return new ColorValue(
                    HexByte(hex[0], hex[1]),
                    HexByte(hex[2], hex[3]),
                    HexByte(hex[4], hex[5]));
            case 8:
                return new ColorValue(
                    HexByte(hex[0], hex[1]),
                    HexByte(hex[2], hex[3]),
                    HexByte(hex[4], hex[5]),
                    HexByte(hex[6], hex[7]));
            default:
                throw new FormatException($"Hex color '#{hex}' must have 3, 6, or 8 digits.");
        }
    }

    private static ColorValue ParseRgbFunction(ReadOnlySpan<char> args, bool hasAlpha)
    {
        Span<Range> ranges = stackalloc Range[hasAlpha ? 4 : 3];
        var count = args.Split(ranges, ',');
        var expected = hasAlpha ? 4 : 3;
        if (count != expected)
        {
            throw new FormatException(
                $"Expected {expected} components in {(hasAlpha ? "rgba" : "rgb")}() but found {count}.");
        }

        var r = ParseChannel(args[ranges[0]]);
        var g = ParseChannel(args[ranges[1]]);
        var b = ParseChannel(args[ranges[2]]);
        var a = hasAlpha ? ParseAlpha(args[ranges[3]]) : (byte)255;
        return new ColorValue(r, g, b, a);
    }

    private static byte ParseChannel(ReadOnlySpan<char> s)
    {
        var v = int.Parse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
        if (v is < 0 or > 255)
        {
            throw new FormatException($"Color channel '{s}' is out of range 0..255.");
        }

        return (byte)v;
    }

    private static byte ParseAlpha(ReadOnlySpan<char> s)
    {
        var v = double.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
        if (v is < 0 or > 1)
        {
            throw new FormatException($"Alpha '{s}' is out of range 0..1.");
        }

        return (byte)Math.Round(v * 255);
    }

    private static int Nibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => throw new FormatException($"'{c}' is not a hex digit."),
    };

    private static byte HexByte(char hi, char lo) => (byte)((Nibble(hi) << 4) | Nibble(lo));

    private static bool TryGetNamed(ReadOnlySpan<char> s, out ColorValue value)
    {
        // The 16 ANSI-standard names plus a few common extras. Projections own the rest.
        value = s switch
        {
            "black" => new ColorValue(0, 0, 0),
            "red" => new ColorValue(170, 0, 0),
            "green" => new ColorValue(0, 170, 0),
            "yellow" => new ColorValue(170, 85, 0),
            "blue" => new ColorValue(0, 0, 170),
            "magenta" => new ColorValue(170, 0, 170),
            "cyan" => new ColorValue(0, 170, 170),
            "white" => new ColorValue(170, 170, 170),
            "gray" or "grey" => new ColorValue(85, 85, 85),
            "brightred" => new ColorValue(255, 85, 85),
            "brightgreen" => new ColorValue(85, 255, 85),
            "brightyellow" => new ColorValue(255, 255, 85),
            "brightblue" => new ColorValue(85, 85, 255),
            "brightmagenta" => new ColorValue(255, 85, 255),
            "brightcyan" => new ColorValue(85, 255, 255),
            "brightwhite" => new ColorValue(255, 255, 255),
            "transparent" => Transparent,
            _ => default,
        };

        // default(ColorValue) is opaque black with A=0? No — default has A=0. Distinguish:
        // "transparent" legitimately maps to A=0; everything else we didn't match returns
        // default which is also A=0,R=0,G=0,B=0. Re-check explicitly.
        if (s.SequenceEqual("transparent"))
        {
            value = Transparent;
            return true;
        }

        return value != default;
    }
}
