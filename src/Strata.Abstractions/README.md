# StandardBeagle.Strata.Abstractions

The interface layer of [Strata](https://github.com/standardbeagle/strata) — a selector-driven
projection engine that resolves a tree into a value space with CSS-style cascade. This package
is interfaces only, with **no third-party dependencies**.

```bash
dotnet add package StandardBeagle.Strata.Abstractions --prerelease
```

It defines the contracts every other Strata package builds on:

- `ITreeNode` / `ITreeAdapter<TSource>` — wrap any source object (PSObject, JsonNode, an AST) as an addressable tree.
- `ISelector` / `ISelectorLanguage` — match nodes; `Specificity` for cascade ordering.
- `IRule` / `IStylesheet`, `IPropertyDescriptor` / `IPropertyValue` — declarations and the property system.
- `ICascade` / `ICascadeResult` and `IProjection<TOutput>` — compute and render computed values.

```csharp
// Expose your own type as a Strata tree by implementing ITreeNode:
public sealed class MyNode : ITreeNode
{
    public string Kind => "MyType";
    public IEnumerable<ITreeNode> Children => /* ... */;
    // Id, Classes, PseudoStates, Parent, TryGetAttribute, Underlying ...
}
```

Reference the contracts here; pull in `StandardBeagle.Strata.Core` for the cascade engine.
See the [docs](https://github.com/standardbeagle/strata/tree/main/docs) for the full specification.
