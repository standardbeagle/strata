namespace Strata.Phase0.Tests;

using System.Management.Automation;
using Strata.Adapters.PSObject;

public sealed class PsObjectAdapterSelectorTests
{
    private sealed record Row(int Id, string Name, double Cpu, bool HasExited);

    [Fact]
    public void Class_selector_supplies_classes_per_node()
    {
        var adapter = new PsObjectTreeAdapter(
            classes: PsObjectTreeAdapter.ClassesFromRules(
                ("high-cpu", ps => ps.Properties["Cpu"]?.Value is double d && d > 50),
                ("zombie", ps => ps.Properties["HasExited"]?.Value is bool b && b)));

        var hot = global::System.Management.Automation.PSObject.AsPSObject(
            new Row(1, "chrome", 75.0, false));
        var dead = global::System.Management.Automation.PSObject.AsPSObject(
            new Row(2, "ghost", 0.0, true));

        adapter.Wrap(hot).Classes.Should().BeEquivalentTo(new[] { "high-cpu" });
        adapter.Wrap(dead).Classes.Should().BeEquivalentTo(new[] { "zombie" });
    }

    [Fact]
    public void Pseudo_state_selector_supplies_states()
    {
        var focused = global::System.Management.Automation.PSObject.AsPSObject(
            new Row(1, "chrome", 1, false));
        var unfocused = global::System.Management.Automation.PSObject.AsPSObject(
            new Row(2, "vim", 1, false));

        var adapter = new PsObjectTreeAdapter(
            pseudoStates: src => src == focused ? new[] { "focused" } : Array.Empty<string>());

        adapter.Wrap(focused).PseudoStates.Should().BeEquivalentTo(new[] { "focused" });
        adapter.Wrap(unfocused).PseudoStates.Should().BeEmpty();
    }

    [Fact]
    public void Custom_kind_selector_overrides_default()
    {
        var adapter = new PsObjectTreeAdapter(kind: _ => "MyKind");
        var ps = global::System.Management.Automation.PSObject.AsPSObject(new Row(1, "x", 0, false));
        adapter.Wrap(ps).Kind.Should().Be("MyKind");
    }

    [Fact]
    public void Custom_id_selector_overrides_default()
    {
        var adapter = new PsObjectTreeAdapter(id: _ => "fixed-id");
        var ps = global::System.Management.Automation.PSObject.AsPSObject(new Row(99, "x", 0, false));
        adapter.Wrap(ps).Id.Should().Be("fixed-id");
    }

    [Fact]
    public void Classes_from_property_splits_space_separated()
    {
        var adapter = new PsObjectTreeAdapter(
            classes: PsObjectTreeAdapter.ClassesFromProperty("Classes"));

        var ps = global::System.Management.Automation.PSObject.AsPSObject(new { Classes = "a b c" });
        adapter.Wrap(ps).Classes.Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    [Fact]
    public void Options_factory_wires_all_hooks()
    {
        var adapter = PsObjectTreeAdapter.Create(new PsObjectTreeAdapter.Options
        {
            Kind = _ => "Row",
            Id = src => src.Properties["Id"]?.Value?.ToString(),
            Classes = PsObjectTreeAdapter.ClassesFromRules(
                ("dead", ps => ps.Properties["HasExited"]?.Value is bool b && b)),
        });

        var ps = global::System.Management.Automation.PSObject.AsPSObject(new Row(7, "x", 0, true));
        var node = adapter.Wrap(ps);
        node.Kind.Should().Be("Row");
        node.Id.Should().Be("7");
        node.Classes.Should().BeEquivalentTo(new[] { "dead" });
    }

    [Fact]
    public void Defaults_remain_for_unconfigured_hooks()
    {
        var ps = global::System.Management.Automation.PSObject.AsPSObject(new Row(42, "init", 0, false));
        ps.TypeNames.Insert(0, "System.Diagnostics.Process");

        var node = new PsObjectTreeAdapter().Wrap(ps);
        node.Kind.Should().Be("Process");
        node.Id.Should().Be("42");
        node.Classes.Should().BeEmpty();
        node.PseudoStates.Should().BeEmpty();
    }
}
