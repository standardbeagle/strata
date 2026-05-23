# Strata — Plan

**Status:** Draft v0.1
**Scope:** Phased delivery, milestones, decision checkpoints

## Approach

Each phase ships a runnable deliverable. No phase is more than ~2 weeks of focused work, so there's always a path back to working software within a sprint. Phases are ordered so that the **riskiest assumptions are tested first** — specifically, the shape of `ITreeNode` and `ISelector` are validated in Phase 0 before any layer depends on them being right.

Three principles for the rollout:

1. **Two callers from day one.** Anything that could be tree-specific has to work against PSObject and JsonNode before it lands in core.
2. **Ship something dogfoodable by Phase 3.** Spectre-based inline rendering is real product value and surfaces real property-surface bugs.
3. **Keep abstractions earnable.** Don't add `IPropertyDescriptor` complexity, custom pseudo-classes, or behavior lifecycle until a real demo demands them.

## Cross-cutting setup (Phase -1, ~half day)

Before Phase 0 starts:

- Repo created: `github.com/standardbeagle/strata`
- License chosen (MIT recommended for broadest adoption)
- `Directory.Packages.props` set up with central package management
- Single solution `Strata.sln`
- CI: GitHub Actions with `dotnet test`, `dotnet pack`, AOT smoke build
- Public NuGet feed published as a placeholder (`Strata.Abstractions` 0.1.0-alpha)
- README points to docs/

This is bookkeeping but it removes friction from the first real commit.

## Phase 0 — Abstractions and two-adapter smoke test (~1 week)

### Goal

Prove `ITreeNode` and `ISelector` are shaped right by forcing two adapters before fleshing out either one.

### Deliverables

- `Strata.Abstractions` with `ITreeNode`, `ISelector`, `Specificity`, `Match`, `MatchContext` (no implementation behind them)
- `Strata.Core` with `Cascade`, `RuleIndex` skeletons
- `Strata.Adapters.PSObject` — minimal wrapping of PSObject + properties
- `Strata.Adapters.JsonNode` — minimal wrapping of `System.Text.Json.Nodes.JsonNode`
- One hand-written `ISelector` implementation: matches `Kind == X` with an `[attr op value]` predicate
- A test that runs the same selector against both adapters and produces equivalent matches

### Success criterion

The hand-written selector contains zero PSObject or JsonNode references. If we had to add anything tree-specific to make matching work, the abstraction is wrong and we fix it now.

### Risks

- `Children` as `IEnumerable<ITreeNode>` may be too lazy for incremental updates — might need `IReadOnlyList<ITreeNode>` or a separate enumeration API.
- `TryGetAttribute` returning `object?` is unsatisfying for typed-source predicates — might need a typed overload.

### Decision checkpoint

Before Phase 1: lock down `ITreeNode` shape. Changes after this point require a deliberate revision.

## Phase 1 — CSS selector language (~1–2 weeks)

### Goal

Replace the hand-coded selector with a parsed CSS one. End of phase: any reasonable CSS subset selector parses and matches correctly.

### Deliverables

- `Strata.Css` package
- ExCSS integration for tokenization and AST
- `CssSelectorLanguage : ISelectorLanguage`
- `CssSelector : ISelector` covering: type, id, class, attribute (all 5 ops), descendant, child, adjacent sibling, general sibling, comma list, `:not`, `:is`, `:where`, `:has`, `:nth-child`, `:first-child` / `:last-child` / `:only-child`, `:empty`, `:root`, `:focused` / `:selected` / `:hovered`
- Typed predicate compilation via `System.Linq.Dynamic.Core` for `[expr]`
- Specificity computation per spec
- Conformance test suite with 50+ fixture cases

### Success criterion

A realistic stylesheet (say, the procs.css we'll write for ps-bash) parses cleanly and matches against expected PSObject and JsonNode trees. Fuzz tests with malformed selectors don't crash.

### Risks

- **Dynamic.Core AOT.** If `Expression.Compile` doesn't work cleanly under NAOT on .NET 9, build a small hand-written predicate parser as fallback (subset: `op` ∈ {`==`, `!=`, `<`, `<=`, `>`, `>=`}, identifiers, literals, `and`/`or`, `Name.StartsWith(...)` and friends). Allocate a week of contingency.
- **`:has` complexity.** `:has` requires looking at descendants, which interacts poorly with the right-to-left optimization. Document and implement naively; revisit if it becomes a perf hotspot.

### Decision checkpoint

Verify AOT story for Dynamic.Core by Phase 1 end. If broken, switch to fallback predicate parser before Phase 2.

## Phase 2 — Cascade + property system (~1 week)

### Goal

Multiple matching rules produce a single resolved value per property per node.

### Deliverables

- `Cascade.Compute` working against the full CSS subset
- Specificity-based resolution with `!important`
- Stable source-order tie-breaking
- Inheritance for inheritable properties
- `IPropertyRegistry` with a handful of built-in descriptors (color, length, enum, ident-list, string)
- Hot reload: cascade re-runs on stylesheet `Version` change

### Success criterion

`Get-Process | Wrap-In-Strata-Adapter | Cascade` produces correct computed values for every process row given a non-trivial stylesheet. Diagnostic dump (`IStrataInspector.Dump`) reads cleanly.

### Risks

- Inheritable property lookup is recursive — convert to a loop early to avoid stack issues on deep trees.

## Phase 3 — Spectre.Console projection — first shippable internal release (~1–2 weeks)

### Goal

A working `Format-Styled` ps-bash command. Dogfood it for two weeks before continuing.

### Deliverables

- `Strata.Properties.Styling` with: `color`, `background`, `font-weight`, `font-style`, `text-decoration`, `wrap`, `overflow`, `padding`, `margin`
- `Strata.Render.Spectre` with `SpectreProjection : IProjection<IRenderable>`
- ps-bash `Format-Styled` cmdlet
- Sample stylesheet for `Get-Process` output
- A small demo gallery: process list, file listing, git log

### Success criterion

`Get-Process | Format-Styled procs.css` renders correctly. Andy uses it for two weeks for real work. Bugs and property-surface gaps get filed.

### Risks

- **Property surface gaps.** Almost certainly the styling property set is wrong on first cut. This is exactly why this phase ships and we dogfood — the next phase should be informed by real usage, not speculation.
- **Color downgrade edge cases.** Spectre handles these but the mapping from our property values to `Spectre.Console.Color` may surface bugs.

### Decision checkpoint

After two weeks of usage, sit down and write up:
- Properties that were missing
- Properties that were over-specified
- Anything ergonomically painful about the stylesheet authoring

Adjust Phase 4 and 5 scope accordingly. **This is the most important checkpoint in the plan.**

## Phase 4 — Yoga layout (~1–2 weeks)

### Goal

Real `display: flex` and `display: grid` against terminal cells.

### Deliverables

- `Strata.Layout.Yoga` package with Yoga.Net (chenrensong) integration
- Mapping from styling properties to Yoga node properties
- `LayoutPass` that builds a parallel Yoga tree and computes rects
- `SpectreProjection` updated to honor layout rects: emits `Canvas` regions for absolutely-positioned content, `Grid` for grid display
- A "dashboard" demo: a stylesheet that lays out `Get-Process` as a multi-column grid

### Success criterion

Resize the terminal, the dashboard reflows correctly. Grid lines align. Absolute positioning works for floating annotations.

### Risks

- **Yoga grid support is recent (3.2+).** Verify Yoga.Net's port covers grid completely. If not, fall back to flex-only for v1.0 and document grid as v1.1.
- **Cell rounding.** Yoga returns float rects. We need consistent integer rounding to avoid sub-cell drift.

## Phase 5 — Interactions (~1.5 weeks)

**Combined with the original Phase 6 (Commands + input) per `docs/05-interaction-redesign.md`.** Phase 5 now ships the `command:` property, `IInputSource`, `ICommandRegistry`, the subscription-diff dispatcher, and the sample handlers. The original Phase 6 section below is retained only as background. Skip to Phase 7 once Phase 5 lands.

## Phase 5.legacy — Behaviors (deprecated, ~1–2 weeks)

### Goal

Stylesheet `behavior: kill, meter;` attaches and detaches behaviors as the cascade changes.

### Deliverables

- `Strata.Behaviors` package
- `IBehavior`, `IBehaviorContext`, `BehaviorHost`
- DI integration via `Microsoft.Extensions.DependencyInjection` keyed services
- `System.Reactive` event bus per `BehaviorContext`
- `behavior` property type (ident list) with **additive** cascade semantics (documented as a deviation from CSS override)
- Sample behaviors:
  - `ResourceMeter` — draws a sparkline
  - `KillProcessConfirm` — adds a context-menu kill command
  - `Highlight` — toggles a class when focused

### Success criterion

Stylesheet rule `Process[CPU>50] { behavior: meter; }` produces a sparkline next to high-CPU processes. Removing the rule (e.g., the process drops below 50%) cleanly detaches.

### Risks

- **Behavior identity across re-cascade.** A behavior attached to `Process#1234` should not be re-attached if the cascade re-runs and `meter` is still in the resolved value. The `(node, name)` key has to be stable across cascade runs — i.e., `ITreeNode` equality has to be solid. Reinforces Phase 0 design.
- **Lifecycle ordering.** Detach must complete before Attach for cleanup correctness. Document and test.

## Phase 6 — Commands and input (~1 week)

### Goal

Keystrokes invoke behavior-published commands on focused/selected nodes.

### Deliverables

- `ICommandRegistry`, `ICommand`, `KeyBinding`
- Focus model: `:focused` pseudo-state, j/k or arrow navigation
- Selection model: multi-node, `:selected` pseudo-state
- Input dispatcher that routes keystrokes to focused-node behavior contexts
- `Format-Styled -Interactive` switch in ps-bash that enables nav + command invocation

### Success criterion

`Get-Process | Format-Styled procs.css -Interactive`: j/k moves between rows, 'k' invokes the kill command published by the `kill` behavior. Status line shows available commands for the focused row.

### Risks

- **Conflicting key bindings across behaviors.** Two behaviors attached to the same node both bind 'd'. Resolution: error at attach time and log; the second to attach loses.
- **In-line vs full-screen input.** `Format-Styled -Interactive` is in-line but needs raw-mode input. Terminal.Gui v2's in-line mode is the prior art here — borrow or adopt their input layer.

## Phase 7 — Terminal.Gui projection (~2 weeks)

### Goal

A full-screen interactive command (`Show-Processes`) driven entirely by stylesheet, sharing the engine with `Format-Styled`.

### Deliverables

- `Strata.Render.TerminalGui` package
- `TerminalGuiProjection : IProjection<View>` with reconciliation (React-style diff)
- Sample command: `Show-Processes` — a full-screen process explorer
- Documentation on stateful-projection authoring

### Success criterion

The same stylesheet (possibly with a context-distinguishing pseudo-class like `:fullscreen`) drives both `Format-Styled` and `Show-Processes`. Focus and selection state survive across cascade runs.

### Risks

- **Reconciliation complexity.** This is the highest-risk phase technically. The fallback is tear-down-and-recreate on each cascade, accepting lost focus — log this as an explicit option if reconciliation gets out of hand.
- **Terminal.Gui v2 stability.** v2 was still in develop builds as of recent NuGet versions. Pin to a tested build and document.

### Decision checkpoint

If reconciliation isn't working cleanly by mid-phase, switch to tear-down-recreate with focus-restoration heuristic.

## Phase 8 — Second tree adapter (Bifrost) (~1–2 weeks)

### Goal

Validate the abstraction by porting Bifrost's CSS-style rule engine to Strata.

### Deliverables

- `Strata.Adapters.Schema` (or similar) wrapping Bifrost's schema AST
- Bifrost's existing rules ported to Strata stylesheets
- A `GraphqlResolverProjection` that emits resolver descriptors
- Bifrost test suite passing against the new engine
- Public blog post / writeup on the pattern (this is a marketing moment — Bifrost-on-Strata is a strong story)

### Success criterion

Bifrost's existing tests pass with Strata as its rule engine. No regressions. Bonus: rules are noticeably more expressive thanks to `:has`, `:not`, and predicates.

### Risks

- **Bifrost rule semantics may not be 1:1 with CSS cascade.** Identify deviations early and either extend Strata (preferred) or transform rules on import (fallback).
- **Backwards compatibility for Bifrost users.** Old rule format may need a compatibility shim.

## Phase 9 — JSONPath selector language (~1 week)

### Goal

Pluggable selector languages prove out: same engine, different selector syntax.

### Deliverables

- `Strata.JsonPath` package using JsonPath.Net (RFC 9535) for parsing
- `JsonPathSelector : ISelector` over `ITreeNode`
- Capture extraction from `*`, slices, filters
- Sample: a small "router projection" that maps state-tree slices to handler descriptors

### Success criterion

`$.users[?(@.role == 'admin')]` matches the same logical nodes as `.user[role="admin"]` does in CSS. Captures populate.

### Risks

- **Specificity for JSONPath is arbitrary.** Document the chosen mapping and accept that users mixing CSS and JSONPath in one stylesheet will hit edge cases.

## Optional Phase 10 — JS port for reducer router (~3–4 weeks, separate track)

A standalone TypeScript port of `Strata.Abstractions`, `Strata.Core`, `Strata.JsonPath`, plus a React-flavored router projection. Separate repo, separate release cadence. Driven by demand, not on the critical path for v1.0.

## v1.0 release criteria

By end of Phase 7, plus either Phase 8 (Bifrost) or Phase 9 (JSONPath):

- All Phase 0–7 deliverables shipping
- Public NuGet packages versioned 1.0.0
- Docs site with quickstart, selector reference, three end-to-end tutorials
- AOT verification passing
- Benchmark targets met (per Tech Design §10.4)
- At least one external consumer beyond ps-bash and Bifrost

## Aggregate timeline

| Phase | Calendar weeks | Cumulative |
|---|---:|---:|
| -1 | 0.1 | 0.1 |
| 0 | 1 | 1.1 |
| 1 | 1.5 | 2.6 |
| 2 | 1 | 3.6 |
| 3 | 1.5 (+ 2 weeks dogfood) | 7.1 |
| 4 | 1.5 | 8.6 |
| 5 | 1.5 | 10.1 |
| 6 | 1 | 11.1 |
| 7 | 2 | 13.1 |
| 8 *or* 9 | 1.5 | 14.6 |
| 1.0 release polish | 1 | **15.6** |

About **16 calendar weeks at a sane pace** to v1.0, with explicit slack via the Phase 3 dogfood window where most of the property-surface refinement happens organically rather than under deadline pressure.

A faster pace is feasible — Phases 1, 2, 5, 6 could each compress by a few days if implementation is clean. A slower pace is more likely if Dynamic.Core AOT or Terminal.Gui reconciliation hit unexpected issues.

## Decision log (to be maintained going forward)

This section is to be appended to as decisions are locked down:

- *(Phase -1)* License: TBD (MIT recommended)
- *(Phase -1)* Target framework: .NET 9.0+
- *(Phase -1)* Repository: standardbeagle/strata, separate from standardbeagle-tools
- *(Phase 0)* `ITreeNode.Children` type: TBD (`IEnumerable` vs `IReadOnlyList`)
- *(Phase 1)* Dynamic.Core AOT status: TBD; fallback parser ready
- *(Phase 5)* Interaction model: selector-bound `IObservable<HostEvent>` subscriptions, **not** `IBehavior.Attach/Detach`. `command:` property carries `(command-name, event-name)` pairs; additive cascade semantics (same deviation from CSS as the original `behavior:` design, but narrower contract). Replaces FR-12 + §6 of spec/tech-design. See `docs/05-interaction-redesign.md`.
- *(Phase 7)* Reconciliation strategy: **full diff — shipped.** The tear-down-recreate fallback was authorised but not needed; Terminal.Gui v2 `View` exposes mutable `Text`/`ColorScheme` and an add/remove `Subviews` collection, so in-place diff reconciliation is clean. See `docs/06-stateful-projection.md` §3.
- *(Phase 7)* Terminal.Gui pin: `2.0.0-prealpha.216` (the planned `.4` is not published on nuget.org). Transitive `System.Text.Json` pinned to `8.0.5` for advisory GHSA-8g4q-xg66-9fp4.
- *(Phase 7)* AOT exception: `Strata.Render.TerminalGui` opts out of AOT/trim (Terminal.Gui v2 deps are not trim-clean); the rest of the engine stays AOT-compatible.
- *(Phase 7)* Live input: `TerminalGuiInputSource : IInputSource` supplies the live terminal raw-mode input layer the Phase 6 re-scope deferred; it feeds the existing interaction dispatcher unchanged.

## What this plan does not promise

- A C++ rewrite. The whole point of Yoga.Net was AOT-clean pure C#; we don't need a C++ tier.
- A custom font rendering or glyph layer. Terminal cell math only.
- Bidirectional / RTL text support beyond what Spectre and Terminal.Gui already provide.
- A visual stylesheet editor or browser-style devtools. The `IStrataInspector` dump is what we ship; a richer UI is a downstream project.
