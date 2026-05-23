using Strata;
using Strata.JsonPath;

namespace Strata.Demo.RouterProjection;

/// <summary>
/// A handler descriptor produced by routing a state-tree slice. This is the "projection output":
/// the engine never invokes the handler, it only describes which handler a slice routes to and
/// which slice address triggered it.
/// </summary>
/// <param name="Handler">The logical handler name the matched slice routes to.</param>
/// <param name="Location">The normalized JSONPath location of the matched slice (e.g. <c>$['users'][0]</c>).</param>
/// <param name="Captures">The addressable segments of the matched slice, in document order.</param>
public sealed record HandlerDescriptor(string Handler, string Location, IReadOnlyList<object?> Captures);

/// <summary>
/// A single route: a JSONPath selector paired with the handler name slices it selects route to.
/// </summary>
/// <param name="Selector">JSONPath source.</param>
/// <param name="Handler">Handler name.</param>
public sealed record Route(string Selector, string Handler);

/// <summary>
/// Maps state-tree slices to <see cref="HandlerDescriptor"/>s using JSONPath selectors — the
/// "router projection" deliverable from docs/04-plan.md §Phase 9. Demonstrates that JSONPath is a
/// peer selector language: the same <see cref="ISelector"/> contract that drives the CSS cascade
/// here drives a routing table instead.
/// </summary>
public sealed class RouterProjection
{
    private readonly IReadOnlyList<(ISelector Selector, string Handler)> _routes;

    /// <summary>Compile a routing table. Invalid JSONPath in any route throws at construction.</summary>
    public RouterProjection(IEnumerable<Route> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        var lang = new JsonPathSelectorLanguage();
        _routes = routes
            .Select(r => (lang.Parse(r.Selector), r.Handler))
            .ToArray();
    }

    /// <summary>
    /// Project the state tree rooted at <paramref name="root"/> into one handler descriptor per
    /// matched slice, in route order then document order.
    /// </summary>
    public IReadOnlyList<HandlerDescriptor> Project(ITreeNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var descriptors = new List<HandlerDescriptor>();
        foreach (var (selector, handler) in _routes)
        {
            foreach (var match in selector.Find(root))
            {
                var location = match.Context.Captures.TryGetValue("location", out var loc)
                    ? loc?.ToString() ?? string.Empty
                    : string.Empty;

                descriptors.Add(new HandlerDescriptor(handler, location, Addresses(match.Context.Captures)));
            }
        }

        return descriptors;
    }

    private static List<object?> Addresses(IReadOnlyDictionary<string, object?> captures)
    {
        var values = new List<object?>();
        for (var i = 0; captures.TryGetValue($"${i}", out var value); i++)
        {
            values.Add(value);
        }

        return values;
    }
}
