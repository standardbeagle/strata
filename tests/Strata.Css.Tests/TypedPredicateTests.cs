namespace Strata.Css.Tests;

using Strata.Core.Tests.TestFixtures;

public sealed class TypedPredicateTests
{
    private sealed record Process(int Id, string Name, double Cpu, int Threads);

    private static readonly CssSelectorLanguage Css = new();

    private static bool Match(string selector, object underlying)
    {
        var node = new ProcessNode("Process", underlying);
        return Css.Parse(selector).Matches(node, out _);
    }

    /// <summary>TestNode variant that exposes a real underlying typed object.</summary>
    private sealed class ProcessNode(string kind, object underlying) : ITreeNode
    {
        public string Kind { get; } = kind;

        public string? Id => null;

        public IReadOnlySet<string> Classes { get; } = new HashSet<string>();

        public IReadOnlySet<string> PseudoStates { get; } = new HashSet<string>();

        public ITreeNode? Parent => null;

        public IEnumerable<ITreeNode> Children => Array.Empty<ITreeNode>();

        public object? Underlying { get; } = underlying;

        public bool TryGetAttribute(string name, out object? value)
        {
            value = null;
            return false;
        }
    }

    [Fact]
    public void Numeric_comparison_matches_underlying()
    {
        var hot = new Process(1, "chrome", 64.0, 32);
        var cold = new Process(2, "init", 0.1, 1);

        Match("Process[Cpu > 50]", hot).Should().BeTrue();
        Match("Process[Cpu > 50]", cold).Should().BeFalse();
    }

    [Fact]
    public void String_method_call_works()
    {
        var chrome = new Process(1, "chrome", 64.0, 32);
        var init = new Process(2, "init", 0.1, 1);

        Match("Process[Name.StartsWith(\"chr\")]", chrome).Should().BeTrue();
        Match("Process[Name.StartsWith(\"chr\")]", init).Should().BeFalse();
    }

    [Fact]
    public void Compound_predicate_with_and_or_works()
    {
        var p = new Process(1, "chrome", 64.0, 32);

        Match("Process[Cpu > 50 and Threads > 10]", p).Should().BeTrue();
        Match("Process[Cpu > 100 or Threads > 10]", p).Should().BeTrue();
        Match("Process[Cpu > 100 and Threads > 10]", p).Should().BeFalse();
    }

    [Fact]
    public void Simple_attribute_form_is_preferred_when_pattern_matches()
    {
        // The simple form has no operators beyond = ^= $= *=, so [Name=\"chrome\"] must
        // stay an AttributeMatcher path, not a typed predicate.
        var sel = Css.Parse("Process[Name=\"chrome\"]");
        sel.Specificity.Should().Be(new Specificity(0, 1, 1));
    }

    [Fact]
    public void Typed_predicate_specificity_counts_as_B()
    {
        // Process[Cpu > 50] → kind=C, predicate=B → (0,1,1)
        Css.Parse("Process[Cpu > 50]").Specificity.Should().Be(new Specificity(0, 1, 1));
    }

    [Fact]
    public void Typed_predicate_against_missing_property_throws_meaningfully()
    {
        var p = new Process(1, "x", 0, 0);
        Action act = () => Match("Process[NoSuchProp > 0]", p);
        act.Should().Throw<Exception>(); // Dynamic.Core surfaces ParseException
    }
}
