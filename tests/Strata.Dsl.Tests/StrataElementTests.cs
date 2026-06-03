using FluentAssertions;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class StrataElementTests
{
    [Fact]
    public void Constructor_sets_kind_id_and_filters_blank_classes()
    {
        var el = new StrataElement("Stack", id: "root", classes: new[] { "a", "", "  ", "b" });

        el.Kind.Should().Be("Stack");
        el.Id.Should().Be("root");
        el.Classes.Should().BeEquivalentTo(new[] { "a", "b" });
        el.PseudoStates.Should().BeEmpty();
        el.Parent.Should().BeNull();
        el.Children.Should().BeEmpty();
        el.Underlying.Should().BeNull();
    }

    [Fact]
    public void TryGetAttribute_returns_value_on_hit_and_false_on_miss()
    {
        var attrs = new Dictionary<string, object?> { ["text"] = "hello" };
        var el = new StrataElement("Text", attributes: attrs);

        el.TryGetAttribute("text", out var hit).Should().BeTrue();
        hit.Should().Be("hello");
        el.TryGetAttribute("missing", out var miss).Should().BeFalse();
        miss.Should().BeNull();
    }

    [Fact]
    public void Add_appends_child_and_sets_its_parent()
    {
        var parent = new StrataElement("Stack");
        var child = new StrataElement("Text");

        var returned = parent.Add(child);

        returned.Should().BeSameAs(parent);
        parent.Children.Should().ContainSingle().Which.Should().BeSameAs(child);
        child.Parent.Should().BeSameAs(parent);
    }

    [Fact]
    public void Identity_is_reference_based()
    {
        var a = new StrataElement("Text");
        var b = new StrataElement("Text");

        a.Equals(a).Should().BeTrue();
        a.Equals(b).Should().BeFalse();
        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }
}
