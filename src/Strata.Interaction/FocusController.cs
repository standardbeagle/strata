namespace Strata.Interaction;

/// <summary>
/// Owns focus state for an ordered set of focusable nodes and moves it in response to navigation
/// commands. Mutating the focused node's <c>:focused</c> pseudo-state is exactly the role the
/// redesign assigns to the input dispatcher's focus owner
/// (<c>docs/05-interaction-redesign.md</c> Open-question Q2): "the input dispatcher owns a
/// <c>FocusController</c> that mutates the focused node's pseudo-state set in response to
/// navigation commands."
/// </summary>
/// <remarks>
/// <para>
/// Focusable nodes must implement <see cref="IPseudoStateMutable"/> so the controller can toggle
/// <c>:focused</c>. Each move clears <c>:focused</c> on the previously focused node and sets it on
/// the new one, emitting a <see cref="TreeChange.PseudoStateChanged"/> for each actual change so a
/// host can drive an incremental or full re-cascade.
/// </para>
/// <para>
/// Navigation is index-based over the focusable list: <c>+1</c> (j / down / arrow-down) moves to
/// the next node, <c>-1</c> (k / up / arrow-up) to the previous. Movement clamps at the ends — it
/// does not wrap — matching a list-navigation feel where the cursor stops at the boundary.
/// </para>
/// </remarks>
public sealed class FocusController
{
    /// <summary>The pseudo-state this controller toggles.</summary>
    public const string FocusedState = "focused";

    private readonly IReadOnlyList<ITreeNode> _focusables;
    private readonly Action<TreeChange>? _onChange;
    private int _index;

    /// <summary>
    /// Create a focus controller over an ordered list of focusable nodes.
    /// </summary>
    /// <param name="focusables">
    /// The focus ring in navigation order. Every entry must implement
    /// <see cref="IPseudoStateMutable"/>.
    /// </param>
    /// <param name="onChange">
    /// Optional sink invoked with a <see cref="TreeChange.PseudoStateChanged"/> for each toggle the
    /// controller performs. A host wires this to its re-cascade loop.
    /// </param>
    /// <param name="initialFocus">
    /// Index of the node to focus initially, or <c>-1</c> (the default) for no initial focus.
    /// </param>
    public FocusController(
        IReadOnlyList<ITreeNode> focusables,
        Action<TreeChange>? onChange = null,
        int initialFocus = -1)
    {
        ArgumentNullException.ThrowIfNull(focusables);

        foreach (var node in focusables)
        {
            if (node is not IPseudoStateMutable)
            {
                throw new ArgumentException(
                    $"Focusable node of kind '{node.Kind}' must implement {nameof(IPseudoStateMutable)} " +
                    "so the controller can toggle its ':focused' pseudo-state.",
                    nameof(focusables));
            }
        }

        if (initialFocus < -1 || initialFocus >= focusables.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialFocus),
                $"Initial focus index {initialFocus} is outside [-1, {focusables.Count - 1}].");
        }

        _focusables = focusables;
        _onChange = onChange;
        _index = -1;

        if (initialFocus >= 0)
        {
            SetFocus(initialFocus);
        }
    }

    /// <summary>The currently focused node, or <see langword="null"/> if nothing is focused.</summary>
    public ITreeNode? Focused => _index >= 0 ? _focusables[_index] : null;

    /// <summary>The index of the focused node, or <c>-1</c> when nothing is focused.</summary>
    public int FocusedIndex => _index;

    /// <summary>
    /// Move focus by <paramref name="delta"/> positions (clamped to the focusable range). The
    /// navigation command handlers pass <c>+1</c> for navigate-down and <c>-1</c> for navigate-up.
    /// </summary>
    /// <returns><see langword="true"/> if focus actually moved; otherwise <see langword="false"/>.</returns>
    public bool Move(int delta)
    {
        if (_focusables.Count == 0)
        {
            return false;
        }

        // From "no focus", a forward move starts at the first node and a backward move at the last,
        // so the very first keystroke always lands somewhere sensible.
        var start = _index >= 0 ? _index : (delta >= 0 ? -1 : _focusables.Count);
        var target = Math.Clamp(start + delta, 0, _focusables.Count - 1);

        if (target == _index)
        {
            return false;
        }

        SetFocus(target);
        return true;
    }

    /// <summary>Move focus to the next node (navigate-down).</summary>
    public bool MoveNext() => Move(+1);

    /// <summary>Move focus to the previous node (navigate-up).</summary>
    public bool MovePrevious() => Move(-1);

    private void SetFocus(int target)
    {
        if (_index >= 0)
        {
            Toggle(_focusables[_index], add: false);
        }

        _index = target;
        Toggle(_focusables[_index], add: true);
    }

    private void Toggle(ITreeNode node, bool add)
    {
        var mutable = (IPseudoStateMutable)node;
        var changed = add ? mutable.AddPseudoState(FocusedState) : mutable.RemovePseudoState(FocusedState);
        if (changed)
        {
            _onChange?.Invoke(new TreeChange.PseudoStateChanged(node, FocusedState, add));
        }
    }
}
