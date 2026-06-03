using FluentAssertions;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class StrataBinderTests
{
    [Fact]
    public void Apply_binds_scalar_to_text_attribute()
    {
        var store = StrataStore.FromJson("""{ "latency": 12 }""");
        var text = new StrataElement("Text", attributes: new Dictionary<string, object?> { ["bind-text"] = "$.latency" });

        StrataBinder.Apply(text, store.State);

        text.TryGetAttribute("text", out var value).Should().BeTrue();
        value.Should().Be("12");
    }

    [Fact]
    public void Apply_binds_array_to_data_attribute()
    {
        var store = StrataStore.FromJson("""{ "history": [1, 2, 3] }""");
        var graph = new StrataElement("Graph", attributes: new Dictionary<string, object?> { ["bind-data"] = "$.history" });

        StrataBinder.Apply(graph, store.State);

        graph.TryGetAttribute("data", out var data).Should().BeTrue();
        data.Should().BeOfType<double[]>().Which.Should().Equal(1.0, 2.0, 3.0);
    }

    [Fact]
    public void Apply_recurses_into_children()
    {
        var store = StrataStore.FromJson("""{ "host": "google.com", "history": [5, 10] }""");
        var root = new StrataElement("Stack");
        var title = new StrataElement("Text", attributes: new Dictionary<string, object?> { ["bind-text"] = "$.host" });
        var graph = new StrataElement("Graph", attributes: new Dictionary<string, object?> { ["bind-data"] = "$.history" });
        root.Add(title).Add(graph);

        StrataBinder.Apply(root, store.State);

        title.TryGetAttribute("text", out var t).Should().BeTrue();
        t.Should().Be("google.com");
        graph.TryGetAttribute("data", out var d).Should().BeTrue();
        d.Should().BeOfType<double[]>().Which.Should().Equal(5.0, 10.0);
    }

    [Fact]
    public void Apply_leaves_attribute_unset_when_path_matches_nothing()
    {
        var store = StrataStore.FromJson("""{ }""");
        var text = new StrataElement("Text", attributes: new Dictionary<string, object?> { ["bind-text"] = "$.missing" });

        StrataBinder.Apply(text, store.State);

        text.TryGetAttribute("text", out _).Should().BeFalse();
    }

    [Fact]
    public void Apply_throws_on_invalid_jsonpath()
    {
        var store = StrataStore.FromJson("{}");
        var text = new StrataElement("Text", attributes: new Dictionary<string, object?> { ["bind-text"] = "not a path" });

        var act = () => StrataBinder.Apply(text, store.State);
        act.Should().Throw<Exception>();
    }
}
