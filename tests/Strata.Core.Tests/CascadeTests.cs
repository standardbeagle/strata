namespace Strata.Core.Tests;

using Strata.Core.Tests.TestFixtures;

public sealed class CascadeTests
{
    private static (Cascade cascade, PropertyRegistry props) NewEngine()
    {
        var props = new PropertyRegistry();
        props.Register(new StringPropertyDescriptor("color", initial: "black", inherits: true));
        props.Register(new StringPropertyDescriptor("font-size", initial: "12", inherits: false));
        return (new Cascade(props), props);
    }

    private static IStylesheet StylesheetOf(params (ISelector selector, Declaration[] decls)[] rules)
    {
        var ordered = new List<IRule>();
        for (var i = 0; i < rules.Length; i++)
        {
            ordered.Add(new Rule(rules[i].selector, rules[i].decls, i));
        }

        return new Stylesheet(ordered, version: 1);
    }

    private static Declaration Decl(string property, string value, bool important = false)
        => new(property, new StringValue(value), important);

    [Fact]
    public void Single_rule_winner_is_returned()
    {
        var root = new TestNode("Process");
        var (engine, _) = NewEngine();

        var stylesheet = StylesheetOf(
            (new KindSelector("Process", new Specificity(0, 0, 1)),
             new[] { Decl("color", "red") }));

        var result = engine.Compute(root, stylesheet);

        result.GetComputed<StringValue>(root, "color").Text.Should().Be("red");
        result.GetOrigin(root, "color").Kind.Should().Be(OriginKind.Declared);
    }

    [Fact]
    public void Higher_specificity_wins_over_lower()
    {
        var root = new TestNode("Process", classes: new[] { "zombie" });
        var (engine, _) = NewEngine();

        var stylesheet = StylesheetOf(
            // Type selector: (0,0,1).
            (new KindSelector("Process", new Specificity(0, 0, 1)),
             new[] { Decl("color", "red") }),
            // Type + class: (0,1,1).
            (new KindSelector("Process", new Specificity(0, 1, 1), requiredClass: "zombie"),
             new[] { Decl("color", "blue") }));

        engine.Compute(root, stylesheet)
            .GetComputed<StringValue>(root, "color").Text.Should().Be("blue");
    }

    [Fact]
    public void Important_beats_higher_specificity()
    {
        var root = new TestNode("Process", classes: new[] { "zombie" });
        var (engine, _) = NewEngine();

        var stylesheet = StylesheetOf(
            (new KindSelector("Process", new Specificity(0, 0, 1)),
             new[] { Decl("color", "red", important: true) }),
            (new KindSelector("Process", new Specificity(0, 1, 1), requiredClass: "zombie"),
             new[] { Decl("color", "blue") }));

        engine.Compute(root, stylesheet)
            .GetComputed<StringValue>(root, "color").Text.Should().Be("red");
    }

    [Fact]
    public void Later_source_order_wins_on_tie()
    {
        var root = new TestNode("Process");
        var (engine, _) = NewEngine();

        var stylesheet = StylesheetOf(
            (new KindSelector("Process", new Specificity(0, 0, 1)),
             new[] { Decl("color", "red") }),
            (new KindSelector("Process", new Specificity(0, 0, 1)),
             new[] { Decl("color", "blue") }));

        engine.Compute(root, stylesheet)
            .GetComputed<StringValue>(root, "color").Text.Should().Be("blue");
    }

    [Fact]
    public void Inheritable_property_walks_to_ancestor()
    {
        var root = new TestNode("Window");
        var child = new TestNode("Process");
        root.Add(child);

        var (engine, _) = NewEngine();

        var stylesheet = StylesheetOf(
            (new KindSelector("Window", new Specificity(0, 0, 1)),
             new[] { Decl("color", "red") }));

        var result = engine.Compute(root, stylesheet);
        result.GetComputed<StringValue>(child, "color").Text.Should().Be("red");
        result.GetOrigin(child, "color").Kind.Should().Be(OriginKind.Inherited);
        result.GetOrigin(child, "color").InheritedFrom.Should().Be(root);
    }

    [Fact]
    public void Non_inheritable_property_falls_back_to_initial()
    {
        var root = new TestNode("Window");
        var child = new TestNode("Process");
        root.Add(child);

        var (engine, _) = NewEngine();

        var stylesheet = StylesheetOf(
            (new KindSelector("Window", new Specificity(0, 0, 1)),
             new[] { Decl("font-size", "20") }));

        var result = engine.Compute(root, stylesheet);
        result.GetComputed<StringValue>(child, "font-size").Text.Should().Be("12");
        result.GetOrigin(child, "font-size").Kind.Should().Be(OriginKind.Initial);
    }

    [Fact]
    public void Matched_rules_are_returned_in_winner_first_order()
    {
        var root = new TestNode("Process", classes: new[] { "zombie" });
        var (engine, _) = NewEngine();

        var lowSpec = new KindSelector("Process", new Specificity(0, 0, 1));
        var highSpec = new KindSelector("Process", new Specificity(0, 1, 1), requiredClass: "zombie");

        var stylesheet = StylesheetOf(
            (lowSpec, new[] { Decl("color", "red") }),
            (highSpec, new[] { Decl("color", "blue") }));

        var result = engine.Compute(root, stylesheet);
        var matched = result.GetMatchedRules(root);
        matched.Should().HaveCount(2);
        matched[0].Rule.Selector.Should().BeSameAs(highSpec);
        matched[1].Rule.Selector.Should().BeSameAs(lowSpec);
    }

    [Fact]
    public void Update_throws_not_implemented_in_phase_0()
    {
        var (engine, _) = NewEngine();
        var root = new TestNode("Process");
        var ss = StylesheetOf();
        var result = engine.Compute(root, ss);

        Action act = () => engine.Update(result, Array.Empty<TreeChange>());
        act.Should().Throw<NotImplementedException>();
    }
}
