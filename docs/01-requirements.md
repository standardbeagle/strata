# Strata — Requirements

**Status:** Draft v0.1
**Owner:** Andy Brummer / StandardBeagle

## Premise

Strata is a .NET library for **selector-driven projection from a tree to a value space, with cascade resolution when multiple selectors match the same node.**

This abstraction subsumes several patterns that are normally implemented from scratch:

- CSS-style styling of arbitrary object trees
- IE5-HTC-style behavior attachment
- JSONPath-driven routing over reducer state
- Schema-rule systems like Bifrost's CSS-to-GraphQL mapping
- Vim-style keymap binding to nodes by predicate

The core engine knows nothing about styling, rendering, or HTML. It knows about trees, selectors, matches, cascade, and projection. Styling is one projection among many.

## Use cases

### UC-1 — Inline pipeline formatting (ps-bash)

A ps-bash user pipes objects through `Format-Styled` with a CSS-style stylesheet.

- Tree: PSObject result tree
- Selectors: CSS subset
- Projection: Spectre.Console `IRenderable`
- Output: inline styled console output, no full-screen mode

### UC-2 — Full-screen interactive widget (ps-bash)

A ps-bash user invokes an interactive command (e.g. `Show-Processes`) that brings up a full-screen view driven by a stylesheet.

- Tree: PSObject tree, plus selection/focus state as pseudo-states
- Selectors: CSS subset
- Projection: Terminal.Gui v2 `View` hierarchy
- Behaviors: attached via stylesheet, publishing commands bound to keystrokes
- Output: full-screen TUI app from a stylesheet

### UC-3 — Bifrost schema rules

Bifrost authors GraphQL endpoints via CSS-style rules over a schema AST. Strata replaces Bifrost's existing rule engine.

- Tree: Schema AST nodes
- Selectors: CSS subset
- Projection: GraphQL resolver descriptors
- Output: configured GraphQL schema

### UC-4 — Reducer state router (future, possibly JS port)

A web app routes UI components by matching against the Redux/reducer state tree.

- Tree: Redux state tree
- Selectors: JSONPath
- Projection: React element + props, populated from selector captures
- Output: rendered route component

### UC-5 — Behavior-driven command bindings (Phase 5+)

A user defines vim-style keymaps in a stylesheet — selectors pick out nodes that should respond to keystrokes, behaviors publish the commands.

- Tree: any
- Selectors: CSS subset with `:focused` / `:selected` pseudo-states
- Projection: command registry
- Output: keystroke → command invocation on matching nodes

## Functional requirements

### FR-1 — Tree adapter abstraction

The engine MUST operate over any tree shape via an `ITreeNode` interface. Concrete trees (PSObject, JsonNode, schema AST, Redux state) are wrapped by per-source adapters. Core code MUST NOT reference any specific tree implementation.

### FR-2 — Pluggable selector languages

The engine MUST support multiple selector languages via `ISelectorLanguage`. Initial implementations: CSS subset, JSONPath. Adding a new language MUST NOT require changes to core.

### FR-3 — Selector matching

For any tree node, the engine MUST determine all rules whose selectors match that node. Matching MUST be deterministic, MUST NOT depend on iteration order of containers, and MUST produce structured match results that include any captured values.

### FR-4 — Cascade resolution

When multiple rules match the same node and declare the same property, the engine MUST resolve a single winner using:

1. `!important` declarations win over non-important
2. Higher specificity wins
3. Later source order wins (stable tie-breaker)

The CSS specificity model `(A, B, C)` for `(ids, classes+attrs+pseudo, types)` MUST be used for CSS selectors. JSONPath selectors MUST have a documented specificity mapping.

### FR-5 — Property system

Properties MUST be defined as typed descriptors with: name, value type, parser, initial value, inheritance flag. New properties MUST be registrable without changing core. Property values MUST be reference-cheap (struct or interned) for AOT.

### FR-6 — Inheritance

Properties marked inheritable MUST cascade to descendants when no local declaration is present. Inheritance walks the tree, not the rule order.

### FR-7 — Pluggable projections

The engine MUST allow callers to define projections `IProjection<TOutput>` that consume the cascade output and produce arbitrary results. Projections MUST NOT be hard-coded to a specific output type in core.

### FR-8 — Incremental tree updates

When a tree node mutates (added, removed, attribute changed), the engine MUST support recomputing only the affected subtree, not the whole tree.

### FR-9 — Incremental stylesheet updates

When a stylesheet changes (hot reload), the engine MUST recompute affected nodes efficiently. Phase 2 may do full recompute; Phase 5+ MUST be incremental.

### FR-10 — Captures

Selector languages with wildcards or predicates (JSONPath especially) MUST surface captured values to the projection. Captures are key-value pairs available in the `Match` record.

### FR-11 — Pseudo-state model

Pseudo-states like `:focused`, `:selected`, `:hovered`, `:expanded` MUST be representable per-node and toggleable at runtime. Toggling a pseudo-state MUST trigger only re-cascade of selectors that reference it.

### FR-12 — Behavior attachment (Phase 5+)

A `behavior` property MUST be supported that names one or more behaviors to attach to matching nodes. Behaviors have a documented `Attach`/`Detach` lifecycle. When the cascade changes the set of behaviors attached to a node, the engine MUST issue the appropriate Attach/Detach calls.

### FR-13 — Diagnostics

The engine MUST expose, for any node:

- All matched rules
- Which declaration won for each property and why
- Inheritance chains for inheritable properties

This is required for stylesheet authoring tooling.

## Non-functional requirements

### NFR-1 — Native AOT compatibility

Strata.Abstractions, Strata.Core, Strata.Css, and Strata.JsonPath MUST be Native AOT compatible end to end. No `System.Reflection.Emit`, no runtime code generation, no dynamic proxies. Source generators are acceptable.

Justification: ps-bash.exe ships as a Native AOT binary.

### NFR-2 — No native dependencies in core

Strata.Core, Strata.Css, Strata.JsonPath, Strata.Layout.Yoga MUST have no P/Invoke or native library dependencies. Pure managed code only.

Justification: deployment simplicity, AOT trimming, cross-platform consistency.

### NFR-3 — Allocation budget

A re-cascade of a 1000-node tree against a 100-rule stylesheet MUST allocate less than 1 MB on the managed heap in steady state (i.e., excluding initial parse and JIT warmup). Hot paths MUST use pooled buffers where feasible.

### NFR-4 — Target framework

Strata targets **.NET 10.0+**. No .NET Standard 2.0 fallback. No .NET Framework support. .NET 10 chosen for improved Native AOT codegen, smaller AOT binaries, and dynamic-PGO-friendly tiered compilation.

### NFR-5 — Public API surface

Strata.Abstractions MUST keep its public surface small and stable. Breaking changes to Abstractions require a major version bump and a migration note. Implementation packages may evolve faster.

### NFR-6 — Threading

The engine is single-threaded by design. Cascade results are immutable and safe to share across threads. Mutation, re-cascade, and projection MUST happen on a single thread per cascade instance.

### NFR-7 — Trim-safe

All packages MUST build with trimming enabled and produce no trimmer warnings in published binaries.

### NFR-8 — Documentation

Every public type MUST have XML documentation. The CSS selector grammar and the JSONPath subset MUST be documented with a complete, testable grammar. A "why was this rule applied" diagnostic MUST be available in a sample app from Phase 3 onward.

## Out of scope

- HTML rendering
- Browser DOM compatibility
- W3C CSS conformance beyond the documented subset
- CSS animation, transition, transform
- CSS variables / custom properties (deferred; reconsider after Phase 6)
- Container queries (deferred)
- Cascade layers `@layer` (deferred)
- Media queries (replaced by a `MediaContext` object available to selectors)
- Specific rendering — Strata renders nothing, projections do
- Specific tree types — Strata core has zero PSObject/JsonNode/schema-specific code

## Constraints

- **License:** MIT or Apache 2.0 (decide before public release)
- **Repository:** Separate repo from `standardbeagle-tools` since `standardbeagle-tools` is a pnpm/TS monorepo. Suggested: `standardbeagle/strata`
- **Languages:** C# only in the .NET implementation. A separate TS port may follow for UC-4.
- **Public consumption:** Bifrost and ps-bash are first-class consumers; design choices are vetted against both.

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| `ITreeNode` shape proves wrong after committing | Phase 0 requires two adapters before any single tree is fleshed out |
| Property value types proliferate and core becomes complex | Properties registered externally; core has no built-in property types |
| Cascade performance is dominated by selector matching | Right-to-left subject-first matching + per-kind rule index |
| AOT incompatibility discovered late | Dedicated AOT verification project from Phase 0 |
| Terminal.Gui projection stateful diff is harder than expected | Treat Phase 7 as exploratory; ship Phase 3 (Spectre) regardless |
| Bifrost consolidation breaks Bifrost users | Phase 8 ships Bifrost adapter as additive; old engine deprecates over a release |

## Success definition for v1.0

A v1.0 release means:

- `Get-Process | Format-Styled -StyleSheet procs.css` renders styled output in ps-bash
- `Show-Processes -StyleSheet procs.css` runs a full-screen interactive process explorer driven by stylesheet
- Bifrost compiles its schema using Strata internally
- A documented public API with semver guarantees
- Published NuGet packages
- Reasonable docs site with a quickstart and a selector reference
- A demo gallery with at least three non-styling projections to demonstrate the abstraction
