using System.Text.Json.Nodes;
using FluentAssertions;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class StrataStoreTests
{
    private static StrataStore NewStore() => StrataStore.FromJson("{}");

    [Fact]
    public void Set_creates_intermediate_objects()
    {
        var store = NewStore();

        store.Set("$.hosts.google.latency", 12);

        store.State["hosts"]!["google"]!["latency"]!.GetValue<int>().Should().Be(12);
    }

    [Fact]
    public void Set_overwrites_existing_value()
    {
        var store = StrataStore.FromJson("""{ "latency": 1 }""");

        store.Set("$.latency", 99);

        store.State["latency"]!.GetValue<int>().Should().Be(99);
    }

    [Fact]
    public void Append_creates_array_and_adds_values()
    {
        var store = NewStore();

        store.Append("$.history", 1);
        store.Append("$.history", 2);

        var array = store.State["history"]!.AsArray();
        array.Should().HaveCount(2);
        array[0]!.GetValue<int>().Should().Be(1);
        array[1]!.GetValue<int>().Should().Be(2);
    }

    [Fact]
    public void Append_caps_from_the_front()
    {
        var store = NewStore();

        for (var i = 1; i <= 5; i++)
        {
            store.Append("$.history", i, cap: 3);
        }

        var array = store.State["history"]!.AsArray();
        array.Should().HaveCount(3);
        array[0]!.GetValue<int>().Should().Be(3);
        array[2]!.GetValue<int>().Should().Be(5);
    }

    [Fact]
    public void Mutations_raise_changed()
    {
        var store = NewStore();
        var count = 0;
        store.Changed += (_, _) => count++;

        store.Set("$.a", 1);
        store.Append("$.b", 2);

        count.Should().Be(2);
    }

    [Fact]
    public void FromJson_rejects_non_object()
    {
        var act = () => StrataStore.FromJson("[1,2,3]");
        act.Should().Throw<FormatException>();
    }
}
