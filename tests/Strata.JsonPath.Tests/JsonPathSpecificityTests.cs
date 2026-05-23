using Strata.JsonPath;

namespace Strata.JsonPath.Tests;

public class JsonPathSpecificityTests
{
    private static readonly JsonPathSelectorLanguage Lang = new();

    [Fact]
    public void Named_steps_count_toward_the_A_axis()
    {
        // $.users.0 -> name + name (index written as name here is still a name selector)
        var selector = Lang.Parse("$.users.settings");
        selector.Specificity.Should().Be(new Specificity(2, 0, 0));
    }

    [Fact]
    public void Filters_count_toward_the_B_axis()
    {
        var selector = Lang.Parse("$.users[?@.role == 'admin']");
        // 'users' name (A) + filter (B).
        selector.Specificity.Should().Be(new Specificity(1, 1, 0));
    }

    [Fact]
    public void Wildcards_and_slices_count_toward_the_C_axis()
    {
        Lang.Parse("$.users[*]").Specificity.Should().Be(new Specificity(1, 0, 1));
        Lang.Parse("$.users[0:2]").Specificity.Should().Be(new Specificity(1, 0, 1));
    }

    [Fact]
    public void More_named_steps_outrank_a_wildcard()
    {
        var named = Lang.Parse("$.users.settings").Specificity;
        var wild = Lang.Parse("$.users[*]").Specificity;
        named.Should().BeGreaterThan(wild);
    }
}
