# Strata — Specification

**Status:** Draft v0.1
**Scope:** Public API contracts, selector grammars, cascade algorithm, file formats

This document defines the interfaces a v1.0 Strata implementation MUST satisfy. Tech design (algorithms, internal data structures) is in `03-tech-design.md`. Phasing is in `04-plan.md`.

## 1. Package layout

| Package | Purpose | Depends on |
|---|---|---|
| `Strata.Abstractions` | Interfaces only | — |
| `Strata.Core` | Cascade engine, registries, primitives | Abstractions |
| `Strata.Css` | CSS selector language | Core (ExCSS and System.Linq.Dynamic.Core were dropped — see note) |
| `Strata.JsonPath` | JSONPath selector language | Core, JsonPath.Net |
| `Strata.Behaviors` | Behavior lifecycle | Core, MS.Extensions.DI.Abstractions, System.Reactive |
| `Strata.Properties.Styling` | Common styling property descriptors | Core |
| `Strata.Layout.Yoga` | Yoga-backed layout pass | Core, Properties.Styling, Yoga.Net |
| `Strata.Render.Spectre` | Spectre.Console projection | Core, Layout.Yoga, Spectre.Console |
| `Strata.Render.TerminalGui` | Terminal.Gui v2 projection | Core, Layout.Yoga, Terminal.Gui |
| `Strata.Adapters.PSObject` | PSObject tree adapter | Abstractions, PowerShellStandard.Library |
| `Strata.Adapters.JsonNode` | JsonNode tree adapter | Abstractions |
| `Strata.Diagnostics` | Inspection, dumps, ETW | Core |

`Strata.Abstractions` and `Strata.Core` have **no third-party dependencies**.

> **Superseded (Phase 1):** `Strata.Css` no longer depends on **ExCSS** or
> **System.Linq.Dynamic.Core**. Tokenization, the selector/stylesheet parser, and the
> `[expr]` typed-predicate DSL are all hand-written and AOT-clean; predicate *evaluation*
> is gated behind the opt-in `CssPredicates.Enable()` reflection hook. The §3.1 typed-predicate
> prose below that still references Dynamic.Core describes the original design only.

## 2. Core abstractions

### 2.1 Tree

```csharp
namespace Strata;

public interface ITreeNode
{
    /// <summary>Type-like identity, analogous to CSS element name. Required.</summary>
    string Kind { get; }

    /// <summary>Optional unique identifier within the tree.</summary>
    string? Id { get; }

    /// <summary>Class labels for this node.</summary>
    IReadOnlySet<string> Classes { get; }

    /// <summary>Dynamic pseudo-states currently active on this node.</summary>
    IReadOnlySet<string> PseudoStates { get; }

    /// <summary>Parent node, or null at root.</summary>
    ITreeNode? Parent { get; }

    /// <summary>Ordered children. May be empty.</summary>
    IEnumerable<ITreeNode> Children { get; }

    /// <summary>Read a named attribute. Returns false if absent.</summary>
    bool TryGetAttribute(string name, out object? value);

    /// <summary>Original underlying object. Opaque to the engine.</summary>
    object? Underlying { get; }
}

public interface ITreeAdapter<TSource>
{
    ITreeNode Wrap(TSource source);
}
```

#### Identity semantics

Two `ITreeNode` instances representing the same logical node MUST compare equal via `Equals` and MUST produce the same `GetHashCode`. Adapters SHOULD return cached wrappers rather than fresh ones.

#### Pseudo-state mutation

Pseudo-states are mutable at runtime via an adapter-specific mechanism. The adapter is responsible for raising `INodeMutationSource` events (see 2.6) when a state toggles.

### 2.2 Selectors

```csharp
public interface ISelectorLanguage
{
    string Name { get; }
    ISelector Parse(string source);
}

public interface ISelector
{
    Specificity Specificity { get; }

    /// <summary>Does this selector match the given node?</summary>
    bool Matches(ITreeNode node, out MatchContext context);

    /// <summary>Enumerate all nodes in the tree rooted at root that match.</summary>
    IEnumerable<Match> Find(ITreeNode root);
}

public readonly record struct Specificity(int A, int B, int C) : IComparable<Specificity>
{
    public static Specificity Zero => default;
    public static Specificity operator +(Specificity x, Specificity y)
        => new(x.A + y.A, x.B + y.B, x.C + y.C);
}

public readonly record struct Match(ITreeNode Node, MatchContext Context);

public readonly struct MatchContext
{
    public IReadOnlyDictionary<string, object?> Captures { get; init; }
    public static MatchContext Empty { get; } = new() { Captures = EmptyDict.Instance };
}
```

Specificity is a 3-tuple ordered lexicographically. JSONPath specificity is defined in §3.2.

### 2.3 Rules and stylesheets

```csharp
public interface IRule
{
    ISelector Selector { get; }
    IReadOnlyList<Declaration> Declarations { get; }
    int SourceOrder { get; }
}

public readonly record struct Declaration(
    string Property,
    IPropertyValue Value,
    bool Important);

public interface IStylesheet
{
    IReadOnlyList<IRule> Rules { get; }
    /// <summary>Monotonically increasing version. Increments on any edit.</summary>
    int Version { get; }
}
```

### 2.4 Properties

```csharp
public interface IPropertyDescriptor
{
    string Name { get; }
    Type ValueType { get; }
    bool Inherits { get; }
    IPropertyValue Initial { get; }

    /// <summary>Parse a textual value into the typed representation.</summary>
    IPropertyValue Parse(ReadOnlySpan<char> source);
}

public interface IPropertyValue
{
    Type Type { get; }
}

public interface IPropertyRegistry
{
    void Register(IPropertyDescriptor descriptor);
    bool TryGet(string name, out IPropertyDescriptor descriptor);
    IEnumerable<IPropertyDescriptor> All { get; }
}
```

Property values MUST be either value types or interned reference types. Allocating per-cascade is forbidden for built-in property types.

### 2.5 Cascade

```csharp
public interface ICascade
{
    /// <summary>Initial computation against a tree and stylesheet.</summary>
    ICascadeResult Compute(ITreeNode root, IStylesheet stylesheet);

    /// <summary>Incremental update given a prior result and changes.</summary>
    ICascadeResult Update(
        ICascadeResult prior,
        IReadOnlyList<TreeChange> treeChanges,
        IStylesheet? newStylesheet = null);
}

public interface ICascadeResult
{
    int StylesheetVersion { get; }

    /// <summary>Resolved value for a property, including inheritance lookup.</summary>
    TValue GetComputed<TValue>(ITreeNode node, string property)
        where TValue : IPropertyValue;

    /// <summary>All rules that matched a node, ordered by precedence (winner first).</summary>
    IReadOnlyList<RuleApplication> GetMatchedRules(ITreeNode node);

    /// <summary>For diagnostics: explain why a property has its current value.</summary>
    PropertyOrigin GetOrigin(ITreeNode node, string property);
}

public readonly record struct RuleApplication(IRule Rule, MatchContext Context);

public readonly record struct PropertyOrigin(
    OriginKind Kind,           // Declared, Inherited, Initial
    IRule? Rule,               // when Declared
    ITreeNode? InheritedFrom); // when Inherited
```

### 2.6 Projections

```csharp
public interface IProjection<TOutput>
{
    TOutput Project(ITreeNode root, ICascadeResult cascade);
}
```

A projection is pure with respect to its inputs: given the same `(root, cascade)` pair, two calls MUST produce equivalent output. Side effects (e.g. attaching behaviors) are the responsibility of behavior attachment, not projection.

### 2.7 Tree mutation events (incremental updates)

```csharp
public interface INodeMutationSource
{
    IObservable<TreeChange> Changes { get; }
}

public abstract record TreeChange(ITreeNode Node)
{
    public sealed record Inserted(ITreeNode Node, ITreeNode? PreviousSibling)
        : TreeChange(Node);
    public sealed record Removed(ITreeNode Node) : TreeChange(Node);
    public sealed record ClassChanged(ITreeNode Node, string Class, bool Added)
        : TreeChange(Node);
    public sealed record PseudoStateChanged(ITreeNode Node, string State, bool Added)
        : TreeChange(Node);
    public sealed record AttributeChanged(ITreeNode Node, string Attribute)
        : TreeChange(Node);
}
```

Adapters that want incremental cascade implement `INodeMutationSource`. Adapters that don't are statically re-cascaded.

## 3. Selector languages

### 3.1 CSS subset (`Strata.Css`)

The CSS selector grammar is a subset of [Selectors Level 4](https://drafts.csswg.org/selectors-4/) with one addition (typed predicates) and several omissions.

#### Supported

| Form | Example | Notes |
|---|---|---|
| Type | `Process` | Matches `Kind == "Process"`. Case-sensitive. |
| Universal | `*` | Matches any kind. |
| Id | `#chrome-1234` | Matches `Id == "chrome-1234"`. |
| Class | `.zombie` | Matches `Classes.Contains("zombie")`. |
| Attribute equal | `[Name="chrome"]` | String equality. |
| Attribute starts | `[Name^="chr"]` | |
| Attribute ends | `[Name$="me"]` | |
| Attribute contains | `[Name*="rom"]` | |
| Attribute exists | `[CPU]` | Attribute is present (any value). |
| **Typed predicate** | `[CPU > 50 and Name.StartsWith("chr")]` | See below. |
| Pseudo-class | `:focused`, `:not(...)`, `:has(...)`, `:nth-child(n)` | Registry-extensible. |
| Descendant combinator | `Window Process` | Space. |
| Child combinator | `Window > Process` | |
| Adjacent sibling | `Header + Row` | |
| General sibling | `Header ~ Row` | |
| Selector list | `Process, Thread` | Comma-separated. Each contributes its own match. |

#### Typed predicate extension

The `[expr]` form is overloaded. If the expression contains an operator other than the four string operators above (`=`, `^=`, `$=`, `*=`), or contains identifiers other than the attribute name on the left, it is parsed as a **predicate expression** and compiled via `System.Linq.Dynamic.Core` against the underlying object's type at adapter registration time.

```css
Process[CPU > 50]
Process[Threads.Count > 10 and Name != "System"]
File[Length > 1024 * 1024]
```

The expression's free identifiers are resolved against the `Underlying` object's properties. Type info comes from the adapter (which declares the source type at registration). If `Underlying` is dynamically typed (e.g. `PSObject`), property access goes through `PSObject.Properties`.

Compilation happens at stylesheet parse time. A compiled predicate is cached per `(selector, expected type)` pair.

#### Pseudo-classes

Built-in:

- `:focused` — `PseudoStates.Contains("focused")`
- `:selected` — `PseudoStates.Contains("selected")`
- `:hovered` — `PseudoStates.Contains("hovered")`
- `:root` — `Parent == null`
- `:empty` — `!Children.Any()`
- `:first-child`, `:last-child`, `:only-child`
- `:nth-child(an+b)`
- `:not(simple-selector)` — negation
- `:has(relative-selector)` — descendant existence
- `:is(s1, s2, ...)` — match any of the inner selectors (specificity = max)
- `:where(s1, s2, ...)` — like `:is` but specificity = 0

Custom pseudo-classes are registered through `IPseudoClassRegistry`:

```csharp
public interface IPseudoClassRegistry
{
    void Register(string name, Func<ITreeNode, bool> predicate);
    void RegisterFunctional(
        string name,
        Func<ITreeNode, string?, bool> predicate);
}
```

A functional pseudo-class takes an argument string: `:has-cpu-over("50")`.

#### Not supported (initial release)

- Pseudo-elements (`::before`, `::marker`, etc.)
- `:scope`, `:host`, `:slotted`, `:part`
- `:lang`, `:dir`
- Logical combinators in attribute selectors (`[a=b], [c=d]` inside `[]`)
- Namespace prefixes
- Case-insensitive attribute matching `[attr=value i]` (deferred)

#### Specificity computation

| Component | Increments |
|---|---|
| `#id` | A |
| `.class`, `[attr...]`, `:pseudo-class` | B |
| Type, `*` (universal counts as 0) | C |
| `:not(s)`, `:is(s1,...)` | max of inner |
| `:where(...)` | 0 |
| `:has(s)` | inner specificity |

### 3.2 JSONPath subset (`Strata.JsonPath`)

Based on [RFC 9535](https://www.rfc-editor.org/rfc/rfc9535.html). Implemented via `JsonPath.Net` for parsing.

#### Supported

| Form | Example |
|---|---|
| Root | `$` |
| Child name | `$.users` or `$['users']` |
| Array index | `$[0]`, `$[-1]` |
| Wildcard | `$.users[*]` |
| Slice | `$.items[0:10:2]` |
| Filter | `$.users[?(@.role == 'admin')]` |
| Descendant | `$..tasks` |

#### Captures

Each `*`, slice, or filter binds the matched node and its path key into `MatchContext.Captures`:

```jsonpath
$.routes[?(@.path == @path)]
```

→ `Captures` contains `{ "path": "<actual path>", "$1": <node> }` for use by projections (routing parameter extraction).

#### Specificity mapping

JSONPath has no native specificity. Strata defines:

- A: count of literal child name steps
- B: count of filter expressions and attribute predicates
- C: count of wildcard / slice / descendant steps

Within a tied specificity, JSONPath selectors lose to CSS selectors by convention. This is a stable, documented choice — JSONPath is generally used in single-language stylesheets and the loss only matters if both languages are mixed.

## 4. Cascade algorithm

For a node N and stylesheet S:

1. Compute matched rules: `M = { r ∈ S.Rules | r.Selector.Matches(N) }`.
2. For each declaration `d` across all `r ∈ M`, group by `d.Property`.
3. For each property group, select the winning declaration:
   - Important declarations beat non-important.
   - Within importance, higher `Specificity` wins.
   - Within specificity, larger `r.SourceOrder` wins.
4. For inheritable properties with no winner: look up parent's computed value (recursively, up to root). If still none: use `IPropertyDescriptor.Initial`.
5. For non-inheritable properties with no winner: use `IPropertyDescriptor.Initial`.

### 4.1 Right-to-left subject-first matching

Implementations MUST evaluate compound selectors right-to-left (subject-first). The "subject" is the rightmost compound selector in a complex selector. For example, in `Window > Process[CPU > 50]:focused`:

- Subject: `Process[CPU > 50]:focused`
- Combinator: `>`
- Context: `Window`

Matching evaluates the subject first against the candidate node, then walks ancestors/siblings as the combinator demands. This avoids the pathological cost of left-to-right walking on deep trees.

### 4.2 Subject-based rule indexing

The cascade engine MUST maintain a rule index keyed by the subject's primary criterion (in priority order: `Id`, `Kind`, `first Class`). When computing matches for a node, it MUST consult only buckets that could match.

This is the standard browser CSS engine optimization. Without it, every node would test every rule.

### 4.3 Incremental updates

When `TreeChange` events arrive:

| Change | Re-cascade scope |
|---|---|
| `Inserted` | New node + its descendants |
| `Removed` | Drop cached results for removed subtree |
| `ClassChanged` | Node + descendants if any selector uses descendant combinator with this class |
| `PseudoStateChanged` | Node only, plus descendants if inheritable property changes |
| `AttributeChanged` | Node only (Strata has no attribute-driven combinators in v1) |

The cascade engine MUST not re-evaluate rules whose subjects can't be affected by the change. The rule index supports this via change-keyed lookups.

## 5. Stylesheet syntax

### 5.1 File format

Strata uses a CSS-like syntax. Files are UTF-8, `.strata` or `.css` (callers' choice — they're parsed identically).

```
selector-list {
    property: value;
    property: value !important;
    behavior: name1, name2;
}

/* Comments are C-style. */
```

### 5.2 Selector list

Comma-separated selectors share a declaration block. Each contributes its own rule with its own specificity.

```
Process, Thread {
    color: white;
}
```

is equivalent to two separate rules.

### 5.3 Property values

Values are parsed per `IPropertyDescriptor.Parse`. Common forms (defined in `Strata.Properties.Styling`):

| Type | Examples |
|---|---|
| Color | `red`, `#ff0000`, `rgb(255,0,0)`, `rgba(255,0,0,0.5)` |
| Length | `10` (cells), `auto`, `25%`, `1fr` (grid only) |
| Enum | `block`, `flex`, `grid`, `none` |
| Ident list | `kill, meter` (used for `behavior:`) |
| String | `"hello"` |

Whitespace between tokens is insignificant. Property names are case-sensitive (lowercase by convention).

### 5.4 At-rules

Not supported in v1. Reserved for future media-query equivalent.

## 6. Interactions (Phase 5+)

**Superseded — see `docs/05-interaction-redesign.md` for the canonical spec.** The sections below describe the original imperative-lifecycle design; the replacement uses a `command:` property and selector-bound `IObservable<HostEvent>` subscriptions with command-name → handler dispatch. The original text is retained for context only.

### 6.1 Behavior contract

```csharp
public interface IBehavior
{
    void Attach(IBehaviorContext context);
    void Detach();
}

public interface IBehaviorContext
{
    ITreeNode Node { get; }
    IServiceProvider Services { get; }
    IObservable<NodeEvent> Events { get; }
    ICommandRegistry Commands { get; }
    IDisposable Subscribe<T>(Action<T> handler) where T : NodeEvent;
}

public abstract record NodeEvent
{
    public sealed record KeyPress(ConsoleKeyInfo Key) : NodeEvent;
    public sealed record FocusChanged(bool Focused) : NodeEvent;
    public sealed record CommandInvoked(string Command, object? Argument) : NodeEvent;
    public sealed record Tick(TimeSpan Delta) : NodeEvent;
    public sealed record Custom(string Name, object? Payload) : NodeEvent;
}
```

### 6.2 Behavior resolution

Behaviors are resolved by name from a DI container using **Microsoft.Extensions.DependencyInjection keyed services**:

```csharp
services.AddKeyedTransient<IBehavior, KillProcessBehavior>("kill");
services.AddKeyedTransient<IBehavior, ResourceMeterBehavior>("meter");
```

A stylesheet declaration `behavior: kill, meter;` resolves to two attached instances per matching node.

### 6.3 Lifecycle

When a node's `behavior` property cascade result changes:

1. For each name newly present, instantiate via DI, call `Attach`.
2. For each name newly absent, call `Detach`, dispose.
3. Names present in both: leave undisturbed.

A behavior instance is scoped to a `(node, behavior-name)` pair. The same name on two nodes produces two instances.

### 6.4 Commands

Behaviors publish commands during `Attach`:

```csharp
context.Commands.Publish(new Command(
    Name: "kill",
    Binding: KeyBinding.Parse("k"),
    Handler: () => Process.GetProcessById(...).Kill()));
```

Commands are scoped to their publishing behavior and disappear on `Detach`.

## 7. Versioning

- `Strata.Abstractions`: SemVer; breaking changes are major. Frozen after v1.0 except by explicit RFC.
- `Strata.Core`: SemVer; breaking changes are major.
- Implementation packages: SemVer; minor bumps may add property descriptors, projections.
- Selector languages: each is its own package, versioned independently. CSS subset MUST be backward-compatible across minor versions.
- The `.strata` stylesheet format is versioned via `@strata 1.0;` header (planned for v1.1; v1.0 is implicit).

## 8. Diagnostics surface

```csharp
public interface IStrataInspector
{
    /// <summary>Produces a human-readable per-node dump.</summary>
    string Dump(ITreeNode root, ICascadeResult result);

    /// <summary>Explain why a property has its current value.</summary>
    string Explain(ITreeNode node, string property, ICascadeResult result);

    /// <summary>Emit ETW/EventSource events for a cascade run.</summary>
    IDisposable Trace(ICascade cascade);
}
```

The `Dump` and `Explain` outputs are stable enough for golden-file tests in downstream projects.
