namespace Strata.Core.Tests;

public sealed class SpecificityTests
{
    [Fact]
    public void Zero_is_default()
    {
        Specificity.Zero.Should().Be(new Specificity(0, 0, 0));
    }

    [Fact]
    public void Addition_is_component_wise()
    {
        (new Specificity(1, 2, 3) + new Specificity(4, 5, 6))
            .Should().Be(new Specificity(5, 7, 9));
    }

    [Theory]
    [InlineData(0, 0, 1, 0, 0, 0)]   // C beats nothing
    [InlineData(0, 1, 0, 0, 0, 9)]   // B beats C regardless of C count
    [InlineData(1, 0, 0, 0, 9, 9)]   // A beats B and C regardless of count
    [InlineData(1, 0, 0, 0, 99, 99)] // A's weight is lexicographic, not weighted-sum
    public void Higher_specificity_compares_greater(int aA, int aB, int aC, int bA, int bB, int bC)
    {
        var a = new Specificity(aA, aB, aC);
        var b = new Specificity(bA, bB, bC);
        a.CompareTo(b).Should().BePositive();
        (a > b).Should().BeTrue();
        (b < a).Should().BeTrue();
    }

    [Fact]
    public void Equal_specificities_compare_zero()
    {
        new Specificity(1, 2, 3).CompareTo(new Specificity(1, 2, 3)).Should().Be(0);
    }

    [Fact]
    public void MatchContext_Empty_has_no_captures()
    {
        MatchContext.Empty.Captures.Count.Should().Be(0);
        MatchContext.Empty.Captures.TryGetValue("x", out var v).Should().BeFalse();
        v.Should().BeNull();
    }
}
