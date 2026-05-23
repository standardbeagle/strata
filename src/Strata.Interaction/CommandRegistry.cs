namespace Strata.Interaction;

/// <summary>
/// The context handed to a <see cref="CommandHandler"/> when a stylesheet-declared command fires.
/// </summary>
/// <param name="Node">The node whose cascade resolved the firing command binding.</param>
/// <param name="Event">The host event that triggered the command.</param>
/// <param name="Cascade">The cascade result the binding was resolved against.</param>
public readonly record struct CommandContext(
    ITreeNode Node,
    HostEvent Event,
    ICascadeResult Cascade);

/// <summary>
/// Handles a command fired by a stylesheet <c>command:</c> binding. Handlers are pure functions of
/// <see cref="CommandContext"/>; any state lives in caller-owned closures.
/// </summary>
/// <param name="context">The firing context.</param>
public delegate void CommandHandler(CommandContext context);

/// <summary>
/// Resolves command names declared in stylesheets to handler delegates. Replaces the original
/// DI keyed-service resolution (see <c>docs/05-interaction-redesign.md</c>): a single registration
/// per command name, no reflection, AOT-clean.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>
    /// Register a handler invoked when a stylesheet <c>command:</c> line for
    /// <paramref name="commandName"/> fires. Registering the same name twice throws.
    /// </summary>
    void Register(string commandName, CommandHandler handler);

    /// <summary>Look up a registered handler by command name.</summary>
    bool TryGet(string commandName, out CommandHandler handler);
}

/// <summary>
/// Default <see cref="ICommandRegistry"/>: a plain name → delegate map with single-registration
/// enforcement. The second registration for a name loses with a clear error
/// (per the redesign's handler-lookup contract).
/// </summary>
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly Dictionary<string, CommandHandler> _handlers = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void Register(string commandName, CommandHandler handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandName);
        ArgumentNullException.ThrowIfNull(handler);

        if (_handlers.ContainsKey(commandName))
        {
            throw new InvalidOperationException(
                $"Command '{commandName}' is already registered. Each command name takes a single " +
                "handler; the second registration is rejected.");
        }

        _handlers[commandName] = handler;
    }

    /// <inheritdoc/>
    public bool TryGet(string commandName, out CommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(commandName);
        return _handlers.TryGetValue(commandName, out handler!);
    }
}
