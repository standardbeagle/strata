# Strata PowerShell — Reactive Live Dashboards (sub-projects 2–5)

**Status:** Design v1.0
**Date:** 2026-06-03
**Owner:** Andy Brummer / StandardBeagle
**Builds on:** `2026-06-03-strata-powershell-dsl-skeleton-design.md` (sub-project 1, shipped)

## Goal

Complete the PS-authored TUI vision: a live, reactive dashboard authored in a `.ps1`. The hero
scenario — a ping/tnc card with a current-value graph and a scrolling history, reused as a
template across uptime and DB-query scripts — runs end to end.

This spec covers the remaining four sub-projects from the decomposition, built as one coherent
reactive layer on the skeleton's spine.

## Projection decision: Spectre live-redraw, not Terminal.Gui

The decomposition named a Terminal.Gui live loop for sub-project 2. Engineering reassessment: the
named use cases (ping, tnc, uptime, DB monitors) are **output dashboards**, not keyboard-driven
apps. A **Spectre live-redraw loop driven by the reactive store** delivers them with far less risk
than editing the Terminal.Gui projection to add custom Graph/scroll views.

Crucially, the store + binding + widget layer is **projection-agnostic**: it mutates element
attributes and re-cascades. Wiring the same elements into the existing Terminal.Gui reconciling
projection (for focus/keyboard interactivity) stays a purely additive future enhancement — nothing
here blocks it.

## Components (all in `Strata.Dsl`, new files)

| Unit | Responsibility |
|---|---|
| `StrataElement.SetAttribute` | Mutate an attribute in place (binding writes here) |
| `Sparkline` | `double[]` → block-char string (`▁▂▃▄▅▆▇█`), scaled min..max |
| `StrataText` | Shared text selector: kind `Graph` → sparkline from `data`; else `text` attr |
| `StrataStore` | State as `JsonObject`; `Set`/`Append`(+cap) over a dotted path; `Changed` event |
| `StrataBinder` | Resolve `bind-text` / `bind-data` (JSONPath, via JsonPath.Net) against state, write `text` / `data` |
| `StrataLiveHost` | Subscribe to `store.Changed`; on change rebind → cascade → clear → write to an `IAnsiConsole` |

New package refs on `Strata.Dsl`: `JsonPath.Net` (binding reads), `System.Text.Json` (state nodes).

## Data flow (reactive cycle)

```
author loop (ping)               StrataStore (JsonObject)         UI tree (StrataElement)
   └─ Update-StrataStore ──► Set/Append + raise Changed
                                          │
                       StrataLiveHost.render():
                         StrataBinder.Apply(root, state)  ── JSONPath ──►  set text/data attrs
                         Cascade.Compute(root, sheet)
                         console.Clear(); console.Write(SpectreProjection.Project(...))
```

- **Reads** use full JSONPath (`$.hosts.google.history`). **Writes** use a dotted path
  (`$.latency`, `$.history`) — object navigation creating intermediates, plus array append with a
  front-trim cap. Dotted writes cover state shaping; richer reads cover binding.

## PowerShell surface

- `New-StrataStore @{ host='google.com'; latency=0; history=@() }` → store (hashtable → JSON → `JsonObject`).
- `Update-StrataStore $store -Set '$.latency' -Value 12`
- `Update-StrataStore $store -Append '$.history' -Value 12 -Cap 40`
- `Graph -Bind '$.history'` → element kind `Graph`, attribute `bind-data`.
- `Text -Bind '$.latency'` → `bind-text`; positional content still supported for static text.
- `Start-StrataApp -Layout $layout -Store $store -Stylesheet ./monitor.css` → attaches the live
  host (initial render + render-on-change); the author then drives their own sampling loop.

## Templates (sub-project 5)

A reusable layout is a PowerShell function returning a parameterized subtree — the DSL already
makes this natural. Bind paths are parameters:

```powershell
function HostCard($name, $base) {
    Card -Class 'host' {
        Text "$name" -Class 'h2'
        Graph -Bind "$base.history"
        Text -Bind "$base.latency" -Class 'metric'
    }
}
```

Reuse = dot-source or module import. Sub-project 5 is: parameterized `-Bind`, the template
pattern, and a sample reusing one `HostCard` template across an uptime script and a DB-latency
script. No new engine code — it validates composition.

## Error handling — fail fast

- Invalid JSONPath in a binding → `JsonPath.Parse` throws; surfaces, no silent skip.
- A binding whose path matches nothing → the bound attribute is left unset (element renders its
  static fallback); this is expected "no data yet", not an error.
- `Update-StrataStore` with neither `-Set` nor `-Append` → terminating error (nothing to do).

## Testing

- **xUnit**: `Sparkline` scaling/empty; `StrataStore` Set creates intermediates, Append caps from
  front, `Changed` fires; `StrataBinder` resolves scalar + array and writes attrs; `StrataLiveHost`
  renders bound values on `Changed` (captured console); `StrataElement.SetAttribute`.
- **In-process PowerShell**: `New-StrataStore`/`Update-StrataStore` round-trip; `Graph`/`Text -Bind`
  build correct `bind-*` attrs; `Start-StrataApp` + an update renders without error.

## Out of scope (genuine future)

Terminal.Gui interactive projection (focus/keyboard) for these widgets; async multi-source
schedulers; a formal reducer/action-type system (the store exposes direct Set/Append, which the
dashboards need). The reactive layer is built so these are additive.

## Success criteria

`samples/Strata.Demo.PowerShell/ping-monitor.ps1` runs a live ping dashboard (graph + scrolling
history, redrawing each sample) from a `.ps1`, and a `HostCard` template authored once is reused
across a second script — proving sub-projects 2–5 end to end.
