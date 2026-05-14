namespace Strata.Css.Tests;

using Strata.Core.Tests.TestFixtures;

public sealed class FunctionalPseudoTests
{
    private static readonly CssSelectorLanguage Css = new();

    private static bool Match(string selector, ITreeNode node)
        => Css.Parse(selector).Matches(node, out _);

    [Fact]
    public void Not_negates_inner_match()
    {
        var alive = new TestNode("Process");
        var zombie = new TestNode("Process", classes: new[] { "zombie" });

        Match("Process:not(.zombie)", alive).Should().BeTrue();
        Match("Process:not(.zombie)", zombie).Should().BeFalse();
    }

    [Fact]
    public void Not_accepts_selector_list()
    {
        var a = new TestNode("Process", classes: new[] { "zombie" });
        var b = new TestNode("Process", classes: new[] { "stopped" });
        var c = new TestNode("Process");

        Match("Process:not(.zombie, .stopped)", a).Should().BeFalse();
        Match("Process:not(.zombie, .stopped)", b).Should().BeFalse();
        Match("Process:not(.zombie, .stopped)", c).Should().BeTrue();
    }

    [Fact]
    public void Is_matches_any_inner()
    {
        var p = new TestNode("Process");
        var t = new TestNode("Thread");
        var f = new TestNode("File");

        Match(":is(Process, Thread)", p).Should().BeTrue();
        Match(":is(Process, Thread)", t).Should().BeTrue();
        Match(":is(Process, Thread)", f).Should().BeFalse();
    }

    [Fact]
    public void Where_matches_like_is_but_has_zero_specificity()
    {
        var p = new TestNode("Process");
        Match(":where(Process)", p).Should().BeTrue();

        Css.Parse(":where(Process, Thread)").Specificity.Should().Be(Specificity.Zero);
        Css.Parse(":is(Process, Thread)").Specificity.Should().Be(new Specificity(0, 0, 1));
    }

    [Fact]
    public void Has_matches_when_any_descendant_matches()
    {
        var parent = new TestNode("Window");
        var child = new TestNode("Process", classes: new[] { "high-cpu" });
        parent.Add(child);
        var empty = new TestNode("Window");

        Match("Window:has(.high-cpu)", parent).Should().BeTrue();
        Match("Window:has(.high-cpu)", empty).Should().BeFalse();
    }

    [Fact]
    public void Nth_child_constant_matches_position()
    {
        var parent = new TestNode("Window");
        var rows = new[] { new TestNode("Row"), new TestNode("Row"), new TestNode("Row") };
        foreach (var r in rows)
        {
            parent.Add(r);
        }

        Match(":nth-child(1)", rows[0]).Should().BeTrue();
        Match(":nth-child(2)", rows[1]).Should().BeTrue();
        Match(":nth-child(3)", rows[2]).Should().BeTrue();
        Match(":nth-child(1)", rows[1]).Should().BeFalse();
    }

    [Theory]
    [InlineData("odd", 0, true)]
    [InlineData("odd", 1, false)]
    [InlineData("odd", 2, true)]
    [InlineData("odd", 3, false)]
    [InlineData("even", 0, false)]
    [InlineData("even", 1, true)]
    [InlineData("even", 2, false)]
    [InlineData("even", 3, true)]
    [InlineData("2n", 0, false)]   // index 1
    [InlineData("2n", 1, true)]    // index 2
    [InlineData("2n+1", 0, true)]  // index 1
    [InlineData("3n+1", 0, true)]  // index 1
    [InlineData("3n+1", 3, true)]  // index 4
    [InlineData("3n+1", 1, false)]
    public void Nth_child_an_plus_b(string arg, int childIndex, bool expected)
    {
        var parent = new TestNode("Window");
        var rows = Enumerable.Range(0, 6).Select(_ => new TestNode("Row")).ToArray();
        foreach (var r in rows)
        {
            parent.Add(r);
        }

        Match($":nth-child({arg})", rows[childIndex]).Should().Be(expected);
    }

    [Fact]
    public void Specificity_for_not_is_max_of_inner()
    {
        // Process:not(#xyz) → C=1 (Process) + A=1 (id) = (1,0,1)
        Css.Parse("Process:not(#xyz)").Specificity.Should().Be(new Specificity(1, 0, 1));
        // Process:not(.a, #xyz) → C=1 + max((0,1,0), (1,0,0)) = (1,0,1)
        Css.Parse("Process:not(.a, #xyz)").Specificity.Should().Be(new Specificity(1, 0, 1));
    }

    [Fact]
    public void Specificity_for_has_uses_inner()
    {
        // Process:has(#xyz) → C=1 + (1,0,0) = (1,0,1)
        Css.Parse("Process:has(#xyz)").Specificity.Should().Be(new Specificity(1, 0, 1));
    }

    [Fact]
    public void Nested_functional_pseudos_compose()
    {
        var n = new TestNode("Process", classes: new[] { "alive" });
        Match(":not(:is(.zombie, .stopped))", n).Should().BeTrue();

        var zomb = new TestNode("Process", classes: new[] { "zombie" });
        Match(":not(:is(.zombie, .stopped))", zomb).Should().BeFalse();
    }

    [Fact]
    public void Unknown_functional_pseudo_throws()
    {
        Action act = () => Css.Parse(":foo(.bar)");
        act.Should().Throw<FormatException>()
            .WithMessage("*Unknown functional pseudo-class*");
    }
}
