using FluentAssertions;
using Strata.Dsl;
using Strata.Dsl.TerminalGui;
using Xunit;

namespace Strata.Dsl.TerminalGui.Tests;

public sealed class StrataUiEventTests
{
    [Fact]
    public void Event_carries_store_element_and_value()
    {
        var store = StrataStore.FromJson("{}");
        var element = new StrataElement("Button");

        var ev = new StrataUiEvent(store, element, "clicked");

        ev.Store.Should().BeSameAs(store);
        ev.Element.Should().BeSameAs(element);
        ev.Value.Should().Be("clicked");
    }
}
