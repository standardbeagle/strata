namespace Strata.Phase0.Tests;

using System.Management.Automation;
using System.Text.Json.Nodes;
using Strata.Adapters.JsonNode;
using Strata.Adapters.PSObject;
using Strata.Core.Tests.TestFixtures;

/// <summary>
/// Phase 0 success criterion: the same selector matches an equivalent set of nodes against
/// both the PSObject adapter and the JsonNode adapter. If this test ever requires
/// tree-implementation-specific branches in the selector, the abstraction is wrong.
/// </summary>
public sealed class AdapterEquivalenceTests
{
    private sealed record ProcessRow(int Id, string Name, double Cpu);

    private static readonly ProcessRow[] Sample =
    [
        new(1, "init", 0.1),
        new(1234, "chrome", 64.0),
        new(5678, "vim", 1.2),
    ];

    [Fact]
    public void Kind_selector_matches_same_logical_nodes_in_both_adapters()
    {
        // CSS-like selector: match all nodes with Kind == "Process".
        var selector = new KindSelector("Process", new Specificity(0, 0, 1));

        var fromPs = MatchesViaPSObject(selector);
        var fromJson = MatchesViaJsonNode(selector);

        fromPs.Should().BeEquivalentTo(fromJson,
            "matching the same logical set against PSObject vs JsonNode trees must agree");
        fromPs.Should().HaveCount(Sample.Length);
    }

    [Fact]
    public void Attribute_predicate_matches_same_logical_nodes_in_both_adapters()
    {
        // Match Process[Name == "chrome"] via the stand-in selector.
        var selector = new KindSelector(
            "Process",
            new Specificity(0, 1, 1),
            attribute: ("Name", "chrome"));

        var fromPs = MatchesViaPSObject(selector);
        var fromJson = MatchesViaJsonNode(selector);

        fromPs.Should().BeEquivalentTo(fromJson);
        fromPs.Should().HaveCount(1);
        fromPs.Single().Should().Be((1234, "chrome"));
    }

    [Fact]
    public void Both_adapters_cache_wrappers_for_identity()
    {
        var psObj = global::System.Management.Automation.PSObject.AsPSObject(Sample[0]);
        var psAdapter = new PsObjectTreeAdapter();
        psAdapter.Wrap(psObj).Should().BeSameAs(psAdapter.Wrap(psObj));

        var json = JsonNode.Parse("{\"x\":1}")!;
        var jsonAdapter = new JsonTreeAdapter();
        jsonAdapter.Wrap(json).Should().BeSameAs(jsonAdapter.Wrap(json));
    }

    [Fact]
    public void PsObject_kind_strips_namespace()
    {
        var psObj = global::System.Management.Automation.PSObject.AsPSObject(Sample[0]);
        // The CLR type name is "ProcessRow" (nested record), so we relabel it to match the
        // "Process" Kind the selector expects. PSObject.TypeNames is mutable.
        psObj.TypeNames.Insert(0, "System.Diagnostics.Process");
        var node = new PsObjectTreeAdapter().Wrap(psObj);
        node.Kind.Should().Be("Process");
    }

    [Fact]
    public void JsonNode_kind_uses_dollar_type_when_present()
    {
        var json = JsonNode.Parse("""{ "$type": "Process", "Id": 1, "Name": "x" }""")!;
        var node = new JsonTreeAdapter().Wrap(json);
        node.Kind.Should().Be("Process");
    }

    private static List<(int Id, string Name)> MatchesViaPSObject(ISelector selector)
    {
        var adapter = new PsObjectTreeAdapter(parent =>
        {
            // Root contains a list of rows; children are the rows.
            if (parent.BaseObject is IEnumerable<ProcessRow> rows)
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
        return ExtractMatches(selector.Find(root));
    }

    private static List<(int Id, string Name)> MatchesViaJsonNode(ISelector selector)
    {
        var jsonArray = new JsonArray(
            Sample.Select(r => (JsonNode)new JsonObject
            {
                ["$type"] = "Process",
                ["Id"] = r.Id,
                ["Name"] = r.Name,
                ["Cpu"] = r.Cpu,
            }).ToArray());
        var adapter = new JsonTreeAdapter();
        var root = adapter.Wrap(jsonArray);
        return ExtractMatches(selector.Find(root));
    }

    private static List<(int Id, string Name)> ExtractMatches(IEnumerable<Match> matches)
    {
        var list = new List<(int, string)>();
        foreach (var m in matches)
        {
            m.Node.TryGetAttribute("Id", out var idVal);
            m.Node.TryGetAttribute("Name", out var nameVal);

            // JsonValue.TryGetValue<long> path stores ints as long; PSObject keeps int.
            var id = idVal switch
            {
                int i => i,
                long l => (int)l,
                _ => throw new InvalidOperationException($"Unexpected Id type: {idVal?.GetType()}"),
            };
            list.Add((id, nameVal?.ToString() ?? string.Empty));
        }

        list.Sort();
        return list;
    }
}
