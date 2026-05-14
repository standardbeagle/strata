namespace Strata.Core.Tests;

using System.Collections.Immutable;
using Strata.Core.Properties;

public sealed class PropertyDescriptorTests
{
    [Fact]
    public void String_descriptor_parses_quoted_and_bare()
    {
        var d = new StringPropertyDescriptor("color", "black", inherits: true);
        ((StringValue)d.Parse("red".AsSpan())).Text.Should().Be("red");
        ((StringValue)d.Parse("\"red\"".AsSpan())).Text.Should().Be("red");
        ((StringValue)d.Parse("  red  ".AsSpan())).Text.Should().Be("red");
    }

    [Fact]
    public void Number_descriptor_parses_invariant_culture()
    {
        var d = new NumberPropertyDescriptor("opacity", 1.0, inherits: false);
        ((NumberValue)d.Parse("0.5".AsSpan())).Value.Should().Be(0.5);
        ((NumberValue)d.Parse("10".AsSpan())).Value.Should().Be(10);
    }

    [Fact]
    public void Number_descriptor_rejects_invalid()
    {
        var d = new NumberPropertyDescriptor("opacity", 1.0, inherits: false);
        Action act = () => d.Parse("not-a-number".AsSpan());
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Length_descriptor_handles_auto_cells_percent_fr()
    {
        var d = new LengthPropertyDescriptor("width", LengthValue.Auto, inherits: false);
        d.Parse("auto".AsSpan()).Should().Be(LengthValue.Auto);
        ((LengthValue)d.Parse("10".AsSpan())).Should().Be(new LengthValue(10, LengthUnit.Cells));
        ((LengthValue)d.Parse("25%".AsSpan())).Should().Be(new LengthValue(25, LengthUnit.Percent));
        ((LengthValue)d.Parse("1fr".AsSpan())).Should().Be(new LengthValue(1, LengthUnit.Fr));
    }

    [Fact]
    public void Enum_descriptor_accepts_only_allowed_values()
    {
        var d = new EnumPropertyDescriptor(
            "display",
            initial: "block",
            inherits: false,
            allowedValues: new[] { "block", "flex", "grid", "none" });

        ((EnumValue)d.Parse("flex".AsSpan())).Value.Should().Be("flex");
        Action bad = () => d.Parse("inline".AsSpan());
        bad.Should().Throw<FormatException>().WithMessage("*not a valid value*");
    }

    [Fact]
    public void IdentList_descriptor_splits_on_commas_and_trims()
    {
        var d = new IdentListPropertyDescriptor("behavior");
        var v = (IdentListValue)d.Parse("kill, meter , highlight".AsSpan());
        v.Idents.Should().BeEquivalentTo(new[] { "kill", "meter", "highlight" });
    }

    [Fact]
    public void IdentList_descriptor_empty_returns_empty_value()
    {
        var d = new IdentListPropertyDescriptor("behavior");
        var v = (IdentListValue)d.Parse("".AsSpan());
        v.Should().Be(IdentListValue.Empty);
        v.Idents.Should().BeEmpty();
    }

    [Fact]
    public void IdentList_descriptor_rejects_empty_element()
    {
        var d = new IdentListPropertyDescriptor("behavior");
        Action act = () => d.Parse("kill,,meter".AsSpan());
        act.Should().Throw<FormatException>();
    }
}
