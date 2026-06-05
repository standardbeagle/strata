namespace Strata;

/// <summary>
/// A node in a Strata-addressable tree. Adapters wrap source objects (PowerShell
/// <c>PSObject</c>, <see cref="System.Text.Json.Nodes.JsonNode"/>, schema ASTs, etc.) as
/// instances of this type so that selectors, cascade, and projections can operate
/// independently of any specific source representation.
/// </summary>
/// <remarks>
/// Identity semantics: two <see cref="ITreeNode"/> instances representing the same logical
/// node MUST compare equal via <see cref="object.Equals(object?)"/> and MUST produce the
/// same <see cref="object.GetHashCode"/>. Adapters SHOULD cache wrappers rather than
/// constructing fresh ones per access.
/// </remarks>
public interface ITreeNode
{
    /// <summary>
    /// Type-like identity for this node, analogous to a CSS element name (e.g. <c>"Process"</c>,
    /// <c>"Window"</c>, <c>"object"</c>). Case-sensitive. Required.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Optional unique identifier within the tree, analogous to CSS <c>#id</c>.
    /// </summary>
    string? Id { get; }

    /// <summary>Class labels for this node, analogous to CSS <c>.class</c>.</summary>
    IReadOnlySet<string> Classes { get; }

    /// <summary>
    /// Dynamic pseudo-states currently active on this node
    /// (e.g. <c>"focused"</c>, <c>"selected"</c>, <c>"hovered"</c>, <c>"expanded"</c>).
    /// Toggled by adapters at runtime.
    /// </summary>
    IReadOnlySet<string> PseudoStates { get; }

    /// <summary>Parent node, or <see langword="null"/> at the root.</summary>
    ITreeNode? Parent { get; }

    /// <summary>Ordered children of this node. May be empty.</summary>
    IEnumerable<ITreeNode> Children { get; }

    /// <summary>
    /// Read a named attribute by string key. Returns <see langword="false"/> if absent.
    /// </summary>
    /// <param name="name">Attribute name (case-sensitive).</param>
    /// <param name="value">Attribute value, or <see langword="null"/> on miss.</param>
    bool TryGetAttribute(string name, out object? value);

    /// <summary>
    /// The underlying object this node wraps, opaque to the engine. Projections and
    /// typed predicates use this to access source-specific data.
    /// </summary>
    object? Underlying { get; }
}

/// <summary>
/// Optional capability a <see cref="ITreeNode"/> MAY implement to expose, beyond its single
/// primary <see cref="ITreeNode.Kind"/>, the full chain of type identities it also answers to —
/// e.g. a PowerShell <c>FileInfo</c> row whose chain is <c>FileInfo</c>, <c>FileSystemInfo</c>,
/// <c>MarshalByRefObject</c>, <c>Object</c>. A CSS type selector matches the node when its element
/// name equals the primary <see cref="ITreeNode.Kind"/> <em>or</em> any entry in
/// <see cref="KindHierarchy"/>, so a stylesheet can target a base type (<c>FileSystemInfo</c>) to
/// style every derived kind at once.
/// </summary>
/// <remarks>
/// Nodes that do not implement this interface match by primary <see cref="ITreeNode.Kind"/> only —
/// the original exact-match behavior, unchanged. A type selector that matches via the hierarchy
/// carries the same specificity as one matching the primary kind (both are CSS type selectors), so
/// the cascade order between a base-type rule and a leaf-type rule follows normal source order.
/// </remarks>
public interface IKindHierarchy
{
    /// <summary>
    /// The node's full type-identity chain, most-derived first, each a bare element name
    /// (namespace stripped). Typically begins with the primary <see cref="ITreeNode.Kind"/>;
    /// MAY be empty. Case-sensitive, matching <see cref="ITreeNode.Kind"/>.
    /// </summary>
    IReadOnlyList<string> KindHierarchy { get; }
}

/// <summary>
/// Wraps a source object as an <see cref="ITreeNode"/>. Implementations are
/// source-type-specific (PSObject, JsonNode, schema AST, Redux state, etc.).
/// </summary>
/// <typeparam name="TSource">The source object type this adapter wraps.</typeparam>
public interface ITreeAdapter<in TSource>
{
    /// <summary>
    /// Wrap <paramref name="source"/> as a tree node. Implementations SHOULD return
    /// a cached wrapper for the same logical source so identity semantics hold.
    /// </summary>
    ITreeNode Wrap(TSource source);
}
