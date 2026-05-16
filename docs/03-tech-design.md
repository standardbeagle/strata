# Strata — Technical Design

**Status:** Draft v0.1
**Scope:** Internal architecture, algorithms, data structures, performance approach

This document describes how Strata is built internally. Public contracts are in `02-spec.md`.

## 1. Architecture overview

```
                  ┌────────────────────────────────┐
                  │     Strata.Abstractions        │
                  │  ITreeNode, ISelector,         │
                  │  IProjection, IPropertyValue   │
                  └────────────────┬───────────────┘
                                   │
                  ┌────────────────▼───────────────┐
                  │         Strata.Core            │
                  │  Cascade engine, registries,   │
                  │  primitives, specificity,      │
                  │  rule index, inheritance       │
                  └──┬────────────┬────────────┬───┘
                     │            │            │
              ┌──────▼─────┐ ┌────▼─────┐ ┌────▼──────┐
              │ Strata.Css │ │ JsonPath │ │ Behaviors │
              └────────────┘ └──────────┘ └───────────┘

  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
  │ Adapters.PSObject│ │ Layout.Yoga      │ │ Render.Spectre   │
  └──────────────────┘ └──────────────────┘ └──────────────────┘
                                            ┌──────────────────┐
                                            │Render.TerminalGui│
                                            └──────────────────┘
```

The flow at runtime:

1. **Adapter** wraps source objects into `ITreeNode`.
2. **Selector language** parses stylesheet text into `IStylesheet`.
3. **Cascade engine** computes per-node winning declarations.
4. **Layout pass** (optional) maps computed values into Yoga, computes box rects.
5. **Projection** turns the cascade + layout into a target value space (Spectre `IRenderable`, Terminal.Gui `View`, GraphQL resolver, React element).

Layers are independent. A caller can use just selector matching without cascade, just cascade without layout, just layout without rendering, etc.

## 2. Internal data structures

### 2.1 Rule index

The cascade engine maintains a precomputed index of rules by their subject selector's primary key. The primary key is, in priority order: `Id`, `Kind`, first `Class`. Rules without any of these go in a catch-all bucket.

```csharp
internal sealed class RuleIndex
{
    private readonly Dictionary<string, List<CompiledRule>> _byId = new();
    private readonly Dictionary<string, List<CompiledRule>> _byKind = new();
    private readonly Dictionary<string, List<CompiledRule>> _byClass = new();
    private readonly List<CompiledRule> _universal = new();

    public RuleCandidates Candidates(ITreeNode node);
}

internal readonly struct RuleCandidates
{
    // Three buckets to walk in order. Each is potentially empty.
    public List<CompiledRule> ById { get; }
    public List<CompiledRule> ByKind { get; }
    public List<CompiledRule>[] ByClass { get; }  // one bucket per class on the node
    public List<CompiledRule> Universal { get; }
}
```

For a typical 100-rule stylesheet against a 1000-node tree, this turns a naive 100k matches into ~5k. Browser engines have measured this optimization to be the single largest perf win in CSS engines.

### 2.2 Compiled rule

Rules are compiled once at stylesheet parse time:

```csharp
internal sealed class CompiledRule
{
    public IRule Source { get; }
    public CompiledSelector Subject { get; }   // rightmost compound
    public ComplexSelectorPart[] Context { get; }  // ancestors/siblings, right-to-left
    public Specificity Specificity { get; }
    public int SourceOrder { get; }
}

internal sealed class CompiledSelector
{
    public string? Kind { get; }
    public string? Id { get; }
    public string[] Classes { get; }
    public AttributeMatcher[] Attributes { get; }
    public PseudoClassMatcher[] PseudoClasses { get; }
}

internal sealed class AttributeMatcher
{
    public string Name { get; }
    public AttributeOp Op { get; }
    public string? Value { get; }
    public Func<object?, bool>? CompiledPredicate { get; }  // for [expr] form
}
```

Compiled predicates (`[CPU > 50]`) are produced by `System.Linq.Dynamic.Core` at parse time. The expression compiles against the **declared source type** of the adapter (e.g., `System.Diagnostics.Process` for the PSObject adapter when its underlying is a process). For dynamic objects (PSObject without a strong type), the predicate falls back to PSObject property lookup at match time — slower but still cached.

### 2.3 Cascade result

Cascade results are immutable and per-tree-snapshot. Internally:

```csharp
internal sealed class CascadeResult : ICascadeResult
{
    private readonly Dictionary<ITreeNode, NodeResult> _byNode;
    private readonly IPropertyRegistry _properties;
}

internal sealed class NodeResult
{
    public CompiledRule[] MatchedOrdered { get; }  // winner first
    public Dictionary<string, ResolvedDeclaration> WinningByProperty { get; }
}

internal readonly struct ResolvedDeclaration
{
    public IRule Rule { get; }
    public Declaration Declaration { get; }
}
```

Inherited values are not stored eagerly. `GetComputed` looks up locally; on miss, if the property is inheritable, walks `Parent` until it finds a value or returns `Initial`. This is cheaper for properties no one reads and bounded by tree depth, which is usually small.

### 2.4 Pooling

The hot path allocations the engine MUST pool:

- `List<CompiledRule>` candidate buffers used in matching
- `RuleApplication[]` arrays returned to projections
- Inheritance walk frames if recursion is converted to a loop

`ArrayPool<T>.Shared` is used for buffers. Result arrays are sized exactly and returned via builder pattern.

## 3. Algorithms

### 3.1 Selector matching (right-to-left)

```csharp
bool TryMatch(CompiledRule rule, ITreeNode node, out MatchContext context)
{
    // 1. Subject check
    if (!MatchesCompound(rule.Subject, node, out context))
        return false;

    // 2. Walk context parts right-to-left
    ITreeNode? cursor = node;
    foreach (var part in rule.Context)
    {
        cursor = part.Combinator switch
        {
            Combinator.Descendant => FindAncestor(cursor, part.Selector),
            Combinator.Child      => MatchParent(cursor, part.Selector),
            Combinator.AdjSibling => MatchPrevSibling(cursor, part.Selector),
            Combinator.GenSibling => FindPrevSibling(cursor, part.Selector),
        };
        if (cursor is null) return false;
    }

    return true;
}
```

`FindAncestor` and `FindPrevSibling` walk until they find a match. `MatchParent` and `MatchPrevSibling` test only one node.

### 3.2 Cascade computation

```csharp
NodeResult ComputeNode(ITreeNode node, RuleIndex index)
{
    var candidates = index.Candidates(node);
    var matched = ListPool<CompiledRule>.Rent();

    foreach (var bucket in candidates.AllBuckets)
        foreach (var rule in bucket)
            if (TryMatch(rule, node, out _))
                matched.Add(rule);

    // Stable sort: (Important DESC, Specificity DESC, SourceOrder DESC)
    matched.Sort(RulePrecedenceComparer.Instance);

    // For each property, first winner across declarations wins.
    var winning = new Dictionary<string, ResolvedDeclaration>();
    foreach (var rule in matched)
        foreach (var decl in rule.Source.Declarations)
            if (!winning.ContainsKey(decl.Property))
                winning[decl.Property] = new(rule.Source, decl);

    var result = new NodeResult(matched.ToArray(), winning);
    ListPool<CompiledRule>.Return(matched);
    return result;
}
```

Important: the comparer sorts so that the **first** matching rule for any property is the winner. This means iterating once over `matched` and skipping already-set properties.

Precedence sort key (high-to-low):
1. `Important` flag of the rule's strongest declaration for the property (this is per-declaration during the inner loop)
2. `Specificity`
3. `SourceOrder`

Important within important compares the same way, so the comparer handles it uniformly.

### 3.3 Incremental update

```csharp
ICascadeResult Update(ICascadeResult prior, IReadOnlyList<TreeChange> changes, ...)
{
    var dirty = new HashSet<ITreeNode>();

    foreach (var change in changes)
    {
        switch (change)
        {
            case TreeChange.Inserted ins:
                CollectSubtree(ins.Node, dirty);
                break;
            case TreeChange.Removed rem:
                // Just forget the subtree.
                ForgetSubtree(prior, rem.Node);
                break;
            case TreeChange.ClassChanged cc:
                dirty.Add(cc.Node);
                if (HasDescendantSelectorOnClass(cc.Class))
                    CollectDescendants(cc.Node, dirty);
                break;
            case TreeChange.PseudoStateChanged psc:
                dirty.Add(psc.Node);
                if (PropertyInheritsThroughState(psc.State))
                    CollectDescendants(psc.Node, dirty);
                break;
            case TreeChange.AttributeChanged ac:
                dirty.Add(ac.Node);
                break;
        }
    }

    var result = prior.Copy();
    foreach (var node in dirty)
        result.ReplaceNode(node, ComputeNode(node, _index));
    return result;
}
```

`HasDescendantSelectorOnClass` is a per-class flag computed at index build time. It's `true` if any compiled rule uses this class as a non-subject (i.e., context) part.

### 3.4 Specificity comparison

`Specificity` is a `record struct` with `IComparable<Specificity>` implemented as lexicographic on `(A, B, C)`. This is two-three int comparisons, branch-predictable, free.

## 4. Layout integration (Yoga)

### 4.1 Mapping

A layout pass walks the styled tree and builds a parallel Yoga tree. Cached on the cascade result.

```csharp
public sealed class YogaLayoutPass
{
    public LayoutResult Compute(
        ITreeNode root,
        ICascadeResult cascade,
        Size availableSize);
}

public sealed class LayoutResult
{
    public Rect GetRect(ITreeNode node);
    public bool IsClipped(ITreeNode node);
}
```

The mapping table (computed style → Yoga property):

| Strata property | Yoga property |
|---|---|
| `display` | `display` (flex/grid/none) |
| `flex-direction` | `FlexDirection` |
| `flex-grow` / `flex-shrink` / `flex-basis` | `FlexGrow` / `FlexShrink` / `FlexBasis` |
| `align-items` / `justify-content` | `AlignItems` / `JustifyContent` |
| `width` / `height` | `Width` / `Height` |
| `min-width` / `max-width` etc | corresponding |
| `padding-*` / `margin-*` | corresponding |
| `position` (`absolute`) + `top/left/right/bottom` | `PositionType` + edges |
| `grid-template-columns` / `grid-template-rows` | Yoga grid (3.2+) |
| `gap` / `row-gap` / `column-gap` | corresponding |

Yoga's unit is the same as ours: terminal cells. We pass `availableSize` in cells, get back rects in cells.

### 4.2 When to skip layout

Layout is optional. If no node declares `display`, padding, margin, or a positional property, the projection can skip the Yoga pass entirely and render in document order. The `LayoutResult` exposes a `Trivial` flag for this case.

## 5. Projections

### 5.1 Spectre.Console

`SpectreProjection : IProjection<IRenderable>`. The implementation:

1. Walks the styled+laid-out tree depth-first.
2. For leaf-like nodes, emits `Markup` or `Text` with cascaded color/decoration.
3. For container nodes with `display: grid`, emits a Spectre `Grid` or, when absolute positioning is in play, a `Canvas`.
4. Borders, panels, padding from `Panel` widget.
5. Color values resolve through Spectre's `Color` (we don't roll our own — we adopt Spectre's color value and downgrade logic wholesale).

The projection is stateless. Given the same `cascade + layout`, it emits the same `IRenderable`. ps-bash invokes `AnsiConsole.Write(renderable)` and is done.

### 5.2 Terminal.Gui v2 (Phase 7)

`TerminalGuiProjection : IProjection<View>` is fundamentally different — Terminal.Gui has stateful Views that we need to update, not recreate, between cascades.

The strategy is React-style reconciliation:

```csharp
public sealed class TerminalGuiProjection : IProjection<View>
{
    private readonly Dictionary<ITreeNode, View> _viewByNode = new();

    public View Project(ITreeNode root, ICascadeResult cascade)
    {
        return Reconcile(root, cascade, parent: null);
    }

    private View Reconcile(ITreeNode node, ICascadeResult cascade, View? parent)
    {
        if (!_viewByNode.TryGetValue(node, out var view))
        {
            view = CreateView(node, cascade);
            _viewByNode[node] = view;
            parent?.Add(view);
        }
        else
        {
            UpdateView(view, node, cascade);
        }

        // Reconcile children: diff existing children against current.
        ReconcileChildren(view, node, cascade);
        return view;
    }
}
```

Removed nodes need explicit cleanup (`view.Dispose()` and removal from parent). Behaviors attached to removed nodes get `Detach()` called by the behavior lifecycle, not the projection.

This is the highest-risk part of the design and is explicitly Phase 7. If reconciliation proves too complex, a fallback option is tear-down-and-recreate on each cascade, accepting lost focus state.

## 6. Interaction lifecycle

**Superseded — see `docs/05-interaction-redesign.md` for the canonical algorithm.** The interaction layer diffs the active subscription set `(node, command, event)` against the prior cascade. No `BehaviorHost`, no DI keyed services. The text below describes the original design; retained for context.

### 6.legacy Behavior host (deprecated)

```csharp
internal sealed class BehaviorHost
{
    private readonly Dictionary<(ITreeNode, string), IBehavior> _attached = new();
    private readonly IServiceProvider _services;

    public void Reconcile(ITreeNode node, ICascadeResult cascade)
    {
        var desired = ExtractBehaviorNames(node, cascade);  // from `behavior:` prop
        var current = _attached.Keys
            .Where(k => k.Item1.Equals(node))
            .Select(k => k.Item2)
            .ToHashSet();

        foreach (var name in current.Except(desired))
            Detach(node, name);

        foreach (var name in desired.Except(current))
            Attach(node, name);
    }

    private void Attach(ITreeNode node, string name)
    {
        var behavior = _services.GetRequiredKeyedService<IBehavior>(name);
        var context = new BehaviorContext(node, _services, _eventBus);
        behavior.Attach(context);
        _attached[(node, name)] = behavior;
    }

    private void Detach(ITreeNode node, string name)
    {
        if (_attached.Remove((node, name), out var behavior))
        {
            behavior.Detach();
            if (behavior is IDisposable d) d.Dispose();
        }
    }
}
```

The behavior host is owned by the rendering driver (ps-bash's interactive loop, the TUI projection, etc.). It runs after each cascade and before each render.

Event delivery uses `System.Reactive` `Subject<NodeEvent>` per behavior context. Input events from the keyboard are dispatched to focused/selected nodes' behavior contexts.

## 7. AOT and trimming

### 7.1 Forbidden constructs

In `Strata.Abstractions` and `Strata.Core`:

- No `Type.GetType(string)`
- No `Activator.CreateInstance` with type arg
- No `Assembly.Load`
- No `System.Reflection.Emit`
- No DynamicProxy / Castle / DispatchProxy
- No `dynamic` keyword
- No `Expression.Compile()` (this rules out some Dynamic.Core use — but Dynamic.Core lives in `Strata.Css`, not Core; see 7.3)

### 7.2 LINQ

LINQ is permitted in cold paths (parsing, registration). It is forbidden in hot paths (`Matches`, `ComputeNode`, `GetComputed`). Hot paths use explicit loops and `Span<T>`.

### 7.3 Dynamic.Core and AOT

`System.Linq.Dynamic.Core` uses `Expression.Compile()` internally, which historically conflicted with NAOT. As of .NET 9 and Dynamic.Core 1.6+, compiled expressions work under AOT for non-trim-aware types. Strata.Css uses Dynamic.Core only for typed predicates, which:

- Live in their own assembly, trim-able if predicates aren't used
- Are compiled at stylesheet load, not at match time
- Fall back to a slower dictionary-driven evaluator when the expected source type is `dynamic`/`PSObject`

If Dynamic.Core proves AOT-incompatible in practice, a fallback predicate language (subset, hand-written parser) will be added behind the same `AttributeMatcher` interface. This is a known risk; see Plan §Phase 1.

### 7.4 Source generators

Property registration MAY use a source generator (`Strata.SourceGen`) so that downstream packages can do:

```csharp
[StrataProperty("color", Inherits = true, ValueType = typeof(ColorValue))]
public static partial class StylingProperties { }
```

and have the registration code emitted at compile time, with no reflection scan at startup. This is a nice-to-have, not required for v1.

## 8. Threading

Cascade instances are not thread-safe. The expected pattern:

- One cascade per logical "session" (a ps-bash prompt, a TUI app, a Bifrost compile).
- Cascade lives on a single thread.
- Cascade results are immutable: a projection can be invoked on a worker thread with a snapshot.
- Tree mutation must be marshaled to the cascade's thread.

For ps-bash interactive, the cascade lives on the prompt thread. For Terminal.Gui, on the application's main loop thread.

## 9. Diagnostics

### 9.1 Dump

```csharp
inspector.Dump(root, cascade);
// → human-readable per-node:
// Process #1234 [chrome] .high-cpu :focused
//   matched rules:
//     1. Process[CPU>50] (B=1, C=1) → color: red, behavior: meter
//     2. Process:focused (B=1, C=1) → background: dark-blue
//   computed:
//     color: red (from rule 1)
//     background: dark-blue (from rule 2)
//     font-weight: bold (inherited from #parent)
//     ...
```

### 9.2 Explain

```csharp
inspector.Explain(node, "color", cascade);
// → "color = red, declared by rule Process[CPU>50] @ procs.css:14
//    (specificity (0,1,1), source order 3),
//    winning over Process (0,0,1) @ procs.css:3"
```

### 9.3 EventSource

`Strata.Diagnostics` provides an `EventSource` (`Strata-Diagnostics`) that emits:

- `CascadeComputed(rootKind, nodeCount, ruleCount, durationMs)`
- `NodeMatched(nodeKind, ruleCount)`
- `BehaviorAttached(behaviorName, nodeKind)`
- `BehaviorDetached(behaviorName, nodeKind)`

For PerfView, dotnet-trace, and ETW capture.

## 10. Testing strategy

### 10.1 Unit tests

xUnit, FluentAssertions. Each package has its own test project.

### 10.2 Golden-file tests

`Strata.Css` has a `Conformance` test project that runs a curated set of selector + tree fixtures and compares against `.expected` JSON files. The corpus covers each grammar production at least once and grows with bug reports.

### 10.3 Property-based tests

`Strata.Core` cascade resolution uses FsCheck to verify:

- Cascade is deterministic across input shuffling
- Specificity is a total order
- Sort is stable (source order preserved on ties)
- Adding `!important` to the winning declaration is a no-op

### 10.4 Benchmark suite

BenchmarkDotNet covers:

- 100-rule stylesheet vs 1k, 10k, 100k node trees
- Full re-cascade vs incremental for class toggle, pseudo-state toggle, single-attribute change
- Spectre projection time vs cascade time
- AOT mode vs JIT mode (separate jobs)

Targets:

- 1k node × 100 rule full cascade: < 5 ms on a modern desktop
- Incremental pseudo-state toggle: < 100 µs
- Allocation per cascade: < 1 MB (per NFR-3)

### 10.5 AOT verification

A `Strata.AotTests` project sets `PublishAot=true` and exercises the public surface in a small console app. The CI build fails if AOT publish produces any trimmer or AOT warning.

## 11. Repository layout

```
strata/
├── docs/
│   ├── 01-requirements.md
│   ├── 02-spec.md
│   ├── 03-tech-design.md
│   ├── 04-plan.md
│   └── selector-grammar.ebnf
├── src/
│   ├── Strata.Abstractions/
│   ├── Strata.Core/
│   ├── Strata.Css/
│   ├── Strata.JsonPath/
│   ├── Strata.Behaviors/
│   ├── Strata.Properties.Styling/
│   ├── Strata.Layout.Yoga/
│   ├── Strata.Render.Spectre/
│   ├── Strata.Render.TerminalGui/
│   ├── Strata.Adapters.PSObject/
│   ├── Strata.Adapters.JsonNode/
│   └── Strata.Diagnostics/
├── tests/
│   ├── Strata.Core.Tests/
│   ├── Strata.Css.Tests/
│   ├── Strata.Css.Conformance/
│   ├── ...
│   └── Strata.AotTests/
├── benchmarks/
│   └── Strata.Benchmarks/
├── samples/
│   ├── PsBash.FormatStyled/
│   ├── PsBash.ProcessExplorer/
│   ├── ReducerRouter/
│   └── BifrostAdapter/
└── Strata.sln
```

A single solution. Each `src/` project becomes its own NuGet package.

## 12. Open design questions

1. **Specificity model for JSONPath** — the proposed `(named-steps, filters, wildcards)` is plausible but untested against a real reducer router use case. Revisit in Phase 9.

2. **Should `behavior:` be a real property or a special form?** If a property, it sits inside the cascade and supports specificity-based override. If a special form, it has additive semantics (multiple rules contribute behaviors, none overrides). The current design picks "real property with a list value and additive append rather than override" — this is a deliberate departure from CSS cascade for this one property. Document clearly.

3. **Mutable trees vs snapshots** — currently `ITreeNode` is expected to be mutable, and adapters fire `TreeChange` events. An alternative is forcing snapshots, with the adapter responsible for diffing. Mutable is simpler for ps-bash but harder for thread safety. Lock down in Phase 0.

4. **Capture flow into projections** — captures need to flow not just to projections that look up matches, but optionally into property values themselves (`color: var(@severity)`). Out of scope for v1.0 unless a strong driver appears.

5. **Stylesheet imports** — `@import` would be useful. Defer to v1.1.
