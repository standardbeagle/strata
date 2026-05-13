using System.Text.Json.Nodes;

namespace Strata.Adapters.JsonNode;

/// <summary>
/// <see cref="ITreeNode"/> wrapping a <see cref="System.Text.Json.Nodes.JsonNode"/>
/// (<see cref="JsonObject"/>, <see cref="JsonArray"/>, or <see cref="JsonValue"/>).
/// </summary>
public sealed class JsonTreeNode : ITreeNode
{
    internal JsonTreeNode(global::System.Text.Json.Nodes.JsonNode? source, JsonTreeAdapter adapter, JsonTreeNode? parent, string? name = null)
    {
        Source = source;
        _adapter = adapter;
        Parent = parent;
        NameInParent = name;

        Kind = ResolveKind(source);
        Id = ResolveId(source);
    }

    private readonly JsonTreeAdapter _adapter;

    /// <summary>The wrapped JsonNode. May be <see langword="null"/> for explicit JSON nulls.</summary>
    public global::System.Text.Json.Nodes.JsonNode? Source { get; }

    /// <summary>The property name under <see cref="Parent"/>, or <see langword="null"/> at the root or inside an array.</summary>
    public string? NameInParent { get; }

    /// <inheritdoc/>
    public string Kind { get; }

    /// <inheritdoc/>
    public string? Id { get; }

    /// <inheritdoc/>
    public IReadOnlySet<string> Classes { get; } = EmptyStringSet.Instance;

    /// <inheritdoc/>
    public IReadOnlySet<string> PseudoStates { get; } = EmptyStringSet.Instance;

    /// <inheritdoc/>
    public ITreeNode? Parent { get; }

    /// <inheritdoc/>
    public IEnumerable<ITreeNode> Children => _adapter.EnumerateChildren(this);

    /// <inheritdoc/>
    public object? Underlying => Source;

    /// <inheritdoc/>
    public bool TryGetAttribute(string name, out object? value)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (Source is JsonObject obj && obj.TryGetPropertyValue(name, out var child))
        {
            value = Unwrap(child);
            return true;
        }

        value = null;
        return false;
    }

    private static string ResolveKind(global::System.Text.Json.Nodes.JsonNode? source) => source switch
    {
        JsonObject obj when obj["$type"] is JsonValue tv && tv.TryGetValue<string>(out var t) => t,
        JsonObject => "object",
        JsonArray => "array",
        JsonValue => "value",
        null => "null",
        _ => source.GetType().Name,
    };

    private static string? ResolveId(global::System.Text.Json.Nodes.JsonNode? source)
    {
        if (source is JsonObject obj
            && obj["$id"] is JsonValue idVal
            && idVal.TryGetValue<string>(out var id))
        {
            return id;
        }

        return null;
    }

    internal static object? Unwrap(global::System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is not JsonValue value)
        {
            return node;
        }

        return value.GetValueKind() switch
        {
            System.Text.Json.JsonValueKind.String => value.GetValue<string>(),
            System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => value.GetValue<bool>(),
            System.Text.Json.JsonValueKind.Number => UnwrapNumber(value),
            System.Text.Json.JsonValueKind.Null => null,
            _ => node,
        };
    }

    private static object UnwrapNumber(JsonValue value)
    {
        if (value.TryGetValue<long>(out var l))
        {
            return l <= int.MaxValue && l >= int.MinValue ? (int)l : l;
        }

        if (value.TryGetValue<int>(out var i))
        {
            return i;
        }

        if (value.TryGetValue<double>(out var d))
        {
            return d;
        }

        if (value.TryGetValue<decimal>(out var m))
        {
            return m;
        }

        return value;
    }
}

internal sealed class EmptyStringSet : IReadOnlySet<string>
{
    public static readonly EmptyStringSet Instance = new();

    private EmptyStringSet() { }

    public int Count => 0;

    public bool Contains(string item) => false;

    public IEnumerator<string> GetEnumerator() => System.Linq.Enumerable.Empty<string>().GetEnumerator();

    public bool IsProperSubsetOf(IEnumerable<string> other) => other?.Any() ?? false;

    public bool IsProperSupersetOf(IEnumerable<string> other) => false;

    public bool IsSubsetOf(IEnumerable<string> other) => true;

    public bool IsSupersetOf(IEnumerable<string> other) => !(other?.Any() ?? false);

    public bool Overlaps(IEnumerable<string> other) => false;

    public bool SetEquals(IEnumerable<string> other) => !(other?.Any() ?? false);

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
