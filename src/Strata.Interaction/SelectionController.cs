namespace Strata.Interaction;

/// <summary>
/// Owns multi-node selection state, toggling each node's <c>:selected</c> pseudo-state. The
/// counterpart to <see cref="FocusController"/>: focus is single (the cursor), selection is a set
/// (zero or more marked nodes). Keeps the redesign's attribute-style pseudo-state model
/// (<c>docs/05-interaction-redesign.md</c> line 41) — <c>:selected</c> is an unchanged toggle.
/// </summary>
/// <remarks>
/// Selectable nodes must implement <see cref="IPseudoStateMutable"/>. Each toggle that actually
/// changes a node's state emits a <see cref="TreeChange.PseudoStateChanged"/> through the optional
/// change sink so a host can re-cascade and re-style the node.
/// </remarks>
public sealed class SelectionController
{
    /// <summary>The pseudo-state this controller toggles.</summary>
    public const string SelectedState = "selected";

    private readonly HashSet<ITreeNode> _selected = new();
    private readonly Action<TreeChange>? _onChange;

    /// <summary>Create a selection controller.</summary>
    /// <param name="onChange">
    /// Optional sink invoked with a <see cref="TreeChange.PseudoStateChanged"/> for each toggle the
    /// controller performs. A host wires this to its re-cascade loop.
    /// </param>
    public SelectionController(Action<TreeChange>? onChange = null)
    {
        _onChange = onChange;
    }

    /// <summary>The currently selected nodes.</summary>
    public IReadOnlyCollection<ITreeNode> Selected => _selected;

    /// <summary>True if <paramref name="node"/> is currently selected.</summary>
    public bool IsSelected(ITreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _selected.Contains(node);
    }

    /// <summary>
    /// Toggle <paramref name="node"/>'s selection: select it if unselected, deselect it if selected.
    /// </summary>
    /// <returns><see langword="true"/> if the node ends up selected; otherwise <see langword="false"/>.</returns>
    public bool Toggle(ITreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _selected.Contains(node) ? !Deselect(node) : Select(node);
    }

    /// <summary>Select <paramref name="node"/> (idempotent).</summary>
    /// <returns><see langword="true"/> if the node was newly selected; otherwise <see langword="false"/>.</returns>
    public bool Select(ITreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        RequireMutable(node);

        if (!_selected.Add(node))
        {
            return false;
        }

        Apply(node, add: true);
        return true;
    }

    /// <summary>Deselect <paramref name="node"/> (idempotent).</summary>
    /// <returns><see langword="true"/> if the node was previously selected; otherwise <see langword="false"/>.</returns>
    public bool Deselect(ITreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!_selected.Remove(node))
        {
            return false;
        }

        Apply(node, add: false);
        return true;
    }

    /// <summary>Deselect every selected node.</summary>
    public void Clear()
    {
        foreach (var node in _selected.ToArray())
        {
            Deselect(node);
        }
    }

    private static IPseudoStateMutable RequireMutable(ITreeNode node)
    {
        if (node is not IPseudoStateMutable mutable)
        {
            throw new ArgumentException(
                $"Selectable node of kind '{node.Kind}' must implement {nameof(IPseudoStateMutable)} " +
                "so the controller can toggle its ':selected' pseudo-state.",
                nameof(node));
        }

        return mutable;
    }

    private void Apply(ITreeNode node, bool add)
    {
        var mutable = (IPseudoStateMutable)node;
        var changed = add ? mutable.AddPseudoState(SelectedState) : mutable.RemovePseudoState(SelectedState);
        if (changed)
        {
            _onChange?.Invoke(new TreeChange.PseudoStateChanged(node, SelectedState, add));
        }
    }
}
