using FluentAssertions;
using Strata.Dsl;
using Strata.Interaction;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class PseudoStateTests
{
    [Fact]
    public void Element_is_pseudo_state_mutable()
    {
        var el = new StrataElement("Button");
        ((object)el).Should().BeAssignableTo<IPseudoStateMutable>();
    }

    [Fact]
    public void Add_and_remove_pseudo_state_toggles_membership_and_reports_change()
    {
        var el = new StrataElement("Button");
        var mutable = (IPseudoStateMutable)el;

        mutable.AddPseudoState("focused").Should().BeTrue();
        el.PseudoStates.Should().Contain("focused");
        mutable.AddPseudoState("focused").Should().BeFalse();

        mutable.RemovePseudoState("focused").Should().BeTrue();
        el.PseudoStates.Should().NotContain("focused");
        mutable.RemovePseudoState("focused").Should().BeFalse();
    }
}
