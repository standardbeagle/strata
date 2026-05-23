using System.Text.Json.Nodes;
using Json.Path;

namespace Strata.JsonPath;

/// <summary>
/// An <see cref="ISelector"/> compiled from a single JSONPath (RFC 9535) source string.
/// </summary>
/// <remarks>
/// <para>
/// JSONPath is evaluated against the <see cref="System.Text.Json.Nodes.JsonNode"/> exposed by
/// <see cref="ITreeNode.Underlying"/>. The selector therefore matches any tree whose nodes wrap
/// <c>JsonNode</c> values (for example the <c>JsonTreeAdapter</c>), without this package taking a
/// dependency on any specific adapter: the bridge is the <see cref="ITreeNode.Underlying"/>
/// contract member alone.
/// </para>
/// <para>
/// Result <c>JsonNode</c>s produced by the underlying engine are mapped back to
/// <see cref="ITreeNode"/> instances by reference identity against the tree rooted at the
/// evaluation root, so the returned matches are the same logical nodes the rest of Strata sees.
/// </para>
/// </remarks>
public sealed class JsonPathSelector : ISelector
{
    private readonly Json.Path.JsonPath _path;
    private readonly string _source;

    internal JsonPathSelector(string source, Json.Path.JsonPath path, Specificity specificity)
    {
        _source = source;
        _path = path;
        Specificity = specificity;
    }

    /// <inheritdoc/>
    public Specificity Specificity { get; }

    /// <summary>The original JSONPath source for diagnostics.</summary>
    public string Source => _source;

    /// <inheritdoc/>
    public bool Matches(ITreeNode node, out MatchContext context)
    {
        ArgumentNullException.ThrowIfNull(node);
        context = MatchContext.Empty;

        if (node.Underlying is not JsonNode target)
        {
            return false;
        }

        var root = EvaluationRoot(node);
        if (root.Underlying is not JsonNode rootJson)
        {
            return false;
        }

        var result = _path.Evaluate(rootJson);
        foreach (var resultNode in result.Matches)
        {
            if (ReferenceEquals(resultNode.Value, target))
            {
                context = BuildContext(resultNode);
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public IEnumerable<Match> Find(ITreeNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.Underlying is not JsonNode rootJson)
        {
            yield break;
        }

        var result = _path.Evaluate(rootJson);
        foreach (var resultNode in result.Matches)
        {
            if (resultNode.Value is not JsonNode value)
            {
                continue;
            }

            var matched = FindByUnderlying(root, value);
            if (matched is not null)
            {
                yield return new Match(matched, BuildContext(resultNode));
            }
        }
    }

    /// <summary>
    /// Captures expose the matched node's normalized JSONPath location (the RFC 9535 normalized
    /// path) under <c>"location"</c>, and each addressable name/index segment as its own capture
    /// keyed by ordinal (<c>"$0"</c>, <c>"$1"</c>, ...) so router projections can read out the
    /// slice that produced a match.
    /// </summary>
    private static MatchContext BuildContext(Node resultNode)
    {
        var location = resultNode.Location?.ToString();
        if (location is null)
        {
            return MatchContext.Empty;
        }

        var captures = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["location"] = location,
        };

        var segments = JsonPathCaptures.AddressableSegments(resultNode.Location);
        for (var i = 0; i < segments.Count; i++)
        {
            captures[$"${i}"] = segments[i];
        }

        return new MatchContext { Captures = captures };
    }

    /// <summary>
    /// Walk to the root of the tree that owns <paramref name="node"/>. JSONPath is always rooted
    /// at <c>$</c>, so a relative <see cref="Matches(ITreeNode, out MatchContext)"/> query is
    /// evaluated against the whole document and then filtered to the candidate node.
    /// </summary>
    private static ITreeNode EvaluationRoot(ITreeNode node)
    {
        var cursor = node;
        while (cursor.Parent is not null)
        {
            cursor = cursor.Parent;
        }

        return cursor;
    }

    private static ITreeNode? FindByUnderlying(ITreeNode root, JsonNode target)
    {
        if (ReferenceEquals(root.Underlying, target))
        {
            return root;
        }

        foreach (var child in root.Children)
        {
            var found = FindByUnderlying(child, target);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
