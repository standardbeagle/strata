using System.Reactive.Subjects;

namespace Strata.Interaction;

/// <summary>
/// Default <see cref="IInputSource"/> backed by a System.Reactive <see cref="Subject{T}"/>.
/// The host calls <see cref="Push"/> as input arrives; the interaction layer subscribes to
/// <see cref="IInputSource.Events"/>.
/// </summary>
public sealed class InputSource : IInputSource, IDisposable
{
    private readonly Subject<HostEvent> _subject = new();

    /// <inheritdoc/>
    public IObservable<HostEvent> Events => _subject;

    /// <summary>Publish an event to all active subscribers.</summary>
    public void Push(HostEvent hostEvent)
    {
        ArgumentNullException.ThrowIfNull(hostEvent);
        _subject.OnNext(hostEvent);
    }

    /// <summary>Complete the stream and release subscribers.</summary>
    public void Dispose()
    {
        _subject.OnCompleted();
        _subject.Dispose();
    }
}
