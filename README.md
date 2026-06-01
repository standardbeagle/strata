# Strata

Selector-driven projection from a tree to a value space, with cascade resolution when multiple selectors match the same node.

A general-purpose .NET engine that subsumes:

- CSS-style styling of arbitrary object trees
- IE5-HTC-style behavior attachment
- JSONPath-driven routing over reducer state
- Schema-rule systems
- Vim-style keymap binding to nodes by predicate

The core knows nothing about styling, rendering, or HTML. It knows about trees, selectors, matches, cascade, and projection.

## Status

Pre-alpha. Phase 3 shipped — CSS subset selectors, cascade with specificity/inheritance/hot-reload,
the built-in styling property set, and a Spectre.Console projection, AOT-verified end-to-end.
Layout (Yoga), interactions, JSONPath, and the Terminal.Gui projection are still ahead. See
[`docs/04-plan.md`](docs/04-plan.md) for the roadmap.

## Install

Packages publish to NuGet under the `StandardBeagle.Strata.*` prefix as prereleases:

```bash
dotnet add package StandardBeagle.Strata.Core --prerelease
dotnet add package StandardBeagle.Strata.Css --prerelease
dotnet add package StandardBeagle.Strata.Properties.Styling --prerelease
dotnet add package StandardBeagle.Strata.Render.Spectre --prerelease
# adapters
dotnet add package StandardBeagle.Strata.Adapters.JsonNode --prerelease
dotnet add package StandardBeagle.Strata.Adapters.PSObject --prerelease
```

(The C# namespaces remain `Strata.*`; only the package IDs carry the `StandardBeagle.` prefix.)

## Documentation

- [Requirements](docs/01-requirements.md)
- [Specification](docs/02-spec.md)
- [Technical design](docs/03-tech-design.md)
- [Plan](docs/04-plan.md)
- [Interaction redesign](docs/05-interaction-redesign.md) — supersedes the Phase 5/6 behavior model

## Target framework

.NET 10.0+ (Native AOT compatible).

## License

MIT
