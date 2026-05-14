namespace Strata.Css.Tests;

using Strata.Core.Tests.TestFixtures;

public sealed class SelectorParseTests
{
    private static readonly CssSelectorLanguage Css = new();

    private static ISelector Parse(string s) => Css.Parse(s);

    [Theory]
    [InlineData("Process", 0, 0, 1)]
    [InlineData("*", 0, 0, 0)]
    [InlineData("#root", 1, 0, 0)]
    [InlineData(".zombie", 0, 1, 0)]
    [InlineData("Process.zombie", 0, 1, 1)]
    [InlineData("Process#chrome.high-cpu.zombie", 1, 2, 1)]
    [InlineData("Process[Name=\"chrome\"]", 0, 1, 1)]
    [InlineData("Process:focused", 0, 1, 1)]
    [InlineData("Window > Process:focused", 0, 1, 2)]
    [InlineData("Window Process[CPU]", 0, 1, 2)]
    public void Specificity_is_computed_per_spec(string source, int a, int b, int c)
    {
        Parse(source).Specificity.Should().Be(new Specificity(a, b, c));
    }

    [Fact]
    public void Empty_selector_throws()
    {
        Action act = () => Parse(" ");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Functional_pseudo_is_explicitly_unsupported_for_now()
    {
        Action act = () => Parse(":not(.x)");
        act.Should().Throw<FormatException>()
            .WithMessage("*Functional pseudo-class*");
    }

    [Fact]
    public void Double_colon_pseudo_element_throws()
    {
        Action act = () => Parse("Process::before");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Comma_list_is_split_into_multiple_selectors()
    {
        var list = Css.ParseList("Process, Thread, .zombie");
        list.Should().HaveCount(3);
        list[0].Specificity.Should().Be(new Specificity(0, 0, 1));
        list[1].Specificity.Should().Be(new Specificity(0, 0, 1));
        list[2].Specificity.Should().Be(new Specificity(0, 1, 0));
    }
}

public sealed class SelectorMatchTests
{
    private static readonly CssSelectorLanguage Css = new();

    private static bool Match(string selector, ITreeNode node)
    {
        return Css.Parse(selector).Matches(node, out _);
    }

    [Fact]
    public void Type_selector_matches_kind()
    {
        var n = new TestNode("Process");
        Match("Process", n).Should().BeTrue();
        Match("Thread", n).Should().BeFalse();
    }

    [Fact]
    public void Universal_matches_anything()
    {
        Match("*", new TestNode("Process")).Should().BeTrue();
        Match("*", new TestNode("Window")).Should().BeTrue();
    }

    [Fact]
    public void Id_selector_matches()
    {
        var n = new TestNode("Process", id: "chrome-1234");
        Match("#chrome-1234", n).Should().BeTrue();
        Match("#chrome-5678", n).Should().BeFalse();
    }

    [Fact]
    public void Class_selector_matches_when_class_present()
    {
        var n = new TestNode("Process", classes: new[] { "zombie", "high-cpu" });
        Match(".zombie", n).Should().BeTrue();
        Match(".high-cpu", n).Should().BeTrue();
        Match(".missing", n).Should().BeFalse();
    }

    [Theory]
    [InlineData("[Name]", true)]
    [InlineData("[Name=\"chrome\"]", true)]
    [InlineData("[Name=\"firefox\"]", false)]
    [InlineData("[Name^=\"chr\"]", true)]
    [InlineData("[Name^=\"fire\"]", false)]
    [InlineData("[Name$=\"ome\"]", true)]
    [InlineData("[Name$=\"fox\"]", false)]
    [InlineData("[Name*=\"rom\"]", true)]
    [InlineData("[Name*=\"xyz\"]", false)]
    [InlineData("[Missing]", false)]
    public void Attribute_operators_match(string sel, bool expected)
    {
        var n = new TestNode("Process", attributes: new Dictionary<string, object?> { ["Name"] = "chrome" });
        Match(sel, n).Should().Be(expected);
    }

    [Fact]
    public void Descendant_combinator_walks_ancestors()
    {
        var root = new TestNode("Window");
        var mid = new TestNode("Panel");
        var leaf = new TestNode("Process");
        root.Add(mid.Add(leaf));

        Match("Window Process", leaf).Should().BeTrue();
        Match("Panel Process", leaf).Should().BeTrue();
        Match("Application Process", leaf).Should().BeFalse();
    }

    [Fact]
    public void Child_combinator_requires_direct_parent()
    {
        var root = new TestNode("Window");
        var mid = new TestNode("Panel");
        var leaf = new TestNode("Process");
        root.Add(mid.Add(leaf));

        Match("Panel > Process", leaf).Should().BeTrue();
        Match("Window > Process", leaf).Should().BeFalse();
    }

    [Fact]
    public void Adjacent_sibling_matches_immediate_prev()
    {
        var root = new TestNode("Window");
        var a = new TestNode("Process", classes: new[] { "header" });
        var b = new TestNode("Process", classes: new[] { "row" });
        var c = new TestNode("Process", classes: new[] { "row" });
        root.Add(a).Add(b).Add(c);

        Match(".header + .row", b).Should().BeTrue();
        Match(".header + .row", c).Should().BeFalse();
    }

    [Fact]
    public void General_sibling_matches_any_prev()
    {
        var root = new TestNode("Window");
        var a = new TestNode("Process", classes: new[] { "header" });
        var b = new TestNode("Process");
        var c = new TestNode("Process", classes: new[] { "row" });
        root.Add(a).Add(b).Add(c);

        Match(".header ~ .row", c).Should().BeTrue();
        Match(".header ~ .row", b).Should().BeFalse();
    }

    [Fact]
    public void Focused_pseudo_state_matches()
    {
        var n = new TestNode("Process", pseudoStates: new[] { "focused" });
        Match("Process:focused", n).Should().BeTrue();
        Match("Process:focused", new TestNode("Process")).Should().BeFalse();
    }

    [Fact]
    public void Root_pseudo_matches_parentless()
    {
        var root = new TestNode("Window");
        var child = new TestNode("Process");
        root.Add(child);

        Match(":root", root).Should().BeTrue();
        Match(":root", child).Should().BeFalse();
    }

    [Fact]
    public void Empty_pseudo_matches_when_no_children()
    {
        var leaf = new TestNode("Process");
        var parent = new TestNode("Window");
        parent.Add(new TestNode("Process"));

        Match(":empty", leaf).Should().BeTrue();
        Match(":empty", parent).Should().BeFalse();
    }

    [Fact]
    public void First_last_only_child_pseudos_match()
    {
        var root = new TestNode("Window");
        var a = new TestNode("Process");
        var b = new TestNode("Process");
        root.Add(a).Add(b);

        Match(":first-child", a).Should().BeTrue();
        Match(":first-child", b).Should().BeFalse();
        Match(":last-child", b).Should().BeTrue();
        Match(":last-child", a).Should().BeFalse();
        Match(":only-child", a).Should().BeFalse();

        var solo = new TestNode("Window");
        var only = new TestNode("Process");
        solo.Add(only);
        Match(":only-child", only).Should().BeTrue();
    }

    [Fact]
    public void Compound_combines_kind_class_attribute_pseudo()
    {
        var n = new TestNode("Process",
            classes: new[] { "zombie" },
            pseudoStates: new[] { "focused" },
            attributes: new Dictionary<string, object?> { ["Name"] = "chrome" });

        Match("Process.zombie[Name=\"chrome\"]:focused", n).Should().BeTrue();
        Match("Process.zombie[Name=\"chrome\"]:hovered", n).Should().BeFalse();
        Match("Process.healthy[Name=\"chrome\"]:focused", n).Should().BeFalse();
    }

    [Fact]
    public void Find_walks_full_subtree()
    {
        var root = new TestNode("Window");
        var p1 = new TestNode("Process", id: "a");
        var p2 = new TestNode("Process", id: "b");
        var t = new TestNode("Thread");
        root.Add(p1).Add(t).Add(p2);

        var matches = Css.Parse("Process").Find(root).ToList();
        matches.Should().HaveCount(2);
        matches.Select(m => m.Node.Id).Should().BeEquivalentTo(new[] { "a", "b" });
    }
}
