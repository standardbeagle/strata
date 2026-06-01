# StandardBeagle.Strata.Core

The cascade engine for [Strata](https://github.com/standardbeagle/strata): registries,
primitives, specificity, and inheritance. Resolves a tree + stylesheet into a per-node
computed value space. **No third-party dependencies.**

```bash
dotnet add package StandardBeagle.Strata.Core --prerelease
```

```csharp
using Strata.Core;

var registry = new PropertyRegistry();          // register IPropertyDescriptors
var cascade  = new Cascade(registry);
ICascadeResult result = cascade.Compute(root, stylesheet);

// Resolve a property — walks inheritance, falls back to the descriptor's initial value.
var value  = result.GetComputed<TColor>(node, "color");
var origin = result.GetOrigin(node, "color");   // Declared / Inherited / Initial
```

Winning declarations are chosen by importance, then specificity, then source order.
Bring a selector language (`StandardBeagle.Strata.Css`) and a property set
(`StandardBeagle.Strata.Properties.Styling`) to feed it.
See the [docs](https://github.com/standardbeagle/strata/tree/main/docs).
