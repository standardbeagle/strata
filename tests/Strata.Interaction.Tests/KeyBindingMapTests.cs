namespace Strata.Interaction.Tests;

public sealed class KeyBindingMapTests
{
    [Fact]
    public void Binds_and_resolves_keys_to_commands()
    {
        var map = new KeyBindingMap();
        map.Bind("key.j", SampleCommands.NavigateDown);
        map.Bind("key.ArrowDown", SampleCommands.NavigateDown);

        map.TryGetCommand("key.j", out var command).Should().BeTrue();
        command.Should().Be(SampleCommands.NavigateDown);
        map.TryGetCommand("key.ArrowDown", out _).Should().BeTrue();
    }

    [Fact]
    public void Rebinding_the_same_key_to_the_same_command_is_a_no_op()
    {
        var map = new KeyBindingMap();
        map.Bind("key.j", SampleCommands.NavigateDown);

        var act = () => map.Bind("key.j", SampleCommands.NavigateDown);

        act.Should().NotThrow();
        map.Bindings.Should().ContainSingle();
    }

    [Fact]
    public void Conflicting_binding_logs_and_throws()
    {
        var logged = new List<string>();
        var map = new KeyBindingMap(logged.Add);
        map.Bind("key.j", SampleCommands.NavigateDown);

        var act = () => map.Bind("key.j", SampleCommands.NavigateUp);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Conflicting key binding*key.j*");
        logged.Should().ContainSingle().Which.Should().Contain("Conflicting key binding");
    }

    [Fact]
    public void Unknown_key_resolves_to_nothing()
    {
        var map = new KeyBindingMap();

        map.TryGetCommand("key.x", out _).Should().BeFalse();
    }
}
