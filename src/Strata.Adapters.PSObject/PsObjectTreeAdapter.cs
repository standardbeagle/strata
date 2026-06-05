using System.Runtime.CompilerServices;

namespace Strata.Adapters.PSObject;

/// <summary>
/// Wraps PowerShell <see cref="System.Management.Automation.PSObject"/> instances as
/// <see cref="ITreeNode"/>s.
/// </summary>
/// <remarks>
/// Identity: wrappers are cached per source <see cref="System.Management.Automation.PSObject"/>
/// via <see cref="ConditionalWeakTable{TKey,TValue}"/>, so identity semantics hold and the
/// cache does not prevent garbage collection of the underlying PSObject.
///
/// <para>Selector hooks: every PSObject derivative (kind, id, classes, pseudo-states,
/// children) is computed by a caller-supplied delegate. The defaults give a reasonable
/// flat-tree view for simple <c>Get-Process</c>-style pipelines; richer scenarios
/// (Format-Styled with custom class predicates) override the relevant hooks.</para>
/// </remarks>
public sealed class PsObjectTreeAdapter : ITreeAdapter<global::System.Management.Automation.PSObject>
{
    /// <summary>Returns child <see cref="System.Management.Automation.PSObject"/>s of a node.</summary>
    public delegate IEnumerable<global::System.Management.Automation.PSObject> ChildAccessor(
        global::System.Management.Automation.PSObject parent);

    /// <summary>Returns the <c>Kind</c> string for a wrapped PSObject.</summary>
    public delegate string KindSelector(global::System.Management.Automation.PSObject source);

    /// <summary>Returns the full type-identity chain (<c>IKindHierarchy.KindHierarchy</c>) for a wrapped PSObject.</summary>
    public delegate IReadOnlyList<string> KindHierarchySelector(global::System.Management.Automation.PSObject source);

    /// <summary>Returns the optional <c>Id</c> string for a wrapped PSObject.</summary>
    public delegate string? IdSelector(global::System.Management.Automation.PSObject source);

    /// <summary>Returns the class labels for a wrapped PSObject.</summary>
    public delegate IEnumerable<string> ClassSelector(global::System.Management.Automation.PSObject source);

    /// <summary>Returns the active pseudo-states for a wrapped PSObject.</summary>
    public delegate IEnumerable<string> PseudoStateSelector(global::System.Management.Automation.PSObject source);

    private readonly ConditionalWeakTable<global::System.Management.Automation.PSObject, PsObjectNode> _cache = new();
    private readonly ChildAccessor _childAccessor;
    private readonly KindSelector _kind;
    private readonly KindHierarchySelector _kindHierarchy;
    private readonly IdSelector _id;
    private readonly ClassSelector _classes;
    private readonly PseudoStateSelector _pseudoStates;

    /// <summary>Create an adapter with the supplied selector hooks; <see langword="null"/> for any hook uses the built-in default.</summary>
    public PsObjectTreeAdapter(
        ChildAccessor? childAccessor = null,
        KindSelector? kind = null,
        IdSelector? id = null,
        ClassSelector? classes = null,
        PseudoStateSelector? pseudoStates = null,
        KindHierarchySelector? kindHierarchy = null)
    {
        _childAccessor = childAccessor ?? s_flatChildren;
        _kind = kind ?? DefaultKind;
        _kindHierarchy = kindHierarchy ?? DefaultKindHierarchy;
        _id = id ?? DefaultId;
        _classes = classes ?? s_emptyClasses;
        _pseudoStates = pseudoStates ?? s_emptyPseudoStates;
    }

    /// <summary>Configuration record for fluent adapter construction.</summary>
    public sealed class Options
    {
        /// <summary>Child accessor; default returns no children (flat list).</summary>
        public ChildAccessor? Children { get; set; }

        /// <summary>Kind selector; default is <c>TypeNames[0]</c> with namespace stripped.</summary>
        public KindSelector? Kind { get; set; }

        /// <summary>Kind-hierarchy selector; default is the full <c>TypeNames</c> chain, each namespace stripped and de-duplicated.</summary>
        public KindHierarchySelector? KindHierarchy { get; set; }

        /// <summary>Id selector; default reads <c>Id</c> then <c>Name</c> properties.</summary>
        public IdSelector? Id { get; set; }

        /// <summary>Class selector; default returns no classes.</summary>
        public ClassSelector? Classes { get; set; }

        /// <summary>Pseudo-state selector; default returns no states.</summary>
        public PseudoStateSelector? PseudoStates { get; set; }
    }

    /// <summary>Create an adapter from an <see cref="Options"/> configuration.</summary>
    public static PsObjectTreeAdapter Create(Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new PsObjectTreeAdapter(
            options.Children,
            options.Kind,
            options.Id,
            options.Classes,
            options.PseudoStates,
            options.KindHierarchy);
    }

    /// <inheritdoc/>
    public ITreeNode Wrap(global::System.Management.Automation.PSObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return WrapInternal(source, parent: null);
    }

    internal PsObjectNode WrapInternal(global::System.Management.Automation.PSObject source, PsObjectNode? parent)
    {
        if (_cache.TryGetValue(source, out var existing))
        {
            return existing;
        }

        var node = new PsObjectNode(
            source,
            this,
            parent,
            _kind(source),
            _kindHierarchy(source),
            _id(source),
            ToReadOnlySet(_classes(source)),
            ToReadOnlySet(_pseudoStates(source)));
        _cache.Add(source, node);
        return node;
    }

    internal IEnumerable<ITreeNode> EnumerateChildren(PsObjectNode node)
    {
        foreach (var child in _childAccessor(node.Source))
        {
            yield return WrapInternal(child, parent: node);
        }
    }

    private static IReadOnlySet<string> ToReadOnlySet(IEnumerable<string>? source)
    {
        if (source is null)
        {
            return EmptyStringSet.Instance;
        }

        if (source is IReadOnlySet<string> set)
        {
            return set;
        }

        var hashSet = new HashSet<string>(source, StringComparer.Ordinal);
        return hashSet.Count == 0 ? EmptyStringSet.Instance : hashSet;
    }

    private static readonly ChildAccessor s_flatChildren =
        _ => Array.Empty<global::System.Management.Automation.PSObject>();

    private static readonly ClassSelector s_emptyClasses =
        _ => Array.Empty<string>();

    private static readonly PseudoStateSelector s_emptyPseudoStates =
        _ => Array.Empty<string>();

    /// <summary>Default <see cref="KindSelector"/>: <c>TypeNames[0]</c> with namespace stripped.</summary>
    public static string DefaultKind(global::System.Management.Automation.PSObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var first = source.TypeNames.Count > 0 ? source.TypeNames[0] : null;
        if (!string.IsNullOrEmpty(first))
        {
            var lastDot = first.LastIndexOf('.');
            return lastDot >= 0 ? first[(lastDot + 1)..] : first;
        }

        return source.BaseObject?.GetType().Name ?? "PSObject";
    }

    /// <summary>
    /// Default <see cref="KindHierarchySelector"/>: the object's full <c>TypeNames</c> chain
    /// (most-derived first), each namespace stripped and de-duplicated in order — e.g. a
    /// <c>Get-ChildItem</c> file yields <c>FileInfo</c>, <c>FileSystemInfo</c>,
    /// <c>MarshalByRefObject</c>, <c>Object</c>. This lets a base-type selector
    /// (<c>FileSystemInfo</c>) match every derived kind. Falls back to the runtime type name when
    /// <c>TypeNames</c> is empty.
    /// </summary>
    public static IReadOnlyList<string> DefaultKindHierarchy(global::System.Management.Automation.PSObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var names = source.TypeNames;
        if (names is null || names.Count == 0)
        {
            var fallback = source.BaseObject?.GetType().Name;
            return string.IsNullOrEmpty(fallback) ? Array.Empty<string>() : new[] { fallback };
        }

        var result = new List<string>(names.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var lastDot = name.LastIndexOf('.');
            var bare = lastDot >= 0 ? name[(lastDot + 1)..] : name;
            if (seen.Add(bare))
            {
                result.Add(bare);
            }
        }

        return result;
    }

    /// <summary>Default <see cref="IdSelector"/>: reads <c>Id</c> then <c>Name</c> properties.</summary>
    public static string? DefaultId(global::System.Management.Automation.PSObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var id = source.Properties["Id"]?.Value ?? source.Properties["Name"]?.Value;
        return id?.ToString();
    }

    /// <summary>
    /// Compose a <see cref="ClassSelector"/> from a sequence of <c>(class-name, predicate)</c>
    /// rules. Each rule's predicate is tested; if true, the class name is included.
    /// </summary>
    public static ClassSelector ClassesFromRules(params (string ClassName, Predicate<global::System.Management.Automation.PSObject> Predicate)[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var captured = rules;
        return source =>
        {
            var list = new List<string>(captured.Length);
            foreach (var rule in captured)
            {
                if (rule.Predicate(source))
                {
                    list.Add(rule.ClassName);
                }
            }

            return list;
        };
    }

    /// <summary>
    /// Resolve classes from a named property on the PSObject. The property value may be a
    /// string (space-separated class names) or an <c>IEnumerable&lt;string&gt;</c>.
    /// </summary>
    public static ClassSelector ClassesFromProperty(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        return source =>
        {
            var prop = source.Properties[propertyName];
            if (prop?.Value is null)
            {
                return Array.Empty<string>();
            }

            return prop.Value switch
            {
                string s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                IEnumerable<string> e => e,
                _ => Array.Empty<string>(),
            };
        };
    }
}
