using Strata.Core;
using Strata.Css;
using Strata.Interaction;
using Terminal.Gui;

namespace Strata.Render.TerminalGui.Tests;

public sealed class TerminalGuiInputSourceTests
{
    [Theory]
    [InlineData(KeyCode.J, "key.j")]
    [InlineData(KeyCode.K, "key.k")]
    [InlineData(KeyCode.X, "key.x")]
    [InlineData(KeyCode.CursorDown, "key.ArrowDown")]
    [InlineData(KeyCode.CursorUp, "key.ArrowUp")]
    [InlineData(KeyCode.CursorLeft, "key.ArrowLeft")]
    [InlineData(KeyCode.CursorRight, "key.ArrowRight")]
    [InlineData(KeyCode.Space, "key.space")]
    public void Maps_keys_to_the_interaction_event_name_dsl(KeyCode code, string expected)
    {
        using var source = new TerminalGuiInputSource();
        string? observed = null;
        using var _ = source.Events.Subscribe(e => observed = e.Name);

        var name = source.HandleKey(new Key(code));

        name.Should().Be(expected);
        observed.Should().Be(expected);
    }

    [Fact]
    public void Maps_a_ctrl_chord_with_the_ctrl_prefix()
    {
        using var source = new TerminalGuiInputSource();

        var name = source.HandleKey(new Key(KeyCode.C).WithCtrl);

        name.Should().Be("key.ctrl+c");
    }

    [Fact]
    public void Uppercase_letter_maps_to_its_lowercase_event_name()
    {
        using var source = new TerminalGuiInputSource();

        // Shift+j and a bare 'J' both resolve to "key.j" so a binding fires regardless of caps state.
        var name = source.HandleKey(new Key(KeyCode.J).WithShift);

        name.Should().Be("key.j");
    }

    [Fact]
    public void Pushed_event_carries_an_equivalent_console_key_info()
    {
        using var source = new TerminalGuiInputSource();
        HostEvent? observed = null;
        using var _ = source.Events.Subscribe(e => observed = e);

        source.HandleKey(new Key(KeyCode.J));

        observed.Should().BeOfType<HostEvent.Key>();
        var key = (HostEvent.Key)observed!;
        key.Press.KeyChar.Should().Be('j');
        key.Press.Key.Should().Be(ConsoleKey.J);
    }

    [Fact]
    public void Live_keystroke_drives_the_focus_controller_through_the_dispatcher()
    {
        // End-to-end: a real Terminal.Gui key → TerminalGuiInputSource → InteractionHost dispatcher →
        // navigate-down command → FocusController moves focus. This is the live input wiring the
        // Phase 6 re-scope deferred to Phase 7.
        var props = InteractionProperties.CreateRegistry();
        var sheet = new CssStylesheetParser(new CssSelectorLanguage(), props)
            .Parse("Table { command: \"navigate-down\" when \"key.j\"; }");
        var cascade = new Cascade(props);

        var table = new RenderTestNode("Table");
        var rows = new[]
        {
            new RenderTestNode("Row", id: "r0"),
            new RenderTestNode("Row", id: "r1"),
        };
        foreach (var row in rows)
        {
            table.Add(row);
        }

        using var input = new TerminalGuiInputSource();
        var registry = new CommandRegistry();
        var focus = new FocusController(rows);
        SampleCommands.RegisterNavigation(registry, focus);

        using var host = new InteractionHost(input, registry);
        host.Reconcile(table, cascade.Compute(table, sheet));

        focus.Focused.Should().BeNull();

        input.HandleKey(new Key(KeyCode.J));

        focus.Focused.Should().BeSameAs(rows[0]);
    }

    [Fact]
    public void Unmappable_key_pushes_nothing_and_returns_null()
    {
        using var source = new TerminalGuiInputSource();
        var pushed = 0;
        using var _ = source.Events.Subscribe(_ => pushed++);

        var name = source.HandleKey(Key.Empty);

        name.Should().BeNull();
        pushed.Should().Be(0);
    }
}
