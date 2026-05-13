using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace Strata.Adapters.JsonNode;

/// <summary>
/// Wraps <see cref="System.Text.Json.Nodes.JsonNode"/> instances as <see cref="ITreeNode"/>s.
/// </summary>
/// <remarks>
/// Children of a <see cref="JsonObject"/> are its named properties; children of a
/// <see cref="JsonArray"/> are its elements. <see cref="JsonValue"/> nodes have no children.
///
/// <para>Wrappers are cached per source <see cref="System.Text.Json.Nodes.JsonNode"/> via
/// <see cref="ConditionalWeakTable{TKey,TValue}"/>, satisfying identity semantics without
/// preventing GC of the wrapped node.</para>
/// </remarks>
public sealed class JsonTreeAdapter : ITreeAdapter<global::System.Text.Json.Nodes.JsonNode>
{
    private readonly ConditionalWeakTable<global::System.Text.Json.Nodes.JsonNode, JsonTreeNode> _cache = new();

    /// <inheritdoc/>
    public ITreeNode Wrap(global::System.Text.Json.Nodes.JsonNode source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return WrapInternal(source, parent: null, name: null);
    }

    internal JsonTreeNode WrapInternal(global::System.Text.Json.Nodes.JsonNode source, JsonTreeNode? parent, string? name)
    {
        if (_cache.TryGetValue(source, out var existing))
        {
            return existing;
        }

        var node = new JsonTreeNode(source, this, parent, name);
        _cache.Add(source, node);
        return node;
    }

    internal IEnumerable<ITreeNode> EnumerateChildren(JsonTreeNode node)
    {
        switch (node.Source)
        {
            case JsonObject obj:
                foreach (var (k, v) in obj)
                {
                    if (v is null)
                    {
                        continue;
                    }

                    // Skip $type / $id reserved properties — they're metadata, not children.
                    if (k is "$type" or "$id")
                    {
                        continue;
                    }

                    yield return WrapInternal(v, parent: node, name: k);
                }

                break;

            case JsonArray arr:
                foreach (var v in arr)
                {
                    if (v is null)
                    {
                        continue;
                    }

                    yield return WrapInternal(v, parent: node, name: null);
                }

                break;
        }
    }
}
