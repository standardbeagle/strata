using Json.Path;

namespace Strata.JsonPath;

/// <summary>
/// The JSONPath (RFC 9535) <see cref="ISelectorLanguage"/>, backed by JsonPath.Net.
/// </summary>
/// <remarks>
/// Registered alongside <c>CssSelectorLanguage</c> to demonstrate that the cascade engine is
/// selector-language agnostic: the same rules/cascade pipeline consumes selectors from either
/// language. See docs/04-plan.md §Phase 9.
/// </remarks>
public sealed class JsonPathSelectorLanguage : ISelectorLanguage
{
    /// <inheritdoc/>
    public string Name => "jsonpath";

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">
    /// <paramref name="source"/> is not a syntactically valid JSONPath. Per the
    /// <see cref="ISelectorLanguage.Parse(string)"/> contract, invalid input throws rather than
    /// producing a never-matching selector.
    /// </exception>
    public ISelector Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Json.Path.JsonPath path;
        try
        {
            path = Json.Path.JsonPath.Parse(source);
        }
        catch (PathParseException ex)
        {
            throw new FormatException($"Invalid JSONPath selector: '{source}'. {ex.Message}", ex);
        }

        return new JsonPathSelector(source, path, JsonPathSpecificity.Compute(path));
    }
}
