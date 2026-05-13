namespace Strata.Core;

/// <summary>
/// Skeleton cascade engine for Phase 0. Computes per-node matched rules and winning
/// declarations via linear scan over all rules in the stylesheet.
/// </summary>
/// <remarks>
/// Per tech-design §2.1, a production cascade MUST maintain a subject-keyed rule index;
/// that landing is gated on selector languages exposing subject summaries (Phase 1, when
/// CSS lands). Until then, the linear scan is correctness-preserving but
/// O(rules × nodes × selector-cost).
///
/// <para><see cref="Update"/> is intentionally not implemented in Phase 0 — incremental
/// re-cascade is a Phase 2/Phase 5 deliverable.</para>
/// </remarks>
public sealed class Cascade : ICascade
{
    private readonly IPropertyRegistry _properties;

    /// <summary>Create a cascade engine bound to a property registry.</summary>
    public Cascade(IPropertyRegistry properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        _properties = properties;
    }

    /// <inheritdoc/>
    public ICascadeResult Compute(ITreeNode root, IStylesheet stylesheet)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(stylesheet);

        var byNode = new Dictionary<ITreeNode, NodeResult>();
        ComputeRecursive(root, stylesheet, byNode);

        return new CascadeResult(byNode, _properties, stylesheet.Version);
    }

    /// <inheritdoc/>
    public ICascadeResult Update(
        ICascadeResult prior,
        IReadOnlyList<TreeChange> treeChanges,
        IStylesheet? newStylesheet = null)
    {
        throw new NotImplementedException(
            "Incremental cascade lands in Phase 2 (full recompute) and Phase 5+ (selective). " +
            "Until then, call Compute directly.");
    }

    private static void ComputeRecursive(
        ITreeNode node,
        IStylesheet stylesheet,
        Dictionary<ITreeNode, NodeResult> byNode)
    {
        byNode[node] = ComputeNode(node, stylesheet);
        foreach (var child in node.Children)
        {
            ComputeRecursive(child, stylesheet, byNode);
        }
    }

    private static NodeResult ComputeNode(ITreeNode node, IStylesheet stylesheet)
    {
        var matched = new List<MatchedRule>();

        foreach (var rule in stylesheet.Rules)
        {
            if (rule.Selector.Matches(node, out var context))
            {
                matched.Add(new MatchedRule(rule, rule.Selector.Specificity, rule.SourceOrder, context));
            }
        }

        matched.Sort(RulePrecedenceComparer.Instance);

        var winning = new Dictionary<string, ResolvedDeclaration>(StringComparer.Ordinal);

        // Walk every (rule, declaration) pair; track per-property best per CompareForProperty.
        foreach (var mr in matched)
        {
            foreach (var d in mr.Rule.Declarations)
            {
                var candidate = new ResolvedDeclaration(mr.Rule, d, mr.Specificity, mr.SourceOrder);
                if (!winning.TryGetValue(d.Property, out var current))
                {
                    winning[d.Property] = candidate;
                }
                else if (RulePrecedenceComparer.CompareForProperty(candidate, current) < 0)
                {
                    winning[d.Property] = candidate;
                }
            }
        }

        return new NodeResult(matched, winning);
    }
}

internal sealed class NodeResult
{
    public NodeResult(List<MatchedRule> matchedOrdered, Dictionary<string, ResolvedDeclaration> winningByProperty)
    {
        MatchedOrdered = matchedOrdered;
        WinningByProperty = winningByProperty;
    }

    public List<MatchedRule> MatchedOrdered { get; }

    public Dictionary<string, ResolvedDeclaration> WinningByProperty { get; }
}

internal sealed class CascadeResult : ICascadeResult
{
    private readonly Dictionary<ITreeNode, NodeResult> _byNode;
    private readonly IPropertyRegistry _properties;

    public CascadeResult(Dictionary<ITreeNode, NodeResult> byNode, IPropertyRegistry properties, int stylesheetVersion)
    {
        _byNode = byNode;
        _properties = properties;
        StylesheetVersion = stylesheetVersion;
    }

    public int StylesheetVersion { get; }

    public TValue GetComputed<TValue>(ITreeNode node, string property)
        where TValue : IPropertyValue
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(property);

        // 1. Local declared value.
        if (_byNode.TryGetValue(node, out var local)
            && local.WinningByProperty.TryGetValue(property, out var rd))
        {
            return (TValue)rd.Value;
        }

        // 2. Inheritance walk (if descriptor inherits) — iterative, not recursive.
        if (_properties.TryGet(property, out var descriptor) && descriptor.Inherits)
        {
            var cursor = node.Parent;
            while (cursor is not null)
            {
                if (_byNode.TryGetValue(cursor, out var ancestor)
                    && ancestor.WinningByProperty.TryGetValue(property, out var inherited))
                {
                    return (TValue)inherited.Value;
                }

                cursor = cursor.Parent;
            }
        }

        // 3. Initial value.
        if (descriptor is not null)
        {
            return (TValue)descriptor.Initial;
        }

        throw new InvalidOperationException(
            $"Property '{property}' is not registered and has no declared value on this node.");
    }

    public IReadOnlyList<RuleApplication> GetMatchedRules(ITreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_byNode.TryGetValue(node, out var result))
        {
            return Array.Empty<RuleApplication>();
        }

        var applications = new RuleApplication[result.MatchedOrdered.Count];
        for (var i = 0; i < applications.Length; i++)
        {
            var mr = result.MatchedOrdered[i];
            applications[i] = new RuleApplication(mr.Rule, mr.Context);
        }

        return applications;
    }

    public PropertyOrigin GetOrigin(ITreeNode node, string property)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(property);

        if (_byNode.TryGetValue(node, out var local)
            && local.WinningByProperty.TryGetValue(property, out var rd))
        {
            return new PropertyOrigin(OriginKind.Declared, rd.Rule, null);
        }

        if (_properties.TryGet(property, out var descriptor) && descriptor.Inherits)
        {
            var cursor = node.Parent;
            while (cursor is not null)
            {
                if (_byNode.TryGetValue(cursor, out var ancestor)
                    && ancestor.WinningByProperty.ContainsKey(property))
                {
                    return new PropertyOrigin(OriginKind.Inherited, null, cursor);
                }

                cursor = cursor.Parent;
            }
        }

        return new PropertyOrigin(OriginKind.Initial, null, null);
    }
}
