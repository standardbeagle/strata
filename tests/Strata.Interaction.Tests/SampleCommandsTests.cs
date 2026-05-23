namespace Strata.Interaction.Tests;

public sealed class SampleCommandsTests
{
    private static CommandContext Ctx(ITreeNode node) =>
        new(node, new HostEvent.Custom("custom.test", null), Cascade: null!);

    [Fact]
    public void Navigation_handlers_drive_focus_by_direction()
    {
        var registry = new CommandRegistry();
        var moves = new List<int>();
        SampleCommands.RegisterNavigation(registry, moves.Add);

        registry.TryGet(SampleCommands.NavigateDown, out var down).Should().BeTrue();
        registry.TryGet(SampleCommands.NavigateUp, out var up).Should().BeTrue();

        down(Ctx(new InteractionTestNode("Row")));
        up(Ctx(new InteractionTestNode("Row")));

        moves.Should().Equal(+1, -1);
    }

    [Fact]
    public void Kill_invokes_action_only_after_confirmation()
    {
        var registry = new CommandRegistry();
        var killed = new List<ITreeNode>();
        var allow = false;
        SampleCommands.RegisterKill(registry, _ => allow, killed.Add);

        registry.TryGet(SampleCommands.Kill, out var kill).Should().BeTrue();

        var node = new InteractionTestNode("Process");
        kill(Ctx(node));
        killed.Should().BeEmpty();

        allow = true;
        kill(Ctx(node));
        killed.Should().ContainSingle().Which.Should().Be(node);
    }

    [Fact]
    public void Sparkline_appends_the_attribute_sample_to_the_node_buffer()
    {
        var registry = new CommandRegistry();
        var buffers = SampleCommands.RegisterSparkline(registry, "Cpu", capacity: 3);

        registry.TryGet(SampleCommands.RenderSparkline, out var render).Should().BeTrue();

        var node = new InteractionTestNode(
            "Process",
            attributes: new Dictionary<string, object?> { ["Cpu"] = 50.0 });

        render(Ctx(node));
        render(Ctx(node));

        buffers.Should().ContainKey(node);
        buffers[node].ToArray().Should().Equal(50.0, 50.0);
    }

    [Fact]
    public void Sparkline_ignores_nodes_without_the_attribute()
    {
        var registry = new CommandRegistry();
        var buffers = SampleCommands.RegisterSparkline(registry, "Cpu");
        registry.TryGet(SampleCommands.RenderSparkline, out var render);

        render(Ctx(new InteractionTestNode("Process")));

        buffers.Should().BeEmpty();
    }
}

public sealed class SparklineBufferTests
{
    [Fact]
    public void Holds_samples_in_order_until_full()
    {
        var buffer = new SparklineBuffer(3);
        buffer.Add(1);
        buffer.Add(2);

        buffer.Count.Should().Be(2);
        buffer.ToArray().Should().Equal(1.0, 2.0);
    }

    [Fact]
    public void Evicts_oldest_when_over_capacity()
    {
        var buffer = new SparklineBuffer(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);

        buffer.Count.Should().Be(3);
        buffer.ToArray().Should().Equal(2.0, 3.0, 4.0);
    }

    [Fact]
    public void Rejects_non_positive_capacity()
    {
        Action act = () => _ = new SparklineBuffer(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
