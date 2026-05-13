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

Pre-alpha. Phase -1 scaffold. See [`docs/04-plan.md`](docs/04-plan.md) for roadmap.

## Documentation

- [Requirements](docs/01-requirements.md)
- [Specification](docs/02-spec.md)
- [Technical design](docs/03-tech-design.md)
- [Plan](docs/04-plan.md)

## Target framework

.NET 10.0+ (Native AOT compatible).

## License

MIT
