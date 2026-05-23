using System.Text.Json.Nodes;
using Strata.Adapters.JsonNode;
using Strata.Css;
using Strata.JsonPath;

namespace Strata.JsonPath.Tests;

public class JsonPathSelectorTests
{
    private static readonly JsonPathSelectorLanguage Lang = new();

    private static ITreeNode WrapState()
    {
        // A small "application state tree": users with roles, plus a settings object.
        var json = JsonNode.Parse(
            """
            {
              "users": [
                { "$type": "user", "$id": "u1", "role": "admin", "name": "Ada" },
                { "$type": "user", "$id": "u2", "role": "user",  "name": "Boris" },
                { "$type": "user", "$id": "u3", "role": "admin", "name": "Chen" }
              ],
              "settings": { "$type": "settings", "theme": "dark" }
            }
            """)!;

        return new JsonTreeAdapter().Wrap(json);
    }

    [Fact]
    public void Language_name_is_jsonpath()
    {
        Lang.Name.Should().Be("jsonpath");
    }

    [Fact]
    public void Parse_invalid_path_throws_format_exception()
    {
        var act = () => Lang.Parse("$.[");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_null_throws()
    {
        var act = () => Lang.Parse(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Wildcard_matches_every_array_element()
    {
        var root = WrapState();
        var selector = Lang.Parse("$.users[*]");

        var names = selector.Find(root)
            .Select(m => Name(m.Node))
            .ToArray();

        names.Should().Equal("Ada", "Boris", "Chen");
    }

    [Fact]
    public void Slice_matches_the_addressed_range()
    {
        var root = WrapState();
        var selector = Lang.Parse("$.users[0:2]");

        var names = selector.Find(root)
            .Select(m => Name(m.Node))
            .ToArray();

        names.Should().Equal("Ada", "Boris");
    }

    [Fact]
    public void Filter_matches_predicate_satisfying_nodes()
    {
        var root = WrapState();
        var selector = Lang.Parse("$.users[?@.role == 'admin']");

        var names = selector.Find(root)
            .Select(m => Name(m.Node))
            .ToArray();

        names.Should().Equal("Ada", "Chen");
    }

    [Fact]
    public void Captures_populate_location_and_addressable_segments()
    {
        var root = WrapState();
        var selector = Lang.Parse("$.users[?@.role == 'admin']");

        var match = selector.Find(root).First();

        match.Context.Captures.Should().ContainKey("location");
        match.Context.Captures["location"].Should().Be("$['users'][0]");

        // Addressable segments expose the resolved slice address: users[0].
        match.Context.Captures["$0"].Should().Be("users");
        match.Context.Captures["$1"].Should().Be(0);
    }

    [Fact]
    public void Matches_filters_a_relative_candidate_against_the_whole_document()
    {
        var root = WrapState();
        var selector = Lang.Parse("$.users[?@.role == 'admin']");

        var adminU1 = Descendants(root).Single(n => n.Id == "u1");
        var plainU2 = Descendants(root).Single(n => n.Id == "u2");

        selector.Matches(adminU1, out var ctx).Should().BeTrue();
        ctx.Captures["location"].Should().Be("$['users'][0]");

        selector.Matches(plainU2, out _).Should().BeFalse();
    }

    [Fact]
    public void Matches_returns_false_for_non_json_underlying()
    {
        var selector = Lang.Parse("$.users[*]");
        var foreign = new ForeignNode();

        selector.Matches(foreign, out var ctx).Should().BeFalse();
        ctx.Should().Be(MatchContext.Empty);
    }

    [Fact]
    public void Find_yields_nothing_for_non_json_root()
    {
        var selector = Lang.Parse("$.users[*]");
        selector.Find(new ForeignNode()).Should().BeEmpty();
    }

    // --- Success criterion from docs/04-plan.md §Phase 9 ---
    // `$.users[?@.role == 'admin']` matches the same logical nodes as the CSS selector for the
    // same concept. JSON nodes carry no CSS classes, so the canonical `.user` class maps to the
    // node Kind (sourced from `$type`); the equivalence is asserted on identical ITreeNode refs.
    [Fact]
    public void Jsonpath_filter_matches_same_logical_nodes_as_css()
    {
        var root = WrapState();

        var css = new CssSelectorLanguage();
        var cssSelector = css.Parse("user[role=\"admin\"]");
        var jsonPathSelector = Lang.Parse("$.users[?@.role == 'admin']");

        var cssMatches = cssSelector.Find(root).Select(m => m.Node).ToHashSet();
        var jsonPathMatches = jsonPathSelector.Find(root).Select(m => m.Node).ToHashSet();

        jsonPathMatches.Should().BeEquivalentTo(cssMatches);
        jsonPathMatches.Should().HaveCount(2);
        jsonPathMatches.Select(n => n.Id).Should().BeEquivalentTo(new[] { "u1", "u3" });
    }

    [Fact]
    public void Legacy_goessner_filter_syntax_is_tolerated_and_equivalent()
    {
        var root = WrapState();

        var rfc = Lang.Parse("$.users[?@.role == 'admin']");
        var legacy = Lang.Parse("$.users[?(@.role == 'admin')]");

        var rfcIds = rfc.Find(root).Select(m => m.Node.Id).ToArray();
        var legacyIds = legacy.Find(root).Select(m => m.Node.Id).ToArray();

        legacyIds.Should().Equal(rfcIds);
    }

    private static string? Name(ITreeNode node)
    {
        node.TryGetAttribute("name", out var value);
        return value?.ToString();
    }

    private static IEnumerable<ITreeNode> Descendants(ITreeNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var d in Descendants(child))
            {
                yield return d;
            }
        }
    }

    private sealed class ForeignNode : ITreeNode
    {
        public string Kind => "foreign";

        public string? Id => null;

        public IReadOnlySet<string> Classes { get; } = new HashSet<string>();

        public IReadOnlySet<string> PseudoStates { get; } = new HashSet<string>();

        public ITreeNode? Parent => null;

        public IEnumerable<ITreeNode> Children => Enumerable.Empty<ITreeNode>();

        public object? Underlying => "not a json node";

        public bool TryGetAttribute(string name, out object? value)
        {
            value = null;
            return false;
        }
    }
}
