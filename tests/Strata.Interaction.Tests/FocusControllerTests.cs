namespace Strata.Interaction.Tests;

public sealed class FocusControllerTests
{
    private static InteractionTestNode[] Rows(int count)
    {
        var rows = new InteractionTestNode[count];
        for (var i = 0; i < count; i++)
        {
            rows[i] = new InteractionTestNode("Row", id: $"r{i}");
        }

        return rows;
    }

    [Fact]
    public void Starts_with_no_focus_by_default()
    {
        var focus = new FocusController(Rows(3));

        focus.Focused.Should().BeNull();
        focus.FocusedIndex.Should().Be(-1);
    }

    [Fact]
    public void Initial_focus_sets_the_pseudo_state()
    {
        var rows = Rows(3);
        var focus = new FocusController(rows, initialFocus: 1);

        focus.Focused.Should().Be(rows[1]);
        rows[1].PseudoStates.Should().Contain(FocusController.FocusedState);
    }

    [Fact]
    public void First_navigate_down_focuses_the_first_node()
    {
        var rows = Rows(3);
        var focus = new FocusController(rows);

        focus.MoveNext().Should().BeTrue();

        focus.FocusedIndex.Should().Be(0);
        rows[0].PseudoStates.Should().Contain(FocusController.FocusedState);
    }

    [Fact]
    public void First_navigate_up_focuses_the_last_node()
    {
        var rows = Rows(3);
        var focus = new FocusController(rows);

        focus.MovePrevious().Should().BeTrue();

        focus.FocusedIndex.Should().Be(2);
        rows[2].PseudoStates.Should().Contain(FocusController.FocusedState);
    }

    [Fact]
    public void Moving_focus_clears_the_old_pseudo_state_and_sets_the_new_one()
    {
        var rows = Rows(3);
        var focus = new FocusController(rows, initialFocus: 0);

        focus.MoveNext();

        rows[0].PseudoStates.Should().NotContain(FocusController.FocusedState);
        rows[1].PseudoStates.Should().Contain(FocusController.FocusedState);
    }

    [Fact]
    public void Navigation_clamps_at_the_ends_and_reports_no_move()
    {
        var rows = Rows(2);
        var focus = new FocusController(rows, initialFocus: 1);

        focus.MoveNext().Should().BeFalse();
        focus.FocusedIndex.Should().Be(1);

        var top = new FocusController(rows, initialFocus: 0);
        top.MovePrevious().Should().BeFalse();
        top.FocusedIndex.Should().Be(0);
    }

    [Fact]
    public void Each_actual_move_emits_change_events_for_both_toggled_nodes()
    {
        var rows = Rows(3);
        var changes = new List<TreeChange>();
        var focus = new FocusController(rows, changes.Add, initialFocus: 0);

        changes.Clear();
        focus.MoveNext();

        changes.Should().HaveCount(2);
        changes.Should().AllBeOfType<TreeChange.PseudoStateChanged>();
        var removed = (TreeChange.PseudoStateChanged)changes[0];
        var added = (TreeChange.PseudoStateChanged)changes[1];
        removed.Node.Should().Be(rows[0]);
        removed.Added.Should().BeFalse();
        added.Node.Should().Be(rows[1]);
        added.Added.Should().BeTrue();
    }

    [Fact]
    public void Empty_focus_ring_never_moves()
    {
        var focus = new FocusController(Array.Empty<ITreeNode>());

        focus.MoveNext().Should().BeFalse();
        focus.Focused.Should().BeNull();
    }

    [Fact]
    public void Non_mutable_focusable_is_rejected()
    {
        var act = () => new FocusController(new[] { new NonMutableNode() });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*IPseudoStateMutable*");
    }

    [Fact]
    public void Out_of_range_initial_focus_is_rejected()
    {
        var act = () => new FocusController(Rows(2), initialFocus: 5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private sealed class NonMutableNode : ITreeNode
    {
        public string Kind => "Row";
        public string? Id => null;
        public IReadOnlySet<string> Classes { get; } = new HashSet<string>();
        public IReadOnlySet<string> PseudoStates { get; } = new HashSet<string>();
        public ITreeNode? Parent => null;
        public IEnumerable<ITreeNode> Children => Enumerable.Empty<ITreeNode>();
        public object? Underlying => this;
        public bool TryGetAttribute(string name, out object? value)
        {
            value = null;
            return false;
        }
    }
}
