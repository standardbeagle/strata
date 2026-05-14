namespace Strata.Phase0.Tests;

using System.Management.Automation;
using System.Text.Json.Nodes;
using Strata.Adapters.JsonNode;
using Strata.Adapters.PSObject;
using Strata.Css;

/// <summary>
/// Phase 1 smoke: CssSelectorLanguage produces the same logical match-set across both
/// adapters, just like the hand-written stand-in did in Phase 0. If this drifts apart,
/// the CSS selector is leaking tree-implementation assumptions.
/// </summary>
public sealed class CssEquivalenceTests
{
    private sealed record Row(int Id, string Name);

    private static readonly Row[] Sample =
    [
        new(1, "init"),
        new(1234, "chrome"),
        new(5678, "vim"),
    ];

    private static readonly CssSelectorLanguage Css = new();

    [Theory]
    [InlineData("Process")]
    [InlineData("Process[Name=\"chrome\"]")]
    [InlineData("Process[Name^=\"chr\"]")]
    [InlineData("*[Name$=\"vim\"]")]
    public void Css_selector_matches_same_logical_nodes_across_adapters(string selector)
    {
        var parsed = Css.Parse(selector);
        MatchesViaPSObject(parsed)
            .Should().BeEquivalentTo(MatchesViaJsonNode(parsed),
                "CSS selector '{0}' must agree across PSObject and JsonNode adapters", selector);
    }

    private static List<int> MatchesViaPSObject(ISelector selector)
    {
        var adapter = new PsObjectTreeAdapter(parent =>
        {
            if (parent.BaseObject is IEnumerable<Row> rows)
            {
                return rows.Select(r =>
                {
                    var ps = global::System.Management.Automation.PSObject.AsPSObject(r);
                    ps.TypeNames.Insert(0, "System.Diagnostics.Process");
                    return ps;
                });
            }

            return Array.Empty<global::System.Management.Automation.PSObject>();
        });

        var root = adapter.Wrap(global::System.Management.Automation.PSObject.AsPSObject(Sample.AsEnumerable()));
        return ExtractIds(selector.Find(root));
    }

    private static List<int> MatchesViaJsonNode(ISelector selector)
    {
        var array = new JsonArray(
            Sample.Select(r => (JsonNode)new JsonObject
            {
                ["$type"] = "Process",
                ["Id"] = r.Id,
                ["Name"] = r.Name,
            }).ToArray());
        var root = new JsonTreeAdapter().Wrap(array);
        return ExtractIds(selector.Find(root));
    }

    private static List<int> ExtractIds(IEnumerable<Match> matches)
    {
        var ids = new List<int>();
        foreach (var m in matches)
        {
            m.Node.TryGetAttribute("Id", out var v);
            ids.Add(v switch
            {
                int i => i,
                long l => (int)l,
                _ => throw new InvalidOperationException($"Unexpected Id type: {v?.GetType()}"),
            });
        }

        ids.Sort();
        return ids;
    }
}
