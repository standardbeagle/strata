using FluentAssertions;
using Strata.Dsl;
using Strata.Dsl.TerminalGui;
using Xunit;

namespace Strata.Dsl.TerminalGui.Tests;

public sealed class HostWiringTests
{
    [Fact]
    public void BindListItems_copies_a_bound_array_into_the_items_attribute()
    {
        var store = StrataStore.FromJson("""{ "rows": ["a", "b", "c"] }""");
        var list = new StrataElement("List", attributes: new Dictionary<string, object?> { ["bind-data"] = "$.rows" });
        var root = new StrataElement("Stack");
        root.Add(list);

        StrataInteractiveHost.BindListItems(root, store.State);

        list.TryGetAttribute("items", out var items).Should().BeTrue();
        ((IEnumerable<object?>)items!).Select(i => i?.ToString()).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void WriteFieldValue_sets_store_at_bind_value_path()
    {
        var store = StrataStore.FromJson("""{ "query": "" }""");
        var field = new StrataElement("TextField", attributes: new Dictionary<string, object?> { ["bind-value"] = "$.query" });

        StrataInteractiveHost.WriteFieldValue(field, store, "SELECT 1");

        store.State["query"]!.GetValue<string>().Should().Be("SELECT 1");
    }

    [Fact]
    public void WriteFieldValue_noop_without_bind_value()
    {
        var store = StrataStore.FromJson("{}");
        var field = new StrataElement("TextField");

        var act = () => StrataInteractiveHost.WriteFieldValue(field, store, "x");

        act.Should().NotThrow();
        store.State.Should().BeEmpty();
    }
}
