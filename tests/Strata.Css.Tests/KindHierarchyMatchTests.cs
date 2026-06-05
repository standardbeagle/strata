namespace Strata.Css.Tests;

using Strata.Core.Tests.TestFixtures;

/// <summary>
/// Type selectors match a node by its primary <see cref="ITreeNode.Kind"/> OR any entry in its
/// <see cref="IKindHierarchy.KindHierarchy"/> chain, so a base-type rule (<c>FileSystemInfo</c>)
/// covers every derived kind (<c>FileInfo</c>, <c>DirectoryInfo</c>).
/// </summary>
public sealed class KindHierarchyMatchTests
{
    private static readonly CssSelectorLanguage Css = new();

    private static bool Match(string selector, ITreeNode node)
        => Css.Parse(selector).Matches(node, out _);

    [Fact]
    public void Leaf_kind_still_matches()
    {
        var file = new TestNode("FileInfo", kindHierarchy: new[] { "FileInfo", "FileSystemInfo", "Object" });
        Match("FileInfo", file).Should().BeTrue();
    }

    [Fact]
    public void Base_kind_matches_via_hierarchy()
    {
        var file = new TestNode("FileInfo", kindHierarchy: new[] { "FileInfo", "FileSystemInfo", "Object" });
        var dir = new TestNode("DirectoryInfo", kindHierarchy: new[] { "DirectoryInfo", "FileSystemInfo", "Object" });

        Match("FileSystemInfo", file).Should().BeTrue();
        Match("FileSystemInfo", dir).Should().BeTrue();
    }

    [Fact]
    public void Unrelated_kind_does_not_match()
    {
        var file = new TestNode("FileInfo", kindHierarchy: new[] { "FileInfo", "FileSystemInfo", "Object" });
        Match("Process", file).Should().BeFalse();
        Match("DirectoryInfo", file).Should().BeFalse();
    }

    [Fact]
    public void Node_without_explicit_hierarchy_matches_leaf_only()
    {
        // A TestNode given no hierarchy defaults to a single-element chain (its own Kind), so a
        // base-type selector that is not its Kind must not match — exact-match behavior preserved.
        var plain = new TestNode("FileInfo");
        Match("FileInfo", plain).Should().BeTrue();
        Match("FileSystemInfo", plain).Should().BeFalse();
    }

    [Fact]
    public void Compound_constraints_still_apply_on_a_hierarchy_match()
    {
        // Matching the Kind via the hierarchy does not waive the rest of the compound selector:
        // class / attribute / pseudo constraints are still evaluated against the node.
        var hot = new TestNode("FileInfo", classes: new[] { "big" }, kindHierarchy: new[] { "FileInfo", "FileSystemInfo" });
        var cold = new TestNode("FileInfo", kindHierarchy: new[] { "FileInfo", "FileSystemInfo" });

        Match("FileSystemInfo.big", hot).Should().BeTrue();
        Match("FileSystemInfo.big", cold).Should().BeFalse();
    }

    [Fact]
    public void Hierarchy_match_has_plain_type_selector_specificity()
    {
        // A base-type rule and a leaf-type rule are both type selectors → equal specificity
        // (0,0,1); source order decides between them, the normal CSS cascade.
        Css.Parse("FileSystemInfo").Specificity.Should().Be(new Specificity(0, 0, 1));
        Css.Parse("FileInfo").Specificity.Should().Be(new Specificity(0, 0, 1));
    }

    [Fact]
    public void Descendant_combinator_resolves_ancestor_by_base_kind()
    {
        // The hierarchy fallback applies to every compound in a complex selector, including
        // ancestors reached through a combinator.
        var container = new TestNode("DirectoryInfo", kindHierarchy: new[] { "DirectoryInfo", "FileSystemInfo" });
        var child = new TestNode("FileInfo", kindHierarchy: new[] { "FileInfo", "FileSystemInfo" });
        container.Add(child);

        Match("FileSystemInfo FileInfo", child).Should().BeTrue();
    }
}
