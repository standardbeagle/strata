using Json.Path;

namespace Strata.JsonPath;

/// <summary>
/// Extracts the addressable (name / index) steps from a normalized JSONPath location so that
/// matches produced by wildcards, slices, and filters surface the concrete slice that matched.
/// </summary>
internal static class JsonPathCaptures
{
    /// <summary>
    /// Returns the concrete name (<see cref="string"/>) and index (<see cref="int"/>) values of
    /// each segment in <paramref name="location"/>, in document order. A normalized location only
    /// ever contains single name or index selectors per segment, so this is the resolved address
    /// of the matched node (e.g. <c>$['users'][0]</c> → <c>["users", 0]</c>).
    /// </summary>
    public static IReadOnlyList<object?> AddressableSegments(Json.Path.JsonPath? location)
    {
        if (location?.Segments is not { } segments)
        {
            return Array.Empty<object?>();
        }

        var values = new List<object?>(segments.Length);
        foreach (var segment in segments)
        {
            foreach (var selector in segment.Selectors)
            {
                switch (selector)
                {
                    case NameSelector name:
                        values.Add(name.Name);
                        break;
                    case IndexSelector index:
                        values.Add(index.Index);
                        break;
                }
            }
        }

        return values;
    }
}
