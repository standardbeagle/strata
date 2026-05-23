using System.Collections.Concurrent;

namespace Strata.Interaction;

/// <summary>
/// The sample command handlers, replacing the original sample behaviors
/// (see <c>docs/05-interaction-redesign.md</c>): a focus-navigation keymap (the <c>Highlight</c>
/// behavior's role), a kill-with-confirmation command (<c>KillProcessConfirm</c>), and a
/// sparkline sampler (<c>ResourceMeter</c>). Handlers are pure functions of
/// <see cref="CommandContext"/>; per-node state lives in caller-owned closures.
/// </summary>
public static class SampleCommands
{
    /// <summary>The <c>navigate-down</c> / <c>navigate-up</c> command names.</summary>
    public const string NavigateDown = "navigate-down";

    /// <inheritdoc cref="NavigateDown"/>
    public const string NavigateUp = "navigate-up";

    /// <summary>The <c>kill</c> command name.</summary>
    public const string Kill = "kill";

    /// <summary>The <c>render-sparkline</c> command name.</summary>
    public const string RenderSparkline = "render-sparkline";

    /// <summary>
    /// Register a focus-navigation keymap. The <paramref name="focus"/> controller is invoked with
    /// <c>+1</c> for <see cref="NavigateDown"/> and <c>-1</c> for <see cref="NavigateUp"/>; the host
    /// owns where focus actually moves (this is the <c>Highlight</c>-equivalent role: navigation
    /// drives the <c>:focused</c> pseudo-state on the node the host moves to).
    /// </summary>
    public static void RegisterNavigation(ICommandRegistry registry, Action<int> focus)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(focus);

        registry.Register(NavigateDown, _ => focus(+1));
        registry.Register(NavigateUp, _ => focus(-1));
    }

    /// <summary>
    /// Register the <see cref="Kill"/> command. The handler asks <paramref name="confirm"/> before
    /// invoking <paramref name="kill"/> — the <c>KillProcessConfirm</c>-equivalent: a confirmation
    /// gate guards a destructive action on the firing node.
    /// </summary>
    public static void RegisterKill(
        ICommandRegistry registry,
        Func<ITreeNode, bool> confirm,
        Action<ITreeNode> kill)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(kill);

        registry.Register(Kill, ctx =>
        {
            if (confirm(ctx.Node))
            {
                kill(ctx.Node);
            }
        });
    }

    /// <summary>
    /// Register the <see cref="RenderSparkline"/> command and return the per-node sample buffers it
    /// fills. The <c>ResourceMeter</c>-equivalent: on each firing it reads the
    /// <paramref name="attribute"/> (e.g. <c>Cpu</c>) off the node and appends it to that node's
    /// ring buffer, which a projection later reads to draw the sparkline glyph.
    /// </summary>
    public static IReadOnlyDictionary<ITreeNode, SparklineBuffer> RegisterSparkline(
        ICommandRegistry registry,
        string attribute,
        int capacity = 60)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrEmpty(attribute);
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }

        var buffers = new ConcurrentDictionary<ITreeNode, SparklineBuffer>();

        registry.Register(RenderSparkline, ctx =>
        {
            if (ctx.Node.TryGetAttribute(attribute, out var raw)
                && TryToDouble(raw, out var sample))
            {
                var buffer = buffers.GetOrAdd(ctx.Node, _ => new SparklineBuffer(capacity));
                buffer.Add(sample);
            }
        });

        return buffers;
    }

    private static bool TryToDouble(object? raw, out double value)
    {
        switch (raw)
        {
            case double d:
                value = d;
                return true;
            case int i:
                value = i;
                return true;
            case long l:
                value = l;
                return true;
            case float f:
                value = f;
                return true;
            default:
                value = 0;
                return false;
        }
    }
}

/// <summary>
/// A fixed-capacity ring buffer of <see cref="double"/> samples backing a sparkline. Oldest
/// samples are overwritten once <see cref="Capacity"/> is reached.
/// </summary>
public sealed class SparklineBuffer
{
    private readonly double[] _samples;
    private int _start;

    /// <summary>Create a buffer holding up to <paramref name="capacity"/> samples.</summary>
    public SparklineBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }

        _samples = new double[capacity];
        Capacity = capacity;
    }

    /// <summary>Maximum number of retained samples.</summary>
    public int Capacity { get; }

    /// <summary>Number of samples currently held (≤ <see cref="Capacity"/>).</summary>
    public int Count { get; private set; }

    /// <summary>Append a sample, evicting the oldest when full.</summary>
    public void Add(double sample)
    {
        if (Count < Capacity)
        {
            _samples[(_start + Count) % Capacity] = sample;
            Count++;
        }
        else
        {
            _samples[_start] = sample;
            _start = (_start + 1) % Capacity;
        }
    }

    /// <summary>Copy the retained samples in oldest-to-newest order.</summary>
    public double[] ToArray()
    {
        var result = new double[Count];
        for (var i = 0; i < Count; i++)
        {
            result[i] = _samples[(_start + i) % Capacity];
        }

        return result;
    }
}
