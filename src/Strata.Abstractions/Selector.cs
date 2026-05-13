namespace Strata;

/// <summary>
/// Parses textual selector source for a specific language (CSS subset, JSONPath, etc.)
/// into <see cref="ISelector"/> instances. New languages are added by implementing this
/// interface in a separate package; core never references concrete languages.
/// </summary>
public interface ISelectorLanguage
{
    /// <summary>Unique, stable name of this selector language (e.g. <c>"css"</c>, <c>"jsonpath"</c>).</summary>
    string Name { get; }

    /// <summary>
    /// Parse selector source for this language. Implementations MUST throw on syntactically
    /// invalid input rather than producing a never-matching selector.
    /// </summary>
    ISelector Parse(string source);
}

/// <summary>
/// A compiled selector. Selectors are evaluated against tree nodes to determine which
/// rules apply during cascade. Selectors are immutable, thread-safe, and cacheable.
/// </summary>
public interface ISelector
{
    /// <summary>Selector specificity, ordered lexicographically by <c>(A, B, C)</c>.</summary>
    Specificity Specificity { get; }

    /// <summary>
    /// Does this selector match the given node? On a positive match, populates
    /// <paramref name="context"/> with any captures the selector produced.
    /// </summary>
    bool Matches(ITreeNode node, out MatchContext context);

    /// <summary>
    /// Enumerate all nodes in the tree rooted at <paramref name="root"/> that this selector
    /// matches. Order is selector-defined but MUST be deterministic for a given input tree.
    /// </summary>
    IEnumerable<Match> Find(ITreeNode root);
}

/// <summary>
/// CSS-style selector specificity, ordered lexicographically as
/// <c>(A: ids, B: classes + attributes + pseudo-classes, C: types)</c>.
/// </summary>
public readonly record struct Specificity(int A, int B, int C) : IComparable<Specificity>
{
    /// <summary>Zero specificity (used for universal selector and <c>:where(...)</c>).</summary>
    public static Specificity Zero => default;

    /// <summary>Compares two specificities lexicographically on <c>(A, B, C)</c>.</summary>
    public int CompareTo(Specificity other)
    {
        var dA = A - other.A;
        if (dA != 0)
        {
            return dA;
        }

        var dB = B - other.B;
        if (dB != 0)
        {
            return dB;
        }

        return C - other.C;
    }

    /// <summary>Component-wise specificity addition.</summary>
    public static Specificity operator +(Specificity x, Specificity y)
        => new(x.A + y.A, x.B + y.B, x.C + y.C);

    /// <inheritdoc cref="CompareTo(Specificity)"/>
    public static bool operator <(Specificity x, Specificity y) => x.CompareTo(y) < 0;

    /// <inheritdoc cref="CompareTo(Specificity)"/>
    public static bool operator >(Specificity x, Specificity y) => x.CompareTo(y) > 0;

    /// <inheritdoc cref="CompareTo(Specificity)"/>
    public static bool operator <=(Specificity x, Specificity y) => x.CompareTo(y) <= 0;

    /// <inheritdoc cref="CompareTo(Specificity)"/>
    public static bool operator >=(Specificity x, Specificity y) => x.CompareTo(y) >= 0;
}

/// <summary>A node that matched a selector together with any captures the selector produced.</summary>
public readonly record struct Match(ITreeNode Node, MatchContext Context);

/// <summary>
/// Per-match captures. Selector languages with wildcards or predicates surface captured
/// values here so projections can read them (e.g. routing parameters from JSONPath).
/// </summary>
public readonly struct MatchContext : IEquatable<MatchContext>
{
    /// <summary>An empty <see cref="MatchContext"/> with no captures.</summary>
    public static MatchContext Empty { get; } = new() { Captures = EmptyCaptures.Instance };

    /// <summary>Captured key/value pairs produced by selector matching. Never <see langword="null"/>.</summary>
    public required IReadOnlyDictionary<string, object?> Captures { get; init; }

    /// <inheritdoc/>
    public bool Equals(MatchContext other) => ReferenceEquals(Captures, other.Captures);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is MatchContext other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Captures?.GetHashCode() ?? 0;

    /// <summary>Reference equality on the captures dictionary.</summary>
    public static bool operator ==(MatchContext left, MatchContext right) => left.Equals(right);

    /// <summary>Reference inequality on the captures dictionary.</summary>
    public static bool operator !=(MatchContext left, MatchContext right) => !left.Equals(right);
}

internal sealed class EmptyCaptures : IReadOnlyDictionary<string, object?>
{
    public static readonly EmptyCaptures Instance = new();

    private EmptyCaptures() { }

    public object? this[string key] => throw new KeyNotFoundException(key);

    public IEnumerable<string> Keys => Array.Empty<string>();

    public IEnumerable<object?> Values => Array.Empty<object?>();

    public int Count => 0;

    public bool ContainsKey(string key) => false;

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        => System.Linq.Enumerable.Empty<KeyValuePair<string, object?>>().GetEnumerator();

    public bool TryGetValue(string key, out object? value)
    {
        value = null;
        return false;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
