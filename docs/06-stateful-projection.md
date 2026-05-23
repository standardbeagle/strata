# Stateful Projection Authoring — Terminal.Gui v2

**Status:** Phase 7
**Scope:** `Strata.Render.TerminalGui`, the `Show-Processes` full-screen sample, and the live input wiring into the Phase 5/6 interaction layer.

This document explains how to author and drive a *stateful* projection — one whose output objects survive across cascade runs and carry their own state (focus, layout, a driver-side display cache). It is the Terminal.Gui counterpart to the stateless Spectre projection (`docs/03-tech-design.md` §5.1).

## 1. Stateless vs stateful projections

The Spectre projection (`SpectreProjection : IProjection<IRenderable>`) is **stateless**: given the same `(root, cascade)` it rebuilds an equivalent `IRenderable` every call, and the host writes it once. There is nothing to preserve between renders.

Terminal.Gui is different. A `View` is a **stateful object**: it owns focus, layout position, and a driver-side display cache. Rebuilding the view tree on every cascade would discard focus and flicker. So `TerminalGuiProjection : IProjection<View>` **reconciles** instead of rebuilding — the React-style diff sketched in `docs/03-tech-design.md` §5.2, now implemented.

## 2. The reconciliation algorithm

The projection keeps one persistent `View` per logical node, keyed by node identity:

```csharp
private readonly Dictionary<ITreeNode, View> _viewByNode = new();
```

On each `Project(root, cascade)` it walks the tree:

- **New node** (not in the map): create a view, add it to the parent, record it.
- **Existing node**: keep the view; refresh only its mutable, cascade-derived state (`Text`, `ColorScheme`). The view instance — and therefore its focus and layout — is preserved.
- **Removed node** (in the map but no longer in the tree): remove its view from the parent, `Dispose()` it, drop it from the map.

Leaf nodes (no children) project to a `Label`; container nodes project to a plain `View` that stacks its children vertically.

### Why identity matters

Reconciliation rests on `ITreeNode` identity being **stable across cascade runs** (the Phase 0 design guarantee). The same logical node must compare equal between cascades for its view — and the focus living on it — to survive. Adapters must return cached, reference-stable wrappers (or value-equal nodes). If a node is rebuilt fresh each cascade, the projection sees it as "removed + added" and focus is lost.

This is the single authoring contract for a stateful projection: **nodes must be stable, mutable-pseudo-state objects.** The `Show-Processes` sample's `ProcessNode` is reference-identity + `IPseudoStateMutable`, which is exactly what `FocusController` (focus ring) and the projection (reconciliation map) both require.

## 3. Mid-phase decision: diff reconciliation, not the fallback

The plan (`docs/04-plan.md` §Phase 7) flagged this as the highest-risk phase and authorised a fallback — tear-down-and-recreate on each cascade with a focus-restoration heuristic — if clean diff reconciliation proved intractable, with a decision checkpoint at mid-phase.

**Decision: diff reconciliation was taken; the fallback was not needed.** Terminal.Gui v2's `View` exposes everything the diff requires:

- mutable `View.Text` and `View.ColorScheme` for in-place update,
- a `View.Subviews` collection with `Add` / `Remove`,
- `View.Dispose()` for removed-node cleanup.

In-place update is therefore straightforward and there was no need to accept lost focus. The shipped path is the diff. The fallback remains documented here only as the contingency it was.

## 4. The re-cascade loop

A host drives the loop:

1. Compute the cascade → `Project(root, cascade)` builds (or reconciles) the view tree.
2. A keystroke arrives, the dispatcher fires a command, a controller toggles a pseudo-state.
3. The controller's change sink re-cascades and calls `Project` again — which **reconciles in place**, re-styling the affected node's existing view.
4. The host drives Terminal.Gui focus onto the newly `:focused` node via `projection.TryGetView(node, out var view)` → `view.SetFocus()`.

Re-cascade is a full `Cascade.Compute` (the incremental `Cascade.Update` path defers tree-change-driven updates, see `src/Strata.Core/Cascade.cs`).

## 5. Live terminal input — the Phase 6 deferral, now wired

Phase 6 (`docs/05-interaction-redesign.md`) built the interaction layer — `IInputSource`, `InteractionHost`, `FocusController`, `SelectionController` — but drove it from a *programmatic* input source, deferring the live terminal raw-mode layer to Phase 7.

Phase 7 supplies it: `TerminalGuiInputSource : IInputSource`. It adapts Terminal.Gui v2 keystrokes into Strata `HostEvent.Key`s and pushes them onto the same `IInputSource` the interaction layer already speaks. Wire it to a `View.KeyDown` event:

```csharp
window.KeyDown += (_, key) =>
{
    var name = input.HandleKey(key);   // "key.j", "key.ArrowDown", "key.ctrl+c", ...
    if (name is not null) key.Handled = true;
};
```

The keystroke flows: `Terminal.Gui Key → TerminalGuiInputSource → InteractionHost dispatcher → command → FocusController → :focused → re-cascade → reconcile`. The whole interaction layer runs unchanged; only the input *source* is new.

### Event-name mapping

`HandleKey` translates a `Key` into the closed event-name DSL the `command:` property and `KeyBindingMap` already use:

| Terminal.Gui key | Event name |
|---|---|
| `j`, `J`, Shift+`j` | `key.j` |
| Cursor up / down / left / right | `key.ArrowUp` / `key.ArrowDown` / `key.ArrowLeft` / `key.ArrowRight` |
| Space | `key.space` |
| Enter / Esc / Tab | `key.enter` / `key.esc` / `key.tab` |
| Ctrl+`c` | `key.ctrl+c` |
| Alt+`x` | `key.alt+x` |

Letters always lower-case (so a binding fires regardless of caps state; Shift is still carried on the `ConsoleKeyInfo` payload). Modifier order is fixed (`ctrl+`, then `alt+`) so a chord yields one stable name.

## 6. The shared engine — one stylesheet, two renderers

The Phase 7 success criterion is that the *same* selector/cascade engine drives both `Format-Styled` (inline, Spectre) and `Show-Processes` (full-screen, Terminal.Gui). It does: `show-processes.css` uses the identical `Process` / `Process[Status=…]` / `Process.high-cpu` rules as `procs.css`, adding only:

- a `Process:focused` rule (the full-screen focus cursor), and
- `command:` bindings on the `Table` container (live navigation).

Nothing in `Strata.Core`, `Strata.Css`, `Strata.Properties.Styling`, or `Strata.Interaction` is projection-specific. The projection is the only layer that knows about `View`.

## 7. AOT / trim exception

The rest of the Strata engine (`Strata.Core`, `Strata.Css`, `Strata.Layout.Yoga`, `Strata.Render.Spectre`) is AOT- and trim-compatible. **`Strata.Render.TerminalGui` is the documented exception.** Terminal.Gui v2 (prealpha) pulls in `System.IO.Abstractions` and `System.Text.Json` reflection paths that are not trim/AOT clean, so the package sets `IsAotCompatible=false` / `IsTrimmable=false`. Hosts that need AOT use the Spectre projection; the full-screen projection runs JIT.

## 8. Version pin and security note

- **Terminal.Gui** is pinned to `2.0.0-prealpha.216`. The plan named `2.0.0-prealpha.4`, but that build is not published on nuget.org; `.216` is the lowest restorable v2 prealpha and is the tested build.
- Terminal.Gui's transitive `System.Text.Json 8.0.4` carries advisory **GHSA-8g4q-xg66-9fp4**. A central transitive pin bumps it to the patched `8.0.5` (still inside Terminal.Gui's `[8.0.4, 9.0.0)` range).

## 9. What is and isn't tested

Terminal.Gui rendering needs a real terminal driver, so the tests cover the **projection mapping and reconciliation logic** and the **input-name mapping + dispatch**, not the live terminal:

- leaf → `Label`, container → `View` with one subview per child;
- view-instance preservation across re-cascade (the focus-survival guarantee);
- add / remove / in-place-update reconciliation;
- color scheme reflects the cascade; `:focused` re-styles the same view;
- key-name mapping (`key.j`, `key.ArrowDown`, `key.ctrl+c`, caps-folding, unmappable → null);
- end-to-end: a live `Key` drives `FocusController` through the dispatcher.

`View`/`Label` construction and `Add`/`Remove`/`Text`/`ColorScheme`/`Dispose` all work headless (no `Application.Init`), which is what makes the reconciliation tests possible. The `Show-Processes` sample's headless branch builds the view tree and exits when output is redirected, so it is safe to run in CI.
