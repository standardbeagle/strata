namespace Strata.Css.Tests;

using Strata.Core;
using Strata.Core.Tests.TestFixtures;

public sealed class StylesheetParseTests
{
    private static (CssStylesheetParser parser, PropertyRegistry props) NewParser()
    {
        var props = new PropertyRegistry();
        props.Register(new StringPropertyDescriptor("color", "black", inherits: true));
        props.Register(new StringPropertyDescriptor("background", "transparent", inherits: false));
        props.Register(new StringPropertyDescriptor("font-weight", "normal", inherits: true));
        return (new CssStylesheetParser(new CssSelectorLanguage(), props), props);
    }

    [Fact]
    public void Single_rule_parses()
    {
        var (parser, _) = NewParser();
        var ss = parser.Parse("Process { color: red; }");

        ss.Rules.Should().HaveCount(1);
        var rule = ss.Rules[0];
        rule.Selector.Specificity.Should().Be(new Specificity(0, 0, 1));
        rule.Declarations.Should().HaveCount(1);
        rule.Declarations[0].Property.Should().Be("color");
        rule.Declarations[0].Important.Should().BeFalse();
    }

    [Fact]
    public void Multiple_declarations_in_one_block()
    {
        var (parser, _) = NewParser();
        var ss = parser.Parse("Process { color: red; background: white; font-weight: bold; }");

        ss.Rules.Should().HaveCount(1);
        ss.Rules[0].Declarations.Should().HaveCount(3);
        ss.Rules[0].Declarations.Select(d => d.Property)
            .Should().BeEquivalentTo(new[] { "color", "background", "font-weight" });
    }

    [Fact]
    public void Important_flag_is_recognized()
    {
        var (parser, _) = NewParser();
        var ss = parser.Parse("Process { color: red !important; background: white; }");

        ss.Rules[0].Declarations[0].Important.Should().BeTrue();
        ss.Rules[0].Declarations[1].Important.Should().BeFalse();
    }

    [Fact]
    public void Comma_separated_selectors_become_separate_rules()
    {
        var (parser, _) = NewParser();
        var ss = parser.Parse("Process, Thread, .zombie { color: red; }");

        ss.Rules.Should().HaveCount(3);
        ss.Rules.Select(r => r.SourceOrder).Should().BeEquivalentTo(new[] { 0, 1, 2 });
        ss.Rules.Should().AllSatisfy(r =>
        {
            r.Declarations.Should().HaveCount(1);
            r.Declarations[0].Property.Should().Be("color");
        });
    }

    [Fact]
    public void Multiple_rules_separated_by_braces()
    {
        var (parser, _) = NewParser();
        var ss = parser.Parse("""
            Process { color: red; }
            Thread { color: blue; background: white; }
            .zombie:focused { color: green !important; }
            """);

        ss.Rules.Should().HaveCount(3);
        ss.Rules[0].Selector.Specificity.Should().Be(new Specificity(0, 0, 1));
        ss.Rules[2].Selector.Specificity.Should().Be(new Specificity(0, 2, 0));
        ss.Rules[2].Declarations[0].Important.Should().BeTrue();
    }

    [Fact]
    public void Block_comments_are_ignored()
    {
        var (parser, _) = NewParser();
        var ss = parser.Parse("""
            /* leading comment */
            Process { color: red; /* inline */ }
            /* trailing */
            """);

        ss.Rules.Should().HaveCount(1);
    }

    [Fact]
    public void Unknown_property_is_rejected()
    {
        var (parser, _) = NewParser();
        Action act = () => parser.Parse("Process { not-a-property: x; }");
        act.Should().Throw<FormatException>().WithMessage("*Unknown property*");
    }

    [Fact]
    public void Missing_brace_throws()
    {
        var (parser, _) = NewParser();
        Action act = () => parser.Parse("Process color: red;");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Unclosed_block_throws()
    {
        var (parser, _) = NewParser();
        Action act = () => parser.Parse("Process { color: red;");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Stylesheet_drives_full_cascade()
    {
        var (parser, props) = NewParser();
        var ss = parser.Parse("""
            Process { color: red; }
            Process.zombie { color: blue; }
            Process:focused { color: green !important; }
            """);

        var node = new TestNode("Process",
            classes: new[] { "zombie" },
            pseudoStates: new[] { "focused" });

        var cascade = new Cascade(props);
        var result = cascade.Compute(node, ss);
        result.GetComputed<StringValue>(node, "color").Text.Should().Be("green");
        result.GetOrigin(node, "color").Kind.Should().Be(OriginKind.Declared);
    }
}
