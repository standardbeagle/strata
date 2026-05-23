namespace Strata.Interaction.Tests;

public sealed class CommandRegistryTests
{
    [Fact]
    public void Registered_handler_is_retrievable()
    {
        var registry = new CommandRegistry();
        CommandHandler handler = _ => { };

        registry.Register("navigate-down", handler);

        registry.TryGet("navigate-down", out var found).Should().BeTrue();
        found.Should().Be(handler);
    }

    [Fact]
    public void Unknown_command_is_not_found()
    {
        var registry = new CommandRegistry();
        registry.TryGet("missing", out _).Should().BeFalse();
    }

    [Fact]
    public void Second_registration_for_same_name_throws_clearly()
    {
        var registry = new CommandRegistry();
        registry.Register("kill", _ => { });

        Action act = () => registry.Register("kill", _ => { });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void Null_or_empty_name_is_rejected()
    {
        var registry = new CommandRegistry();
        Action empty = () => registry.Register("", _ => { });
        empty.Should().Throw<ArgumentException>();
    }
}
