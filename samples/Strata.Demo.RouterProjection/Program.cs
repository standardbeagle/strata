using System.Text.Json.Nodes;
using Strata;
using Strata.Adapters.JsonNode;
using Strata.Demo.RouterProjection;

// Strata demo: a JSONPath "router projection".
//
// The same ISelector contract that drives the CSS cascade drives a routing table here.
// A reducer-style state tree is matched by JSONPath routes; each matched slice projects to a
// HandlerDescriptor (handler name + slice location + slice address). The engine describes the
// routing, it does not invoke handlers.
//
// Pipeline:  JSON state tree -> JsonTreeAdapter -> ITreeNode
//            routing table (JSONPath -> handler) -> RouterProjection
//            RouterProjection.Project -> HandlerDescriptor[]

var state = JsonNode.Parse(
    """
    {
      "users": [
        { "$type": "user", "$id": "u1", "role": "admin", "name": "Ada" },
        { "$type": "user", "$id": "u2", "role": "user",  "name": "Boris" },
        { "$type": "user", "$id": "u3", "role": "admin", "name": "Chen" }
      ],
      "notifications": [
        { "$type": "notification", "kind": "error",   "text": "disk full" },
        { "$type": "notification", "kind": "info",    "text": "synced" }
      ]
    }
    """)!;

var root = new JsonTreeAdapter().Wrap(state);

var router = new RouterProjection(new[]
{
    new Route("$.users[?@.role == 'admin']", "admin-console"),
    new Route("$.users[?@.role == 'user']", "user-home"),
    new Route("$.notifications[?@.kind == 'error']", "alert-banner"),
});

Console.WriteLine("Router projection — state slices routed to handlers:");
Console.WriteLine();

foreach (var descriptor in router.Project(root))
{
    var address = string.Join(".", descriptor.Captures);
    Console.WriteLine($"  {descriptor.Location,-22} -> {descriptor.Handler,-14} (slice: {address})");
}
