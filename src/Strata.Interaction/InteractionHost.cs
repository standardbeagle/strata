using System.Reactive.Linq;

namespace Strata.Interaction;

/// <summary>
/// The selector-bound interaction dispatcher. Replaces the original imperative
/// <c>IBehavior.Attach</c>/<c>Detach</c> lifecycle (see <c>docs/05-interaction-redesign.md</c>)
/// with a subscription diff over selector-filtered <see cref="IObservable{T}"/> streams.
/// </summary>
/// <remarks>
/// <para>
/// On each <see cref="Reconcile"/> the host computes, for every node, the additive set of
/// <c>(command, event)</c> bindings the cascade resolved — collected from <em>all</em> matched
/// rules (not the single cascade winner), honoring the additive cascade semantics of the
/// <c>command:</c> property. It diffs that set, keyed by <c>(node, command, event)</c>, against
/// the previously active set:
/// </para>
/// <list type="bullet">
///   <item>A binding present only in the new set gets a fresh subscription.</item>
///   <item>A binding present only in the prior set has its subscription disposed.</item>
///   <item>A binding present in both is left untouched — its subscription is never re-created,
///         so identity is stable across re-cascade and no spurious re-fire occurs.</item>
/// </list>
/// <para>
/// Node identity stability rests on <see cref="ITreeNode"/> equality (reinforcing the Phase 0
/// design): the same logical node must compare equal across cascade runs for its subscriptions
/// to be preserved.
/// </para>
/// </remarks>
public sealed class InteractionHost : IDisposable
{
    private readonly IInputSource _input;
    private readonly ICommandRegistry _commands;

    // Active subscriptions keyed by (node, command, event). The value is the live IDisposable
    // from the System.Reactive subscription that routes matching events to the handler.
    private readonly Dictionary<SubscriptionKey, IDisposable> _active = new();

    private bool _disposed;

    /// <summary>Create an interaction host bound to an input source and command registry.</summary>
    public InteractionHost(IInputSource input, ICommandRegistry commands)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(commands);
        _input = input;
        _commands = commands;
    }

    /// <summary>The number of currently active <c>(node, command, event)</c> subscriptions.</summary>
    public int ActiveSubscriptionCount => _active.Count;

    /// <summary>
    /// Diff the active subscription set against the command bindings the cascade resolved for the
    /// tree rooted at <paramref name="root"/>, adding and disposing subscriptions accordingly.
    /// Call after each (re-)cascade and on hot reload.
    /// </summary>
    public void Reconcile(ITreeNode root, ICascadeResult cascade)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(cascade);

        var desired = new HashSet<SubscriptionKey>();
        CollectBindings(root, cascade, desired);

        // Add subscriptions for bindings that appeared.
        foreach (var key in desired)
        {
            if (!_active.ContainsKey(key))
            {
                _active[key] = Subscribe(key, cascade);
            }
        }

        // Dispose subscriptions for bindings that disappeared. Detach (dispose) completes before
        // any subsequent attach for the same node on a later reconcile, satisfying the
        // detach-before-re-attach ordering requirement.
        var stale = new List<SubscriptionKey>();
        foreach (var key in _active.Keys)
        {
            if (!desired.Contains(key))
            {
                stale.Add(key);
            }
        }

        foreach (var key in stale)
        {
            _active[key].Dispose();
            _active.Remove(key);
        }
    }

    private static void CollectBindings(
        ITreeNode node,
        ICascadeResult cascade,
        HashSet<SubscriptionKey> into)
    {
        // Additive: every matched rule that declares `command:` contributes. The cascade's single
        // winner is deliberately bypassed here — multiple rules accumulate, none overrides.
        foreach (var application in cascade.GetMatchedRules(node))
        {
            foreach (var declaration in application.Rule.Declarations)
            {
                if (!string.Equals(
                        declaration.Property,
                        CommandPropertyDescriptor.PropertyName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (declaration.Value is not CommandValue value)
                {
                    continue;
                }

                foreach (var binding in value.Bindings)
                {
                    into.Add(new SubscriptionKey(node, binding.Command, binding.Event));
                }
            }
        }

        foreach (var child in node.Children)
        {
            CollectBindings(child, cascade, into);
        }
    }

    private IDisposable Subscribe(SubscriptionKey key, ICascadeResult cascade)
    {
        return _input.Events
            .Where(e => string.Equals(e.Name, key.Event, StringComparison.Ordinal))
            .Subscribe(e => Dispatch(key, e, cascade));
    }

    private void Dispatch(SubscriptionKey key, HostEvent hostEvent, ICascadeResult cascade)
    {
        if (_commands.TryGet(key.Command, out var handler))
        {
            handler(new CommandContext(key.Node, hostEvent, cascade));
        }
    }

    /// <summary>Dispose every active subscription.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var subscription in _active.Values)
        {
            subscription.Dispose();
        }

        _active.Clear();
        _disposed = true;
    }

    private readonly record struct SubscriptionKey(ITreeNode Node, string Command, string Event);
}
