# Strata — Interaction Redesign (Phases 5 + 6)

**Status:** Draft v0.1 (supersedes `01-requirements.md` FR-12, `02-spec.md` §6, `03-tech-design.md` §6, `04-plan.md` Phase 5 + Phase 6)
**Date:** 2026-05-16

## Premise

The original plan modelled interactivity as an imperative behaviour lifecycle: a stylesheet declared `behavior: kill, meter`, each name resolved through DI keyed services, and `IBehavior.Attach(IBehaviorContext)` / `Detach()` fired as the cascade changed. Behaviours owned instance state, subscribed to per-node events, and published commands.

That model has well-known failure modes — double-attach, detach ordering races, instance-per-(node, name) cache invalidation, lifecycle drift on hot reload. It also burns AOT trim budget on DI keyed-service resolution and reflection.

This redesign replaces the imperative lifecycle with a **selector-bound stream model** inspired by [Cycle.js](https://cycle.js.org/). The stylesheet keeps its declarative binding axis (so UC-5 vim-keymaps stay self-contained), but the implementation under the hood is pure `IObservable<T>` over selector-filtered event streams.

## Goals

1. **Declarative bindings stay in the stylesheet.** A keymap, a sparkline trigger, a status indicator should all be expressible without writing code.
2. **No imperative `Attach`/`Detach` lifecycle.** Subscriptions are `IDisposable`s the engine owns; cascade changes diff the active subscription set, no user-visible callback.
3. **No DI keyed services in core.** Resolution is by registered command-name → handler delegate.
4. **AOT-clean.** No reflection-based instantiation. No `Expression.Compile`. Pure delegates + `System.Reactive`.
5. **Composes via streams.** Keystroke filtering, debounce, throttle, tick-driven animation all use the standard `System.Reactive` operator surface.
6. **Stateful interactions still work.** Handlers close over their own state, or use `Scan`/`Aggregate` on stream inputs.

## Non-goals

- A general-purpose reactive UI framework. Strata stays a projection engine; this layer is the minimum needed for Format-Styled -Interactive and Show-Processes.
- The full Cycle.js `main(sources) => sinks` contract. We borrow the **selector-bound event stream** idea, not the whole framework.
- Replacing CSS cascade for visual properties. Styling stays as in §2–5 of the spec.

## The model

### Stylesheet surface

```css
Row:focused {
    command: "navigate-down" when "key.j";
    command: "navigate-up"   when "key.k";
}

Process[Cpu>50] {
    command: "kill"   when "key.k:held";
    command: "render-sparkline" when "tick";
}
```

The `command:` property is **list-valued and additive** (same deviation from CSS cascade as the original `behavior:` property — documented in §2.3). Each list item carries:

- a command name (string),
- a triggering event name (the `when "…"` clause).

A rule may declare zero or more commands. Comma-separated commands inside one declaration are equivalent to multiple declarations.

The `when` clause uses a small event-name DSL:

| Form | Meaning |
|---|---|
| `key.j` | Keystroke matching key `j`. |
| `key.j:held` | Key held (auto-repeat). |
| `key.ctrl+j` | Modifier + key. |
| `focus` | Node gained focus. |
| `blur` | Node lost focus. |
| `tick` | Engine animation tick (rate set by host). |
| `custom.<name>` | Host-published custom event. |

The DSL is a closed set in v1; new event names are registered by the host before stylesheet parse.

### Host surface

```csharp
public interface IInputSource
{
    /// <summary>Hot stream of events typed by name. The dispatcher feeds it.</summary>
    IObservable<HostEvent> Events { get; }
}

public abstract record HostEvent(string Name)
{
    public sealed record Key(string Name, ConsoleKeyInfo Press) : HostEvent(Name);
    public sealed record Focus(string Name, ITreeNode Node, bool Focused) : HostEvent(Name);
    public sealed record Tick(string Name, TimeSpan Delta) : HostEvent(Name);
    public sealed record Custom(string Name, object? Payload) : HostEvent(Name);
}

public interface ICommandRegistry
{
    /// <summary>Register a handler invoked when a stylesheet command:line fires.</summary>
    void Register(string commandName, CommandHandler handler);
}

public delegate void CommandHandler(CommandContext context);

public readonly record struct CommandContext(
    ITreeNode Node,
    HostEvent Event,
    ICascadeResult Cascade);
```

The host wires:

1. **Stylesheet → command-subscription set.** At cascade time the engine produces, for every node, the list of `(commandName, eventName)` pairs the cascade resolved. The interaction layer diffs against the prior set and resubscribes accordingly.
2. **Event stream → selector-filtered dispatch.** When the engine sees a `HostEvent` of `Name == "key.j"`, it looks up every active `(node, "navigate-down", "key.j")` subscription and invokes the registered handler for `"navigate-down"`.
3. **Handler lookup.** The handler comes from `ICommandRegistry.Register("navigate-down", handler)`. Single registration per name; second-to-register loses with a clear error.

No `IBehavior` interface. No DI keyed service. No `Attach`/`Detach` callbacks. The engine owns subscription disposal and the dispatcher is a single switch over event-name → handler.

### Stateful interactions

Handlers close over their own state, or compose with `System.Reactive` operators against the input source:

```csharp
// Sparkline keeps a 60-sample buffer per node.
var buffers = new ConcurrentDictionary<ITreeNode, RingBuffer<double>>();
commands.Register("render-sparkline", ctx =>
{
    var buf = buffers.GetOrAdd(ctx.Node, _ => new RingBuffer<double>(60));
    if (ctx.Node.TryGetAttribute("Cpu", out var v) && v is double cpu)
    {
        buf.Add(cpu);
        // Mark the node so the projection picks up the latest sparkline glyph.
        // Re-cascade will pull through the projection's sparkline drawing path.
    }
});
```

Or fully functional via the input stream:

```csharp
host.Input.Events
    .OfType<HostEvent.Tick>()
    .Scan(0.0, (acc, t) => acc + t.Delta.TotalSeconds)
    .Subscribe(elapsed => Engine.RaiseCustom("clock", elapsed));
```

### Lifecycle (replacing Attach/Detach)

The diff is at the **subscription** level, not the instance level. For every node, the engine tracks the set of `(commandName, eventName)` pairs the cascade resolved. When the cascade re-runs:

| Change | Action |
|---|---|
| Pair appeared on a node | Add an `IObservable` filter for the matching event-name on the matching node; route hits through `commands[commandName]`. |
| Pair removed from a node | Dispose the filter. |
| Pair present in both runs | Leave the existing subscription alone. |

There is no user-visible attach/detach event. Handlers are pure functions of `CommandContext`. State lives in caller-owned closures.

If a handler wants explicit "first invocation on this node" semantics it can keep a `HashSet<ITreeNode>` of seen nodes — but that is now the caller's problem, not a lifecycle the engine has to choreograph correctly across cascade re-runs.

## What this changes in existing docs

| Doc | Section | Disposition |
|---|---|---|
| `01-requirements.md` | FR-12 (Behavior attachment) | **Superseded.** New FR: "Selector-bound interaction. Stylesheet declares `command: name when event`; engine subscribes/unsubscribes as the cascade changes; handlers register by command name." |
| `02-spec.md` | §6 (Behaviors) | **Superseded.** New section: `command:` property syntax, `IInputSource`, `ICommandRegistry`, `HostEvent` records. |
| `03-tech-design.md` | §6 (Behavior lifecycle) | **Superseded.** New: subscription-diff algorithm, no `BehaviorHost`, no DI keyed services. |
| `04-plan.md` | Phase 5 (Behaviors) + Phase 6 (Commands + input) | **Combined.** Single Phase 5: "Interactions: command property + input dispatcher + selector-bound subscriptions". Roughly 1.5 weeks, replacing the prior 2.5 weeks. |
| `04-plan.md` | Decision log | Add: *(Phase 5)* Interaction model: selector-bound observables, not IBehavior attach/detach. Replaces additive-`behavior:` cascade semantics with additive-`command:` semantics (same idea, narrower contract). |

## What this does **not** change

- Cascade engine, selectors, properties, projections — unchanged.
- The `:focused` / `:selected` / `:hovered` / `:expanded` pseudo-state model — unchanged (these stay attribute-style toggles).
- The Format-Styled (UC-1) inline read-only path — does not touch interaction at all.
- The Spectre / Terminal.Gui projections — unchanged, except that Terminal.Gui's reconciliation no longer has to coordinate with a separate `BehaviorHost`.

## Out of scope for this redesign

- A general event bus across the whole app. Strata only owns events flowing from `IInputSource` into selector-filtered subscriptions. Cross-cutting app concerns use whatever IPC the host already has.
- Replacing System.Reactive with a custom stream library. We adopt `System.Reactive` directly and accept its surface as the public contract.

## Open questions to lock down before Phase 5 implementation

1. **Event name namespace.** Are `key.*` / `focus` / `tick` / `custom.*` enough, or do we need `mouse.*` and `paste`? The terminal context makes mouse optional; defer until a real consumer demands it.
2. **Where does focus live?** Pseudo-state on the node (already in the model) — but who toggles it? Proposal: the input dispatcher owns a `FocusController` that mutates the focused node's pseudo-state set in response to navigation commands.
3. **Re-entrancy.** A handler firing `Engine.RaiseCustom` could trigger another handler. Cap to "deferred until current dispatch unwinds" to keep semantics simple; document.
4. **Hot reload.** If the stylesheet changes the `command:` set, the subscription diff is exactly the same as for tree changes — that part falls out for free.

## Risks

| Risk | Mitigation |
|---|---|
| Loss of declarative power compared to "whole keymap in css" | Mitigated by `command:` declaration in the stylesheet; only handler bodies move to code, where they belong anyway. |
| `System.Reactive` is a heavyweight dep | Mitigated by adopting only the small surface (`IObservable`, `Subject`, `Where`, `OfType`, `Scan`). Acceptable. |
| Discoverability — which selectors emit which commands | Mitigated by the existing `IStrataInspector.Dump` — it already enumerates a node's properties; `command:` shows up alongside `color:` etc. |
| Stateful behaviors more awkward in the functional style | Acceptable — most "behaviors" in practice are stateless dispatchers; the sparkline buffer is the worst case and a `ConcurrentDictionary` closure handles it. |
