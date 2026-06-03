using FluentAssertions;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class StrataNodeTests
{
    [Fact]
    public void Create_builds_detached_element_with_kind_classes_attributes()
    {
        var node = StrataNode.Create(
            "Card",
            id: "host1",
            classes: new[] { "host", "up" },
            attributes: new Dictionary<string, object?> { ["text"] = "google.com" });

        node.Kind.Should().Be("Card");
        node.Id.Should().Be("host1");
        node.Classes.Should().BeEquivalentTo(new[] { "host", "up" });
        node.TryGetAttribute("text", out var t).Should().BeTrue();
        t.Should().Be("google.com");
        node.Parent.Should().BeNull();
    }

    [Fact]
    public void Create_normalizes_blank_id_to_null()
    {
        StrataNode.Create("Text", id: "   ").Id.Should().BeNull();
        StrataNode.Create("Text").Id.Should().BeNull();
    }
}
