using System.Collections.Immutable;

namespace Strata.Interaction.Tests;

public sealed class CommandPropertyTests
{
    private static ImmutableArray<CommandBinding> Parse(string source)
    {
        var descriptor = new CommandPropertyDescriptor();
        var value = (CommandValue)descriptor.Parse(source.AsSpan());
        return value.Bindings;
    }

    [Fact]
    public void Parses_a_single_command_when_event_pair()
    {
        var bindings = Parse("\"navigate-down\" when \"key.j\"");

        bindings.Should().ContainSingle();
        bindings[0].Should().Be(new CommandBinding("navigate-down", "key.j"));
    }

    [Fact]
    public void Parses_comma_separated_items_as_multiple_bindings()
    {
        var bindings = Parse("\"navigate-down\" when \"key.j\", \"navigate-up\" when \"key.k\"");

        bindings.Should().HaveCount(2);
        bindings[0].Should().Be(new CommandBinding("navigate-down", "key.j"));
        bindings[1].Should().Be(new CommandBinding("navigate-up", "key.k"));
    }

    [Fact]
    public void Accepts_single_quotes()
    {
        var bindings = Parse("'kill' when 'key.k:held'");

        bindings.Should().ContainSingle();
        bindings[0].Should().Be(new CommandBinding("kill", "key.k:held"));
    }

    [Fact]
    public void Empty_value_parses_to_empty()
    {
        Parse("").Should().BeEmpty();
        Parse("   ").Should().BeEmpty();
    }

    [Fact]
    public void Comma_inside_quotes_is_not_a_split_point()
    {
        var bindings = Parse("\"do,thing\" when \"custom.a,b\"");

        bindings.Should().ContainSingle();
        bindings[0].Should().Be(new CommandBinding("do,thing", "custom.a,b"));
    }

    [Fact]
    public void Missing_when_keyword_throws()
    {
        Action act = () => Parse("\"navigate-down\" \"key.j\"");
        act.Should().Throw<FormatException>().WithMessage("*when*");
    }

    [Fact]
    public void Unquoted_command_name_throws()
    {
        Action act = () => Parse("navigate-down when \"key.j\"");
        act.Should().Throw<FormatException>().WithMessage("*quoted*");
    }

    [Fact]
    public void Trailing_garbage_throws()
    {
        Action act = () => Parse("\"a\" when \"b\" extra");
        act.Should().Throw<FormatException>().WithMessage("*trailing*");
    }

    [Fact]
    public void Empty_quoted_name_throws()
    {
        Action act = () => Parse("\"\" when \"key.j\"");
        act.Should().Throw<FormatException>().WithMessage("*non-empty*");
    }

    [Fact]
    public void Descriptor_metadata_is_correct()
    {
        var descriptor = new CommandPropertyDescriptor();

        descriptor.Name.Should().Be("command");
        descriptor.Inherits.Should().BeFalse();
        descriptor.Initial.Should().Be(CommandValue.Empty);
    }
}
