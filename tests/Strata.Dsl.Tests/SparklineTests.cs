using FluentAssertions;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class SparklineTests
{
    [Fact]
    public void Render_empty_series_is_empty_string()
    {
        Sparkline.Render(Array.Empty<double>()).Should().BeEmpty();
    }

    [Fact]
    public void Render_maps_min_to_lowest_and_max_to_highest_bar()
    {
        var s = Sparkline.Render(new double[] { 0, 100 });

        s.Should().HaveLength(2);
        s[0].Should().Be('▁');
        s[1].Should().Be('█');
    }

    [Fact]
    public void Render_flat_series_uses_lowest_bar()
    {
        Sparkline.Render(new double[] { 5, 5, 5 }).Should().Be("▁▁▁");
    }

    [Fact]
    public void Coerce_reads_mixed_numeric_enumerable()
    {
        var coerced = Sparkline.Coerce(new object[] { 1, 2.5, 3L });
        coerced.Should().Equal(1.0, 2.5, 3.0);
    }

    [Fact]
    public void Coerce_null_is_empty()
    {
        Sparkline.Coerce(null).Should().BeEmpty();
    }
}

public sealed class StrataTextTests
{
    [Fact]
    public void ForNode_graph_renders_sparkline_from_data()
    {
        var graph = new StrataElement("Graph");
        graph.SetAttribute("data", new double[] { 0, 100 });

        StrataText.ForNode(graph).Should().Be("▁█");
    }

    [Fact]
    public void ForNode_text_reads_text_attribute()
    {
        var text = new StrataElement("Text", attributes: new Dictionary<string, object?> { ["text"] = "hi" });
        StrataText.ForNode(text).Should().Be("hi");
    }

    [Fact]
    public void SetAttribute_overwrites_in_place()
    {
        var el = new StrataElement("Text");
        el.SetAttribute("text", "a");
        el.SetAttribute("text", "b");

        el.TryGetAttribute("text", out var v).Should().BeTrue();
        v.Should().Be("b");
    }
}
