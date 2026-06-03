# Strata PowerShell — Interactive Apps on Terminal.Gui (tier 3)

**Status:** Design v1.0
**Date:** 2026-06-03
**Owner:** Andy Brummer / StandardBeagle
**Builds on:** the shipped DSL skeleton + reactive live layer
(`2026-06-03-strata-powershell-dsl-skeleton-design.md`,
`2026-06-03-strata-powershell-reactive-live-design.md`).

## Goal

Author a **full-screen interactive** terminal app in a `.ps1`: focusable controls, a text input,
a scrollable/selectable list, keyboard + mouse, driven by the same reactive store. The motivating
scenario is a DB-query tool — type a query, run it, scroll/select result rows — which the
output-only Spectre path cannot do. This is the HTA-equivalent's interactive half.

The interactive loop already exists in C# (the `Show-Processes` Terminal.Gui demo wires it). This
sub-project adds the **PowerShell surface**, **editable/scrollable widgets**, and **store
integration** onto that spine; it does not reinvent input, focus, or reconciliation.

## What already exists (reused unchanged)

- `Strata.Interaction`: `InteractionHost` (diffs `(node,command,event)` subscriptions over an
  `IInputSource` stream, dispatches to `ICommandRegistry` handlers), `FocusController` (`:focused`
  ring, j/k/arrows), `SelectionController` (`:selected` set), the `command:`/`when` stylesheet DSL.
- `Strata.Render.TerminalGui`: `TerminalGuiProjection` (reconciling `View` tree, kinds
  Button/Dialog/Modal/Popup/leaf/container, vertical-stack + `position:absolute` layout, focus
  preserved across re-cascade), `TerminalGuiInputSource` (TG `Key` → `HostEvent.Key`).
- The host loop pattern (from the demo): `Application.Init` → `Window` → `FocusController.onChange`
  re-cascades, re-projects (reconcile), `host.Reconcile`, `SetFocus`, `SetNeedsDisplay` →
  `Application.Run`.

## Decisions (settled)

- **Behavior model — scriptblocks primary, CSS `command:` for advanced keymaps.** PS authors write
  `Button 'Run' -OnSelect { ... }`; the host wires that scriptblock to the widget's native TG event.
  CSS `command: "name" when "key.j"` stays available for declarative/vim keymaps, with handlers
  registered via `Register-StrataCommand`.
- **Separate `Show-StrataApp` cmdlet** = blocking full-screen Terminal.Gui app. `Start-StrataApp`
  (Spectre dashboard) is unchanged. The author chooses by intent — no auto-switching.
- **v1 widgets: Button, TextField, List.** Button (exists) + TextField (text input, two-way bound)
  + List (scrollable, selectable, bound to an array). Table deferred.

## Architecture

### New project: `Strata.Dsl.TerminalGui`

The interactive host. References `Strata.Dsl`, `Strata.Render.TerminalGui`, `Strata.Interaction`,
`Terminal.Gui`. Keeps the TG dependency out of `Strata.Dsl`. The PS module loads this assembly for
`Show-StrataApp`.

- **`StrataInteractiveHost`** — owns the TG app lifecycle and the reactive cycle.

### New TG widget kinds (in `Strata.Render.TerminalGui`)

`TerminalGuiProjection.CreateView` gains two kinds:

- **`TextField`** → a TG `TextField`, initial `Text` from the bound value (`bind-value`).
- **`List`** → a TG `ListView` whose items come from the bound array (`bind-data`); built-in scroll
  + selection.

Both are reconciled like existing views (reference-stable node identity preserves edit/scroll
state across re-cascade). Button already projects to a TG `Button`.

### `StrataElement` becomes focusable

`StrataElement` implements `IPseudoStateMutable` (`AddPseudoState`/`RemovePseudoState` over its
existing mutable `_pseudoStates` set) so `FocusController`/`SelectionController` can toggle
`:focused`/`:selected` on DSL elements. This is the one change to the shipped element type.

## The reactive + interactive cycle

```
                    ┌───────────────────────────── store.Changed ──────────────────────────┐
                    │                                                                       │
author -OnSelect/   ▼                                                                       │
native TG event ─► scriptblock handler ─► Update-StrataStore (mutate state) ────────────────┘
                                                                                            
TG keystroke ─► TerminalGuiInputSource ─► InteractionHost ─► command handler ─► FocusController
                                                                  │                    │
                                                                  └── re-render ◄───────┘

re-render():  StrataBinder.Apply(root, store.State)         (store → text/data/value attrs)
              cascade.Compute(root, stylesheet)
              projection.Project(root, cascade)             (reconcile TG view tree in place)
              host.Reconcile(root, cascade)                 (re-diff command subscriptions)
              window.SetNeedsDisplay()
```

- **store → UI** uses the existing `StrataBinder` (one-way: `bind-text`/`bind-data`, plus new
  `bind-value` for TextField initial text).
- **UI → store** is wired by the host on native TG events: `TextField.TextChanged` writes the field
  text to the store at its `bind-value` path; `ListView` selection writes the selected index/value
  to its `bind-selection` path. These store writes raise `Changed`, closing the loop.
- Store mutations from a background sampling thread marshal onto the UI thread via
  `Application.Invoke` before re-rendering.

## Behavior wiring (the two channels)

**1. Scriptblock handlers (native TG events).** A DSL widget with `-OnSelect`/`-OnChange`/`-OnEnter`
stores the scriptblock keyed by a generated handler id in an attribute (`on-select`, `on-change`,
`on-enter`). After projecting, the host looks up each widget's TG view (`projection.TryGetView`) and
attaches:

| Widget | DSL param | TG event |
|---|---|---|
| Button | `-OnSelect` | `Button.Accept` |
| TextField | `-OnChange` | `TextField.TextChanged` |
| List | `-OnEnter` | `ListView.OpenSelectedItem` |

The handler is invoked with a context object `{ Store, Element, Value }` (`Value` = field text or
selected item). The author reads/writes `$ctx.Store` via `Update-StrataStore`.

**2. CSS `command:` keymaps (InteractionHost).** Unchanged engine path for declarative keymaps
(focus nav, vim keys, custom keys). PS authors register named handlers:
`Register-StrataCommand -Name 'run-query' -Action { param($ctx) ... }`, and bind keys in the
stylesheet (`Input:focused { command: "run-query" when "key.ctrl+enter"; }`). `FocusController`'s
j/k/tab navigation is registered for free by the host.

Mouse: TG widgets handle click-to-focus and list click-select natively — no extra wiring for v1.
Custom mouse→command CSS bindings are deferred.

## PowerShell surface (new)

```powershell
Show-StrataApp -Layout $layout -Store $store -Stylesheet ./app.css
```

- `Button 'Run Query' -OnSelect { param($ctx) Invoke-Query $ctx.Store }`
- `TextField -Bind '$.query' -OnChange { param($ctx) }`   # two-way bound to $.query
- `List -Bind '$.rows' -OnEnter { param($ctx) Show-Detail $ctx.Value }`  # items from $.rows
- `Register-StrataCommand -Name '<name>' -Action { param($ctx) ... }`   # for CSS command: keymaps
- `Show-StrataApp -Layout -Store -Stylesheet`  # blocks on the TG loop until quit (q/Esc), full-screen

`Show-StrataApp` runs headless-safe: when output/input is redirected (CI), it projects the view tree
once and returns a summary instead of entering `Application.Run` (the pattern the demo already uses).

## Example — DB query tool

```powershell
$store = New-StrataStore @{ query = 'SELECT * FROM users'; rows = @() }

$layout = Stack -Class 'app' {
    Text 'DB Query' -Class 'h1'
    TextField -Bind '$.query' -Class 'input'
    Button 'Run' -Class 'run' -OnSelect {
        param($ctx)
        $q = $ctx.Store.State['query'].ToString()
        $results = Invoke-DbQuery $q | ForEach-Object { $_.Name }
        Update-StrataStore $ctx.Store -Set '$.rows' -Value $results
    }
    List -Bind '$.rows' -Class 'results' -OnEnter { param($ctx) }
}

Show-StrataApp -Layout $layout -Store $store -Stylesheet ./query.css
```

## Error handling — fail fast

- Missing terminal driver / real-terminal-only operations on redirected IO → headless projection
  summary, not a crash.
- A scriptblock handler that throws → surfaced to the author's session as a terminating error after
  `Application` shutdown (errors during the loop are collected and rethrown), not swallowed.
- `bind-value` on a non-TextField, or `bind-selection` on a non-List → ignored (the attribute only
  has meaning for its widget).
- Duplicate `Register-StrataCommand` name → throws (matches `ICommandRegistry` contract).

## Testing

Terminal.Gui's `Application.Run` needs a real driver, so the loop itself is smoke-tested headless
(as the existing 27 `Strata.Render.TerminalGui.Tests` do), and the wiring logic is unit-tested.

- **xUnit (`Strata.Render.TerminalGui.Tests`)**: `CreateView` builds a `TextField` with the bound
  initial text and a `ListView` with the bound items; reconciliation preserves a `TextField`
  instance (and its caret) across re-cascade.
- **xUnit (`Strata.Dsl.TerminalGui.Tests`, new)**: `StrataElement` implements `IPseudoStateMutable`
  (focus toggles `:focused`); the UI→store write helpers (field-text → path, selection → path)
  mutate state and raise `Changed`; headless `Show-StrataApp` projects without entering the loop.
- **In-process PowerShell**: `Button -OnSelect`/`TextField -OnChange`/`List -OnEnter` set the
  expected `on-*` attributes; `Register-StrataCommand` registers a handler; a headless
  `Show-StrataApp` over a small layout returns its projection summary without error.

## Out of scope (future)

Table/TableView widget; Yoga-driven TG layout (v1 keeps TG stacking + `position:absolute`); custom
mouse→command CSS bindings; menus/dialog-driven navigation; async query cancellation. The store +
binding + focus model is built so these are additive.

## Success criteria

`samples/Strata.Demo.PowerShell/db-query.ps1` runs a full-screen app: type into the query field,
press Run (or a bound key) to populate results, arrow/scroll + Enter through the result list — all
on the reactive store, authored entirely in a `.ps1`.
