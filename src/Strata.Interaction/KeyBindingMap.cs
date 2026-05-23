namespace Strata.Interaction;

/// <summary>
/// Maps trigger event-names (e.g. <c>key.j</c>, <c>key.ArrowDown</c>) to command names, detecting
/// conflicts where one key would fire two different commands. A conflict is an
/// <em>error</em> condition (the Phase 6 risk: "conflicting key bindings (error+log)"): the map
/// logs it through the optional sink and throws, rather than silently letting one binding shadow
/// another.
/// </summary>
/// <remarks>
/// Binding the same key to the <em>same</em> command twice is idempotent and not a conflict — only
/// a divergent target is. This mirrors the registry's single-handler-per-name rule: the interaction
/// layer rejects ambiguous wiring up front instead of resolving it by accident at dispatch time.
/// </remarks>
public sealed class KeyBindingMap
{
    private readonly Dictionary<string, string> _byEvent = new(StringComparer.Ordinal);
    private readonly Action<string>? _log;

    /// <summary>Create a key-binding map with an optional log sink for conflict diagnostics.</summary>
    /// <param name="log">
    /// Invoked with a human-readable message when a conflicting binding is detected, just before the
    /// map throws. No logging framework is assumed; the host supplies the sink.
    /// </param>
    public KeyBindingMap(Action<string>? log = null)
    {
        _log = log;
    }

    /// <summary>The current event-name → command-name bindings.</summary>
    public IReadOnlyDictionary<string, string> Bindings => _byEvent;

    /// <summary>
    /// Bind <paramref name="eventName"/> to <paramref name="command"/>. Re-binding to the same
    /// command is a no-op; re-binding to a different command logs and throws.
    /// </summary>
    public void Bind(string eventName, string command)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        ArgumentException.ThrowIfNullOrEmpty(command);

        if (_byEvent.TryGetValue(eventName, out var existing))
        {
            if (string.Equals(existing, command, StringComparison.Ordinal))
            {
                return;
            }

            var message =
                $"Conflicting key binding: '{eventName}' is already bound to command '{existing}'; " +
                $"refusing to rebind it to '{command}'.";
            _log?.Invoke(message);
            throw new InvalidOperationException(message);
        }

        _byEvent[eventName] = command;
    }

    /// <summary>Look up the command bound to <paramref name="eventName"/>, if any.</summary>
    public bool TryGetCommand(string eventName, out string command)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        return _byEvent.TryGetValue(eventName, out command!);
    }
}
