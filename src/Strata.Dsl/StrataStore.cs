using System.Globalization;
using System.Text.Json.Nodes;

namespace Strata.Dsl;

/// <summary>
/// The reactive state store for a live Strata app. Holds state as a <see cref="JsonObject"/> so
/// JSONPath bindings resolve against it, and raises <see cref="Changed"/> after every mutation so
/// the live host can re-render. Mutations address state by a dotted path (<c>$.a.b</c>);
/// intermediate objects are created on demand.
/// </summary>
public sealed class StrataStore
{
    /// <summary>The current state document. Bindings read from this via JSONPath.</summary>
    public JsonObject State { get; }

    /// <summary>Raised after any successful <see cref="Set"/> or <see cref="Append"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Create a store over an existing state object.</summary>
    public StrataStore(JsonObject state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <summary>Create a store from a JSON object string (the PowerShell surface passes a hashtable as JSON).</summary>
    public static StrataStore FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (JsonNode.Parse(json) is not JsonObject obj)
        {
            throw new FormatException("Strata store initial state must be a JSON object.");
        }

        return new StrataStore(obj);
    }

    /// <summary>Set the value at <paramref name="path"/>, creating intermediate objects as needed.</summary>
    public void Set(string path, object? value)
    {
        var (parent, key) = Navigate(path, create: true);
        parent[key] = ToNode(value);
        OnChanged();
    }

    /// <summary>
    /// Append <paramref name="value"/> to the array at <paramref name="path"/> (creating the array
    /// if absent). When <paramref name="cap"/> &gt; 0 and the array exceeds it, the oldest entries
    /// are dropped from the front so a scrolling-history window stays bounded.
    /// </summary>
    public void Append(string path, object? value, int cap = 0)
    {
        var (parent, key) = Navigate(path, create: true);
        if (parent[key] is not JsonArray array)
        {
            array = new JsonArray();
            parent[key] = array;
        }

        array.Add(ToNode(value));

        if (cap > 0)
        {
            while (array.Count > cap)
            {
                array.RemoveAt(0);
            }
        }

        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Walk a dotted path to the parent object of the final key. <c>$.a.b</c> returns the object
    /// at <c>$.a</c> and the key <c>b</c>. A leading <c>$</c> or <c>$.</c> is optional.
    /// </summary>
    private (JsonObject Parent, string Key) Navigate(string path, bool create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var trimmed = path.TrimStart('$', '.');
        var segments = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new FormatException($"Store path '{path}' addresses no property.");
        }

        var current = State;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (current[segment] is JsonObject next)
            {
                current = next;
            }
            else if (create)
            {
                var created = new JsonObject();
                current[segment] = created;
                current = created;
            }
            else
            {
                throw new KeyNotFoundException($"Store path '{path}' segment '{segment}' is not an object.");
            }
        }

        return (current, segments[^1]);
    }

    private static JsonNode? ToNode(object? value)
        => value switch
        {
            null => null,
            JsonNode node => node,
            string s => JsonValue.Create(s),
            bool b => JsonValue.Create(b),
            int i => JsonValue.Create(i),
            long l => JsonValue.Create(l),
            double d => JsonValue.Create(d),
            float f => JsonValue.Create((double)f),
            decimal m => JsonValue.Create((double)m),
            IConvertible conv => JsonValue.Create(conv.ToDouble(CultureInfo.InvariantCulture)),
            _ => JsonValue.Create(value.ToString()),
        };
}
