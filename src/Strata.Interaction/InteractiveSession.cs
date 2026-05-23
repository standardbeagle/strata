using Strata.Core;

namespace Strata.Interaction;

/// <summary>
/// The <c>Format-Styled -Interactive</c> entry point: wires an <see cref="IInputSource"/> through
/// the <see cref="InteractionHost"/> dispatcher into a cascade re-evaluation loop, so that a
/// pseudo-state change made by a command handler (focus move, selection toggle) re-styles the
/// affected nodes.
/// </summary>
/// <remarks>
/// <para>
/// The loop is: input event → dispatcher → command handler mutates a node's pseudo-state via a
/// controller → the controller's change sink calls <see cref="OnPseudoStateChanged"/> → the
/// session re-cascades and re-reconciles the host. Re-cascade is a full
/// <see cref="ICascade.Compute"/>: the incremental <see cref="ICascade.Update"/> path explicitly
/// defers tree-change-driven updates (see <c>Strata.Core/Cascade.cs</c>), so the session takes the
/// supported full-recompute route until that lands.
/// </para>
/// <para>
/// Per the Phase 6 re-scope, the input source here is programmatic: the host (or a test harness)
/// pushes <see cref="HostEvent"/>s into the <see cref="Input"/> source. The live terminal raw-mode
/// input layer that feeds real keystrokes is deferred to Phase 7
/// (<c>docs/05-interaction-redesign.md</c> / task re-scope).
/// </para>
/// </remarks>
public sealed class InteractiveSession : IDisposable
{
    private readonly ICascade _cascade;
    private readonly IStylesheet _stylesheet;
    private readonly ITreeNode _root;
    private readonly InteractionHost _host;
    private bool _disposed;

    /// <summary>
    /// Create an interactive session over a tree, stylesheet, input source, and command registry.
    /// Computes the initial cascade and reconciles the dispatcher immediately.
    /// </summary>
    public InteractiveSession(
        ICascade cascade,
        IStylesheet stylesheet,
        ITreeNode root,
        IInputSource input,
        ICommandRegistry commands)
    {
        ArgumentNullException.ThrowIfNull(cascade);
        ArgumentNullException.ThrowIfNull(stylesheet);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(commands);

        _cascade = cascade;
        _stylesheet = stylesheet;
        _root = root;
        Input = input;
        _host = new InteractionHost(input, commands);

        Cascade = _cascade.Compute(_root, _stylesheet);
        _host.Reconcile(_root, Cascade);
    }

    /// <summary>The programmatic input source this session dispatches from.</summary>
    public IInputSource Input { get; }

    /// <summary>The most recent cascade result, refreshed on every re-cascade.</summary>
    public ICascadeResult Cascade { get; private set; }

    /// <summary>The number of full re-cascades performed since construction.</summary>
    public int RecascadeCount { get; private set; }

    /// <summary>
    /// Raised after each re-cascade with the fresh result, so a projection can repaint. The
    /// argument is the same instance as <see cref="Cascade"/>.
    /// </summary>
    public event Action<ICascadeResult>? Recascaded;

    /// <summary>
    /// The change sink to hand to <see cref="FocusController"/> / <see cref="SelectionController"/>.
    /// Each pseudo-state change drives one re-cascade-and-reconcile pass.
    /// </summary>
    public void OnPseudoStateChanged(TreeChange change)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(change);
        Recascade();
    }

    private void Recascade()
    {
        Cascade = _cascade.Compute(_root, _stylesheet);
        RecascadeCount++;
        _host.Reconcile(_root, Cascade);
        Recascaded?.Invoke(Cascade);
    }

    /// <summary>Dispose the underlying dispatcher and its subscriptions.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _host.Dispose();
        _disposed = true;
    }
}
