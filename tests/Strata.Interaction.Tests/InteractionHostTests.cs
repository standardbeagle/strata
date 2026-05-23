using Strata.Core;
using Strata.Css;

namespace Strata.Interaction.Tests;

public sealed class InteractionHostTests
{
    private static (Cascade cascade, IStylesheet sheet) Build(string css)
    {
        var props = InteractionProperties.CreateRegistry();
        var parser = new CssStylesheetParser(new CssSelectorLanguage(), props);
        var sheet = parser.Parse(css);
        return (new Cascade(props), sheet);
    }

    [Fact]
    public void Event_routes_to_registered_handler_for_matching_node()
    {
        var root = new InteractionTestNode("Row", pseudoStates: new[] { "focused" });
        var (cascade, sheet) = Build("Row:focused { command: \"navigate-down\" when \"key.j\"; }");
        var result = cascade.Compute(root, sheet);

        var fired = new List<ITreeNode>();
        var registry = new CommandRegistry();
        registry.Register("navigate-down", ctx => fired.Add(ctx.Node));

        using var input = new InputSource();
        using var host = new InteractionHost(input, registry);
        host.Reconcile(root, result);

        input.Push(new HostEvent.Key("key.j", default));

        fired.Should().ContainSingle().Which.Should().Be(root);
    }

    [Fact]
    public void Non_matching_event_name_does_not_fire()
    {
        var root = new InteractionTestNode("Row", pseudoStates: new[] { "focused" });
        var (cascade, sheet) = Build("Row:focused { command: \"navigate-down\" when \"key.j\"; }");
        var result = cascade.Compute(root, sheet);

        var fired = 0;
        var registry = new CommandRegistry();
        registry.Register("navigate-down", _ => fired++);

        using var input = new InputSource();
        using var host = new InteractionHost(input, registry);
        host.Reconcile(root, result);

        input.Push(new HostEvent.Key("key.k", default));

        fired.Should().Be(0);
    }

    [Fact]
    public void Command_bindings_are_additive_across_matched_rules()
    {
        // Two rules match the same node, each declaring a different command for the same event.
        // Additive semantics: BOTH fire, neither overrides.
        var root = new InteractionTestNode("Process", classes: new[] { "high-cpu" });
        var (cascade, sheet) = Build(
            "Process { command: \"a\" when \"tick\"; } " +
            ".high-cpu { command: \"b\" when \"tick\"; }");
        var result = cascade.Compute(root, sheet);

        var fired = new List<string>();
        var registry = new CommandRegistry();
        registry.Register("a", _ => fired.Add("a"));
        registry.Register("b", _ => fired.Add("b"));

        using var input = new InputSource();
        using var host = new InteractionHost(input, registry);
        host.Reconcile(root, result);

        host.ActiveSubscriptionCount.Should().Be(2);

        input.Push(new HostEvent.Tick("tick", TimeSpan.FromMilliseconds(16)));

        fired.Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void Subscription_persists_unchanged_across_reconcile_when_binding_still_present()
    {
        // Identity stability: a binding present in both cascade runs keeps its subscription;
        // the host must not dispose-and-recreate it (no spurious detach/re-attach).
        var root = new InteractionTestNode("Row", pseudoStates: new[] { "focused" });
        var (cascade, sheet) = Build("Row:focused { command: \"x\" when \"key.j\"; }");

        var registry = new CommandRegistry();
        var fired = 0;
        registry.Register("x", _ => fired++);

        using var input = new InputSource();
        using var host = new InteractionHost(input, registry);

        host.Reconcile(root, cascade.Compute(root, sheet));
        host.ActiveSubscriptionCount.Should().Be(1);

        // Re-cascade with the same resolved bindings.
        host.Reconcile(root, cascade.Compute(root, sheet));
        host.ActiveSubscriptionCount.Should().Be(1);

        // Still exactly one subscription → one fire, not two.
        input.Push(new HostEvent.Key("key.j", default));
        fired.Should().Be(1);
    }

    [Fact]
    public void Binding_removed_on_recascade_is_detached()
    {
        // The :focused state drops on the second cascade → the binding disappears → its
        // subscription is disposed (the detach-equivalent). The handler stops firing.
        var focused = new InteractionTestNode("Row", pseudoStates: new[] { "focused" });
        var unfocused = new InteractionTestNode("Row");
        var (cascade, sheet) = Build("Row:focused { command: \"x\" when \"key.j\"; }");

        var registry = new CommandRegistry();
        var fired = 0;
        registry.Register("x", _ => fired++);

        using var input = new InputSource();
        using var host = new InteractionHost(input, registry);

        host.Reconcile(focused, cascade.Compute(focused, sheet));
        host.ActiveSubscriptionCount.Should().Be(1);

        host.Reconcile(unfocused, cascade.Compute(unfocused, sheet));
        host.ActiveSubscriptionCount.Should().Be(0);

        input.Push(new HostEvent.Key("key.j", default));
        fired.Should().Be(0);
    }

    [Fact]
    public void Dispose_drops_all_subscriptions()
    {
        var root = new InteractionTestNode("Row", pseudoStates: new[] { "focused" });
        var (cascade, sheet) = Build("Row:focused { command: \"x\" when \"key.j\"; }");

        var registry = new CommandRegistry();
        var fired = 0;
        registry.Register("x", _ => fired++);

        var input = new InputSource();
        var host = new InteractionHost(input, registry);
        host.Reconcile(root, cascade.Compute(root, sheet));

        host.Dispose();
        input.Push(new HostEvent.Key("key.j", default));

        fired.Should().Be(0);
        input.Dispose();
    }

    [Fact]
    public void Bindings_on_descendant_nodes_are_collected()
    {
        var root = new InteractionTestNode("Table");
        var row = new InteractionTestNode("Row", pseudoStates: new[] { "focused" });
        root.Add(row);

        var (cascade, sheet) = Build("Row:focused { command: \"x\" when \"key.j\"; }");
        var result = cascade.Compute(root, sheet);

        var fired = new List<ITreeNode>();
        var registry = new CommandRegistry();
        registry.Register("x", ctx => fired.Add(ctx.Node));

        using var input = new InputSource();
        using var host = new InteractionHost(input, registry);
        host.Reconcile(root, result);

        input.Push(new HostEvent.Key("key.j", default));

        fired.Should().ContainSingle().Which.Should().Be(row);
    }
}
