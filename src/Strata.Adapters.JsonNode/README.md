# StandardBeagle.Strata.Adapters.JsonNode

Wraps `System.Text.Json.Nodes.JsonNode` as a [Strata](https://github.com/standardbeagle/strata)
tree, so cascade and projections operate over JSON. **No third-party dependencies.**

```bash
dotnet add package StandardBeagle.Strata.Adapters.JsonNode --prerelease
```

```csharp
using System.Text.Json.Nodes;
using Strata.Adapters.JsonNode;

JsonNode json = JsonNode.Parse(jsonText)!;
ITreeNode root = new JsonTreeAdapter().Wrap(json);
// hand `root` to Cascade.Compute and a projection
```

Object keys, array elements, and values map onto `ITreeNode` so CSS-style selectors
(or, later, JSONPath) can match them.
See the [docs](https://github.com/standardbeagle/strata/tree/main/docs).
