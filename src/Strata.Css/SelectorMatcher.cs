namespace Strata.Css;

internal static class SelectorMatcher
{
    public static bool Matches(ComplexSelector selector, ITreeNode node, IPseudoClassRegistry pseudos)
    {
        if (!MatchCompound(selector.Parts[0], node, pseudos))
        {
            return false;
        }

        ITreeNode? cursor = node;
        for (var i = 0; i < selector.Combinators.Length; i++)
        {
            var combinator = selector.Combinators[i];
            var next = selector.Parts[i + 1];

            cursor = combinator switch
            {
                Combinator.Child => MatchParent(cursor, next, pseudos),
                Combinator.Descendant => FindAncestor(cursor, next, pseudos),
                Combinator.AdjacentSibling => MatchPrevSibling(cursor, next, pseudos),
                Combinator.GeneralSibling => FindPrevSibling(cursor, next, pseudos),
                _ => null,
            };

            if (cursor is null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchCompound(CompoundSelector compound, ITreeNode node, IPseudoClassRegistry pseudos)
    {
        if (!compound.IsUniversal && compound.Kind is not null
            && !string.Equals(compound.Kind, node.Kind, StringComparison.Ordinal))
        {
            return false;
        }

        if (compound.Id is not null && !string.Equals(compound.Id, node.Id, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var cls in compound.Classes)
        {
            if (!node.Classes.Contains(cls))
            {
                return false;
            }
        }

        foreach (var attr in compound.Attributes)
        {
            if (!MatchAttribute(attr, node))
            {
                return false;
            }
        }

        foreach (var p in compound.Pseudos)
        {
            if (!MatchPseudo(p, node, pseudos))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchPseudo(PseudoEntry pseudo, ITreeNode node, IPseudoClassRegistry pseudos)
        => pseudo switch
        {
            SimplePseudo sp => pseudos.Test(sp.Name, node),
            NotPseudo np => !AnyMatchOf(np.Inner, node, pseudos),
            IsPseudo ip => AnyMatchOf(ip.Inner, node, pseudos),
            WherePseudo wp => AnyMatchOf(wp.Inner, node, pseudos),
            HasPseudo hp => HasDescendantMatch(hp.Inner, node, pseudos),
            NthChildPseudo nth => MatchNthChild(nth, node),
            _ => false,
        };

    private static bool AnyMatchOf(ComplexSelector[] selectors, ITreeNode node, IPseudoClassRegistry pseudos)
    {
        foreach (var s in selectors)
        {
            if (Matches(s, node, pseudos))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDescendantMatch(ComplexSelector relative, ITreeNode node, IPseudoClassRegistry pseudos)
    {
        // Treat :has(relative) as "exists a descendant that matches the inner selector
        // when evaluated with the descendant as the candidate". This is the lenient
        // interpretation per spec §3.1; the strict "relative selector" form (with
        // implicit anchor) is deferred until a real consumer demands it.
        foreach (var child in node.Children)
        {
            if (Matches(relative, child, pseudos))
            {
                return true;
            }

            if (HasDescendantMatch(relative, child, pseudos))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchNthChild(NthChildPseudo nth, ITreeNode node)
    {
        if (node.Parent is null)
        {
            return false;
        }

        var index = 1;
        foreach (var sibling in node.Parent.Children)
        {
            if (ReferenceEquals(sibling, node) || node.Equals(sibling))
            {
                break;
            }

            index++;
        }

        // index is 1-based. We want (n-th index satisfies index = a*k + b for some k >= 0).
        var diff = index - nth.B;
        if (nth.A == 0)
        {
            return diff == 0;
        }

        if (diff % nth.A != 0)
        {
            return false;
        }

        return diff / nth.A >= 0;
    }

    private static bool MatchAttribute(AttributeMatcher attr, ITreeNode node)
    {
        if (!node.TryGetAttribute(attr.Name, out var value))
        {
            return false;
        }

        if (attr.Op == AttrOp.Exists)
        {
            return true;
        }

        var s = value?.ToString();
        if (s is null)
        {
            return false;
        }

        var expected = attr.Value ?? string.Empty;
        return attr.Op switch
        {
            AttrOp.Equals => string.Equals(s, expected, StringComparison.Ordinal),
            AttrOp.StartsWith => s.StartsWith(expected, StringComparison.Ordinal),
            AttrOp.EndsWith => s.EndsWith(expected, StringComparison.Ordinal),
            AttrOp.Contains => s.Contains(expected, StringComparison.Ordinal),
            _ => false,
        };
    }

    private static ITreeNode? MatchParent(ITreeNode? node, CompoundSelector compound, IPseudoClassRegistry pseudos)
    {
        var parent = node?.Parent;
        if (parent is null)
        {
            return null;
        }

        return MatchCompound(compound, parent, pseudos) ? parent : null;
    }

    private static ITreeNode? FindAncestor(ITreeNode? node, CompoundSelector compound, IPseudoClassRegistry pseudos)
    {
        var cursor = node?.Parent;
        while (cursor is not null)
        {
            if (MatchCompound(compound, cursor, pseudos))
            {
                return cursor;
            }

            cursor = cursor.Parent;
        }

        return null;
    }

    private static ITreeNode? MatchPrevSibling(ITreeNode? node, CompoundSelector compound, IPseudoClassRegistry pseudos)
    {
        var prev = PrevSibling(node);
        if (prev is null)
        {
            return null;
        }

        return MatchCompound(compound, prev, pseudos) ? prev : null;
    }

    private static ITreeNode? FindPrevSibling(ITreeNode? node, CompoundSelector compound, IPseudoClassRegistry pseudos)
    {
        var cursor = PrevSibling(node);
        while (cursor is not null)
        {
            if (MatchCompound(compound, cursor, pseudos))
            {
                return cursor;
            }

            cursor = PrevSibling(cursor);
        }

        return null;
    }

    private static ITreeNode? PrevSibling(ITreeNode? node)
    {
        if (node?.Parent is null)
        {
            return null;
        }

        ITreeNode? prev = null;
        foreach (var sibling in node.Parent.Children)
        {
            if (ReferenceEquals(sibling, node) || node.Equals(sibling))
            {
                return prev;
            }

            prev = sibling;
        }

        return null;
    }
}
