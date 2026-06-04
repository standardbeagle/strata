using FluentAssertions;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class BoundClassTests
{
    [Fact]
    public void SetBoundClasses_adds_bound_on_top_of_static()
    {
        var el = new StrataElement("Text", classes: new[] { "metric" });

        el.SetBoundClasses(new[] { "down" });

        el.Classes.Should().BeEquivalentTo(new[] { "metric", "down" });
    }

    [Fact]
    public void SetBoundClasses_replaces_previous_bound_but_keeps_static()
    {
        var el = new StrataElement("Text", classes: new[] { "metric" });

        el.SetBoundClasses(new[] { "up" });
        el.SetBoundClasses(new[] { "down" });

        el.Classes.Should().BeEquivalentTo(new[] { "metric", "down" });
        el.Classes.Should().NotContain("up");
    }

    [Fact]
    public void SetBoundClasses_empty_resets_to_static_only()
    {
        var el = new StrataElement("Text", classes: new[] { "metric" });
        el.SetBoundClasses(new[] { "up" });

        el.SetBoundClasses(System.Array.Empty<string>());

        el.Classes.Should().BeEquivalentTo(new[] { "metric" });
    }

    [Fact]
    public void Binder_applies_bind_class_from_store()
    {
        var store = StrataStore.FromJson("""{ "status": "down" }""");
        var el = new StrataElement("Text",
            classes: new[] { "metric" },
            attributes: new Dictionary<string, object?> { ["bind-class"] = "$.status" });

        StrataBinder.Apply(el, store.State);

        el.Classes.Should().BeEquivalentTo(new[] { "metric", "down" });
    }

    [Fact]
    public void Binder_rebind_swaps_the_status_class()
    {
        var store = StrataStore.FromJson("""{ "status": "up" }""");
        var el = new StrataElement("Text",
            classes: new[] { "metric" },
            attributes: new Dictionary<string, object?> { ["bind-class"] = "$.status" });

        StrataBinder.Apply(el, store.State);
        el.Classes.Should().Contain("up");

        store.Set("$.status", "down");
        StrataBinder.Apply(el, store.State);

        el.Classes.Should().BeEquivalentTo(new[] { "metric", "down" });
    }
}
