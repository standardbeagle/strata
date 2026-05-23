namespace Strata.Interaction;

/// <summary>
/// An event flowing from the host into the interaction layer, typed by <see cref="Name"/>.
/// The dispatcher matches an event's <see cref="Name"/> against the <c>when</c> clause of
/// active <see cref="CommandBinding"/>s.
/// </summary>
/// <param name="Name">
/// The event name in the closed DSL: <c>key.&lt;k&gt;</c>, <c>key.&lt;k&gt;:held</c>,
/// <c>key.ctrl+&lt;k&gt;</c>, <c>focus</c>, <c>blur</c>, <c>tick</c>, or <c>custom.&lt;name&gt;</c>.
/// </param>
public abstract record HostEvent(string Name)
{
    /// <summary>A keystroke event.</summary>
    /// <param name="Name">The event name (e.g. <c>key.j</c>).</param>
    /// <param name="Press">The console key info for the keystroke.</param>
    public sealed record Key(string Name, ConsoleKeyInfo Press) : HostEvent(Name);

    /// <summary>A focus gain/loss event for a specific node.</summary>
    /// <param name="Name">The event name (<c>focus</c> or <c>blur</c>).</param>
    /// <param name="Node">The node whose focus state changed.</param>
    /// <param name="Focused"><see langword="true"/> on focus gain; <see langword="false"/> on loss.</param>
    public sealed record Focus(string Name, ITreeNode Node, bool Focused) : HostEvent(Name);

    /// <summary>An engine animation tick.</summary>
    /// <param name="Name">The event name (<c>tick</c>).</param>
    /// <param name="Delta">Elapsed time since the previous tick.</param>
    public sealed record Tick(string Name, TimeSpan Delta) : HostEvent(Name);

    /// <summary>A host-published custom event.</summary>
    /// <param name="Name">The event name (<c>custom.&lt;name&gt;</c>).</param>
    /// <param name="Payload">Opaque host payload.</param>
    public sealed record Custom(string Name, object? Payload) : HostEvent(Name);
}

/// <summary>
/// A hot stream of host events. The interaction layer subscribes selector-filtered views over
/// this stream; the host pushes events as input arrives.
/// </summary>
public interface IInputSource
{
    /// <summary>Hot stream of events typed by name. The dispatcher feeds it.</summary>
    IObservable<HostEvent> Events { get; }
}
