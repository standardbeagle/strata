# Strata PowerShell DSL — Walking Skeleton

**Status:** Design v1.0
**Date:** 2026-06-03
**Owner:** Andy Brummer / StandardBeagle

## Goal

A PowerShell module that lets an author declare a responsive terminal UI in a `.ps1`
file and render it through Strata — the TUI equivalent of an HTA application. The end
vision: author a live "ping/tnc card" (current-value graph + scrolling history panel)
once, save it as a reusable layout template, and import that template into other scripts
(uptime monitor, DB query view).

This document specifies **only the first sub-project: the walking skeleton.** It proves
the authoring spine — *PowerShell DSL → element tree → cascade → projection → screen* —
with the least new infrastructure. The reactive store, live loop, Terminal.Gui projection,
graph/history widgets, and template-packaging convention are deferred to later
sub-projects (see Decomposition).

## Decomposition (full vision, dependency-ordered)

The complete goal spans five sub-projects. This spec covers **#1 only**.

| # | Sub-project | Builds on | This spec |
|---|---|---|---|
| 1 | **PS DSL + element-node model** — `Stack/Text/Card{}` functions build a Strata `ITreeNode` tree, rendered once via Spectre | core/cascade | ✅ |
| 2 | **Host cmdlet (live)** — `Show-StrataApp` boots engine, owns the Terminal.Gui loop + lifecycle | 1 + `Strata.Interaction` | deferred |
| 3 | **Reactive store** — state document + `dispatch` + JSONPath data-bindings (`Graph -Bind '$.history'`) → re-cascade → reconcile | `Strata.JsonPath` (exists) + 2 | deferred |
| 4 | **Live widgets** — Graph/Sparkline, scrolling history list, Card visuals | projections | deferred |
| 5 | **Template packaging** — reusable layout functions importable across scripts | 1–4 | deferred |

### Authoring-model decisions (apply to the whole vision, settled during brainstorming)

- **DSL style: pure-PowerShell functions over a thin C# node factory.** `Stack`/`Text`/`Card`
  are advisory functions in a `.psm1`; they call a C# `StrataNode` factory. Native feel,
  scriptblock nesting, one-line cost to add a widget.
- **Update model (sub-projects 2–3): reactive store.** Author dispatches state updates; JSONPath
  bindings re-render affected widgets. The selector half already exists (`Strata.JsonPath`); the
  store + dispatch + redraw-on-change half is new in sub-project 3.

These are recorded here so later sub-project specs inherit them, but they are **out of scope for
the skeleton**, which is static render-once with no store and no live loop.

## Architecture

Two new units.

### `Strata.Dsl` (new C# project, `src/Strata.Dsl`)

- **`StrataElement : ITreeNode`** — the element/DOM node the DSL builds.
- **`StrataNode`** — static factory the PS functions call.
- **`StrataConsole`** — render facade: tree + CSS path → console output.

Depends on: `Strata.Abstractions`, `Strata.Core`, `Strata.Css`,
`Strata.Properties.Styling`, `Strata.Render.Spectre`. Target `net10.0`, AOT-compatible,
matching the rest of the repo.

### `Strata.PowerShell` (new module dir, e.g. `src/Strata.PowerShell/`)

- **`Strata.PowerShell.psd1`** — module manifest. `RequiredAssemblies` (or `.psm1`-side
  `Add-Type`/assembly load) pulls in `Strata.Dsl` and its dependency DLLs.
- **`Strata.PowerShell.psm1`** — the DSL functions (`Stack`, `Text`, `Card`, `Element`) and
  the `Show-Styled` wrapper. Exports those names.

## Components

### `StrataElement : ITreeNode`

Implements the full contract from `src/Strata.Abstractions/Tree.cs`:

- `Kind` (string, required) — e.g. `"Stack"`, `"Text"`, `"Card"`.
- `Id` (string?) — optional, CSS `#id`.
- `Classes` (`IReadOnlySet<string>`) — backed by a mutable set; CSS `.class`.
- `PseudoStates` (`IReadOnlySet<string>`) — backed by a mutable set; **empty in the skeleton**,
  present so sub-project 3 (focus/selection/live state) layers on without changing the node type.
- `Parent` (`ITreeNode?`) — set by `Add`.
- `Children` (`IEnumerable<ITreeNode>`) — ordered, backed by a `List<StrataElement>`.
- `TryGetAttribute(name, out value)` — reads a `Dictionary<string, object?>`.
- `Underlying` — `null` in the skeleton. Sub-project 3 will expose a `JsonNode` view here so
  `Strata.JsonPath` bindings evaluate against state.

**Identity:** reference identity (default `Equals`/`GetHashCode`). The contract requires that two
instances representing the same logical node compare equal; a reference-stable element built once
per render satisfies this and gives the reconciliation map (sub-project 2) what it needs.

**Mutation:** `Add(StrataElement child)` sets `child.Parent = this` and appends. Classes and
pseudo-states are mutable sets so later phases can toggle them in place.

### `StrataNode` (factory)

```csharp
public static StrataElement Create(
    string kind,
    string? id = null,
    IEnumerable<string>? classes = null,
    IDictionary<string, object?>? attributes = null);
```

Filters empty/whitespace class tokens (so `($Class -split ' ')` on an empty string yields no
classes). Returns a detached element; the caller wires children via `Add`.

### DSL functions (`.psm1`) — composition mechanism

Pure functional composition. **No ambient/global parent stack** (lock-free, matches repo
principles). A child scriptblock is executed with the call operator; nested DSL calls emit their
nodes to the pipeline; the parent collects and adds them.

```powershell
function Stack {
  param(
    [string]$Class,
    [string]$Id,
    [hashtable]$Attr,
    [Parameter(Position = 0)][scriptblock]$Children
  )
  $n = [Strata.Dsl.StrataNode]::Create('Stack', $Id, ($Class -split ' '), $Attr)
  if ($Children) { foreach ($c in & $Children) { if ($c) { $n.Add($c) } } }
  $n
}
```

- `Text` takes a positional string as its content; the function stores it as the `text`
  attribute: `Text 'Ping Monitor' -Class h1`.
- `Card` is a thin wrapper over kind `Card` with a child scriptblock (styleable container).
- **`Element -Kind <name>`** is the generic escape hatch: build any kind without a dedicated
  function. New widgets become one-line PS functions over `Element`, keeping the widget set
  open-ended.
- `$Attr` (hashtable) maps to the attribute dictionary for arbitrary `key=value` pairs a future
  stylesheet or binding may read.

Exported functions for the skeleton: `Stack`, `Text`, `Card`, `Element`, `Show-Styled`.

### `Show-Styled` + `StrataConsole.Render`

```powershell
function Show-Styled {
  param(
    [Parameter(ValueFromPipeline, Mandatory)]$Layout,
    [Parameter(Mandatory)][string]$Stylesheet
  )
  [Strata.Dsl.StrataConsole]::Render($Layout, (Resolve-Path $Stylesheet))
}
```

```csharp
public static class StrataConsole
{
    public static void Render(StrataElement root, string cssPath);
}
```

`Render` reproduces the pipeline the existing `Strata.Demo.Spectre` sample already proves:

1. `File.ReadAllText(cssPath)`.
2. `StylingProperties.CreateRegistry()`.
3. `new CssStylesheetParser(new CssSelectorLanguage(), registry).Parse(css)`.
4. `new Cascade(registry).Compute(root, stylesheet)`.
5. `new SpectreProjection { TextSelector = node => node.TryGetAttribute("text", out var t)
   ? t?.ToString() ?? "" : "" }`.
6. `AnsiConsole.Write(projection.Project(root, cascade))`.

Stateless, render-once. No screen clearing, no input, no loop.

## Data flow

```
author .ps1
  └─ DSL functions (Stack/Text/Card/Element)
        └─ StrataNode.Create + Add  ──►  StrataElement tree (ITreeNode)
                                              │
  monitor.css ──► CssStylesheetParser ──► IStylesheet
                                              │
                          Cascade.Compute(root, sheet) ──► ICascadeResult
                                              │
                       SpectreProjection.Project ──► IRenderable ──► AnsiConsole
```

## Example (the deliverable this skeleton enables)

```powershell
Import-Module Strata.PowerShell

$layout = Stack -Class 'main' {
    Text 'Ping Monitor' -Class 'h1'
    Card -Class 'host' {
        Text 'google.com  12ms  ▁▂▃▅▂▁'
    }
}
$layout | Show-Styled -Stylesheet ./monitor.css
```

Renders a styled static layout once to the console. (The sparkline here is literal text — the
real Graph widget arrives in sub-project 4.)

## Error handling — fail fast, no fallback

- Missing stylesheet path → `Resolve-Path` throws a terminating error. No default stylesheet.
- Invalid CSS → `CssStylesheetParser` throws `FormatException` (its documented contract);
  surfaces as a PS terminating error. No partial/fallback render.
- Attributes not backed by a registered styling property are ignored by the cascade — acceptable;
  the skeleton renders only what the styling property set understands plus the `text` attribute.
- `null` pipeline elements in a child scriptblock are skipped by the `if ($c)` guard.

## Testing

- **xUnit** (new `tests/Strata.Dsl.Tests` or extend existing test project):
  - `StrataElement` honours the `ITreeNode` contract — `Kind`/`Id`/`Classes`/`PseudoStates`,
    `Parent` set by `Add`, ordered `Children`, `TryGetAttribute` hit/miss, reference-identity
    `Equals`/`GetHashCode`.
  - `StrataNode.Create` — class-token filtering, attribute passthrough, detached parent.
  - `StrataConsole.Render` — for a known tree + CSS, assert expected markup/text appears in the
    Spectre output (render to a `StringWriter`-backed `AnsiConsole` or capture).
- **Pester** (`tests/` PowerShell): DSL composition —
  `Stack -Class 'a b' { Text 'x'; Text 'y' }` yields Kind `Stack`, classes `{a,b}`, two `Text`
  children whose `text` attributes are `x`/`y`; `Element -Kind Foo` builds kind `Foo`;
  `Show-Styled` end-to-end against a fixture `.css` produces non-empty output.

  ⚠️ **Risk / task:** Pester requires `pwsh` available in the test/CI run. The repo currently has
  only .NET test projects. Wiring `pwsh` + Pester into the build (or gating the Pester suite
  behind a `pwsh`-present check) is an explicit task in the implementation plan.

## Out of scope (deferred)

Reactive store, `dispatch`, JSONPath data-bindings, live refresh loop, Terminal.Gui projection,
Graph/Sparkline/scrolling-history widgets, focus/selection interaction, template-packaging
convention, inline (non-file) stylesheets. These are sub-projects 2–5.

## Success criteria

A `.ps1` that imports `Strata.PowerShell`, declares a `Stack`/`Text`/`Card` layout via the DSL,
pipes it to `Show-Styled` with a `.css` file, and renders a correctly styled static view to the
terminal — with the C# pipeline reused unchanged from the existing Spectre sample, and the node
type already mutable/pseudo-state-ready for the next sub-project.
