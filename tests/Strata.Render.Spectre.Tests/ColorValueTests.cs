namespace Strata.Render.Spectre.Tests;

using Strata.Properties.Styling;

public sealed class ColorValueTests
{
    [Theory]
    [InlineData("#f00", 255, 0, 0, 255)]
    [InlineData("#ff0000", 255, 0, 0, 255)]
    [InlineData("#ff000080", 255, 0, 0, 128)]
    [InlineData("rgb(10,20,30)", 10, 20, 30, 255)]
    [InlineData("rgba(10,20,30,0.5)", 10, 20, 30, 128)]
    [InlineData("rgb(255, 255, 255)", 255, 255, 255, 255)]
    public void Parses_hex_and_rgb_forms(string source, int r, int g, int b, int a)
    {
        var c = ColorValue.Parse(source.AsSpan());
        c.R.Should().Be((byte)r);
        c.G.Should().Be((byte)g);
        c.B.Should().Be((byte)b);
        c.A.Should().Be((byte)a);
    }

    [Theory]
    [InlineData("black")]
    [InlineData("red")]
    [InlineData("brightcyan")]
    [InlineData("white")]
    public void Parses_named_colors(string name)
    {
        var act = () => ColorValue.Parse(name.AsSpan());
        act.Should().NotThrow();
    }

    [Fact]
    public void Transparent_is_zero_alpha()
    {
        ColorValue.Parse("transparent".AsSpan()).A.Should().Be(0);
        ColorValue.Transparent.IsOpaque.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("#xy")]
    [InlineData("#12345")]
    [InlineData("rgb(256,0,0)")]
    [InlineData("rgb(0,0)")]
    [InlineData("notacolor")]
    public void Rejects_invalid(string source)
    {
        var act = () => ColorValue.Parse(source.AsSpan());
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Styling_registry_has_all_nine_properties()
    {
        var registry = StylingProperties.CreateRegistry();
        registry.TryGet("color", out _).Should().BeTrue();
        registry.TryGet("background", out _).Should().BeTrue();
        registry.TryGet("font-weight", out _).Should().BeTrue();
        registry.TryGet("font-style", out _).Should().BeTrue();
        registry.TryGet("text-decoration", out _).Should().BeTrue();
        registry.TryGet("wrap", out _).Should().BeTrue();
        registry.TryGet("overflow", out _).Should().BeTrue();
        registry.TryGet("padding", out _).Should().BeTrue();
        registry.TryGet("margin", out _).Should().BeTrue();
    }

    [Fact]
    public void Color_inherits_background_does_not()
    {
        var registry = StylingProperties.CreateRegistry();
        registry.TryGet("color", out var color).Should().BeTrue();
        registry.TryGet("background", out var bg).Should().BeTrue();
        color.Inherits.Should().BeTrue();
        bg.Inherits.Should().BeFalse();
    }
}
