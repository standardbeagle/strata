namespace Strata.Interaction.Tests;

public sealed class SelectionControllerTests
{
    [Fact]
    public void Toggle_selects_then_deselects()
    {
        var node = new InteractionTestNode("Row");
        var selection = new SelectionController();

        selection.Toggle(node).Should().BeTrue();
        node.PseudoStates.Should().Contain(SelectionController.SelectedState);
        selection.IsSelected(node).Should().BeTrue();

        selection.Toggle(node).Should().BeFalse();
        node.PseudoStates.Should().NotContain(SelectionController.SelectedState);
        selection.IsSelected(node).Should().BeFalse();
    }

    [Fact]
    public void Selection_is_multi_node()
    {
        var a = new InteractionTestNode("Row", id: "a");
        var b = new InteractionTestNode("Row", id: "b");
        var selection = new SelectionController();

        selection.Select(a);
        selection.Select(b);

        selection.Selected.Should().BeEquivalentTo(new ITreeNode[] { a, b });
        a.PseudoStates.Should().Contain(SelectionController.SelectedState);
        b.PseudoStates.Should().Contain(SelectionController.SelectedState);
    }

    [Fact]
    public void Select_is_idempotent()
    {
        var node = new InteractionTestNode("Row");
        var selection = new SelectionController();

        selection.Select(node).Should().BeTrue();
        selection.Select(node).Should().BeFalse();
        selection.Selected.Should().ContainSingle();
    }

    [Fact]
    public void Clear_deselects_every_node()
    {
        var a = new InteractionTestNode("Row", id: "a");
        var b = new InteractionTestNode("Row", id: "b");
        var selection = new SelectionController();
        selection.Select(a);
        selection.Select(b);

        selection.Clear();

        selection.Selected.Should().BeEmpty();
        a.PseudoStates.Should().NotContain(SelectionController.SelectedState);
        b.PseudoStates.Should().NotContain(SelectionController.SelectedState);
    }

    [Fact]
    public void Toggle_emits_change_events_on_actual_change_only()
    {
        var node = new InteractionTestNode("Row");
        var changes = new List<TreeChange>();
        var selection = new SelectionController(changes.Add);

        selection.Select(node);
        selection.Select(node); // idempotent, no event

        changes.Should().ContainSingle();
        var change = changes[0].Should().BeOfType<TreeChange.PseudoStateChanged>().Subject;
        change.Node.Should().Be(node);
        change.State.Should().Be(SelectionController.SelectedState);
        change.Added.Should().BeTrue();
    }

    [Fact]
    public void Non_mutable_node_is_rejected()
    {
        var selection = new SelectionController();

        var act = () => selection.Select(new NonMutableNode());

        act.Should().Throw<ArgumentException>().WithMessage("*IPseudoStateMutable*");
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
