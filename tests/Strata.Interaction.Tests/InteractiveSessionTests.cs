using Strata.Core;
using Strata.Css;

namespace Strata.Interaction.Tests;

public sealed class InteractiveSessionTests
{
    private static (Cascade cascade, IStylesheet sheet) Build(string css)
    {
        var props = InteractionProperties.CreateRegistry();
        var parser = new CssStylesheetParser(new CssSelectorLanguage(), props);
        return (new Cascade(props), parser.Parse(css));
    }

    // Navigation binds once on the Table container (so one keystroke = one move), while rows gain
    // an extra rule only while :focused. After a navigation keystroke moves focus, the focused
    // node's matched-rule set must grow — proving the keystroke drove a re-cascade through the
    // entry-point loop.
    private const string NavCss =
        "Table { command: \"navigate-down\" when \"key.j\"; command: \"navigate-up\" when \"key.k\"; } " +
        "Row:focused { command: \"noop\" when \"key.x\"; }";

    [Fact]
    public void Initial_cascade_and_reconcile_run_on_construction()
    {
        var root = BuildTree(out _);
        var (cascade, sheet) = Build(NavCss);
        var registry = new CommandRegistry();
        registry.Register(SampleCommands.NavigateDown, _ => { });
        registry.Register(SampleCommands.NavigateUp, _ => { });

        using var input = new InputSource();
        using var session = new InteractiveSession(cascade, sheet, root, input, registry);

        session.Cascade.Should().NotBeNull();
        session.RecascadeCount.Should().Be(0);
    }

    [Fact]
    public void Keystroke_moves_focus_and_triggers_recascade_that_restyles_the_focused_node()
    {
        var root = BuildTree(out var rows);
        var (cascade, sheet) = Build(NavCss);

        var registry = new CommandRegistry();
        using var input = new InputSource();
        using var session = new InteractiveSession(cascade, sheet, root, input, registry);

        var focus = new FocusController(rows, session.OnPseudoStateChanged);
        SampleCommands.RegisterNavigation(registry, focus);

        // Before any keystroke, no row is focused → rows match no rule (only Table carries the nav).
        session.Cascade.GetMatchedRules(rows[0]).Should().BeEmpty();

        // Press j: the Table's navigate-down fires once → focus moves to row 0 → pseudo-state
        // change → re-cascade. Row 0 now matches "Row:focused".
        input.Push(new HostEvent.Key("key.j", default));

        focus.Focused.Should().BeSameAs(rows[0]);
        rows[0].PseudoStates.Should().Contain(FocusController.FocusedState);
        session.RecascadeCount.Should().Be(1);
        session.Cascade.GetMatchedRules(rows[0]).Should().ContainSingle();
    }

    [Fact]
    public void Recascaded_event_fires_with_the_fresh_result()
    {
        var root = BuildTree(out var rows);
        var (cascade, sheet) = Build(NavCss);
        var registry = new CommandRegistry();
        using var input = new InputSource();
        using var session = new InteractiveSession(cascade, sheet, root, input, registry);

        var focus = new FocusController(rows, session.OnPseudoStateChanged);
        SampleCommands.RegisterNavigation(registry, focus);

        ICascadeResult? observed = null;
        session.Recascaded += r => observed = r;

        input.Push(new HostEvent.Key("key.j", default));

        observed.Should().NotBeNull();
        observed.Should().BeSameAs(session.Cascade);
    }

    [Fact]
    public void Selection_toggle_through_the_session_recascades()
    {
        var root = BuildTree(out var rows);
        var (cascade, sheet) = Build(
            "Row { command: \"toggle-select\" when \"key.space\"; }");

        var registry = new CommandRegistry();
        using var input = new InputSource();
        using var session = new InteractiveSession(cascade, sheet, root, input, registry);

        var selection = new SelectionController(session.OnPseudoStateChanged);
        registry.Register("toggle-select", ctx => selection.Toggle(ctx.Node));

        input.Push(new HostEvent.Key("key.space", default));

        rows.Should().OnlyContain(r => r.PseudoStates.Contains(SelectionController.SelectedState));
        session.RecascadeCount.Should().Be(rows.Length);
    }

    [Fact]
    public void Dispose_stops_dispatch()
    {
        var root = BuildTree(out var rows);
        var (cascade, sheet) = Build(NavCss);
        var registry = new CommandRegistry();
        var input = new InputSource();
        var session = new InteractiveSession(cascade, sheet, root, input, registry);

        var focus = new FocusController(rows, session.OnPseudoStateChanged);
        SampleCommands.RegisterNavigation(registry, focus);

        session.Dispose();
        input.Push(new HostEvent.Key("key.j", default));

        focus.Focused.Should().BeNull();
        input.Dispose();
    }

    private static InteractionTestNode BuildTree(out InteractionTestNode[] rows)
    {
        var table = new InteractionTestNode("Table");
        rows = new[]
        {
            new InteractionTestNode("Row", id: "r0"),
            new InteractionTestNode("Row", id: "r1"),
        };

        foreach (var row in rows)
        {
            table.Add(row);
        }

        return table;
    }
}
