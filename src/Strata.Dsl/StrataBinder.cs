using System.Text.Json.Nodes;
using Json.Path;

namespace Strata.Dsl;

/// <summary>
/// Resolves data bindings on a UI tree against the reactive store's state. An element with a
/// <c>bind-text</c> attribute gets its <c>text</c> attribute set from the JSONPath-resolved
/// scalar; an element with <c>bind-data</c> gets its <c>data</c> attribute set from the resolved
/// array. The binder mutates element attributes in place, then the host re-cascades and projects.
/// </summary>
public static class StrataBinder
{
    /// <summary>Apply every binding in the tree rooted at <paramref name="root"/> against <paramref name="state"/>.</summary>
    public static void Apply(StrataElement root, JsonObject state)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(state);
        ApplyNode(root, state);
    }

    private static void ApplyNode(StrataElement element, JsonObject state)
    {
        if (element.TryGetAttribute("bind-text", out var textPath) && textPath is string tp)
        {
            var node = ResolveFirst(tp, state);
            if (node is not null)
            {
                element.SetAttribute("text", ScalarString(node));
            }
        }

        if (element.TryGetAttribute("bind-data", out var dataPath) && dataPath is string dp)
        {
            var node = ResolveFirst(dp, state);
            if (node is JsonArray array)
            {
                element.SetAttribute("data", ToDoubleArray(array));
            }
        }

        foreach (var child in element.Children)
        {
            if (child is StrataElement strataChild)
            {
                ApplyNode(strataChild, state);
            }
        }
    }

    private static JsonNode? ResolveFirst(string jsonPath, JsonObject state)
    {
        // Invalid paths throw here (fail fast); the host surfaces it to the author.
        var path = JsonPath.Parse(jsonPath);
        var result = path.Evaluate(state);
        foreach (var match in result.Matches)
        {
            return match.Value;
        }

        return null;
    }

    private static string ScalarString(JsonNode node)
        => node is JsonValue value ? value.ToString() : node.ToJsonString();

    private static double[] ToDoubleArray(JsonArray array)
    {
        var list = new List<double>(array.Count);
        foreach (var item in array)
        {
            if (item is JsonValue value && value.TryGetValue<double>(out var d))
            {
                list.Add(d);
            }
            else if (item is not null && double.TryParse(item.ToString(), out var parsed))
            {
                list.Add(parsed);
            }
        }

        return list.ToArray();
    }
}
