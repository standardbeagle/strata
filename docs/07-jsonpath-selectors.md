# JSONPath Selector Language

**Status:** Phase 9
**Scope:** `Strata.JsonPath`, the `Strata.Demo.RouterProjection` sample.

This document explains the JSONPath (RFC 9535) selector language for Strata: how it bridges to
the engine, the deliberately-chosen specificity mapping, and the accepted edge cases when mixing
CSS and JSONPath selectors in one stylesheet.

JSONPath is a *second* selector language for the same cascade engine. It proves the
`ISelectorLanguage` / `ISelector` contracts (`src/Strata.Abstractions/Selector.cs`) are
language-agnostic: the engine never references a concrete language, and a new one ships as its own
package.

## 1. What it is

`Strata.JsonPath` exposes:

- `JsonPathSelectorLanguage : ISelectorLanguage` — `Name == "jsonpath"`, `Parse(source)` compiles
  one JSONPath into an `ISelector`. Invalid input throws `FormatException` (per the contract:
  invalid input throws rather than producing a never-matching selector).
- `JsonPathSelector : ISelector` — `Matches`, `Find`, and `Specificity` over `ITreeNode`.

It is backed by [JsonPath.Net](https://www.nuget.org/packages/JsonPath.Net) 1.1.6, which
implements RFC 9535.

## 2. The bridge: `ITreeNode.Underlying`

JsonPath.Net evaluates against `System.Text.Json.Nodes.JsonNode`. Strata selectors operate over
`ITreeNode`. The bridge is the `ITreeNode.Underlying` contract member alone — **`Strata.JsonPath`
does not depend on `Strata.Adapters.JsonNode`** (or any adapter):

1. `Find(root)` reads `root.Underlying as JsonNode`, evaluates the path against it.
2. Each result `Node.Value` (a `JsonNode`) is mapped back to the `ITreeNode` whose `Underlying` is
   that same `JsonNode` instance (reference identity), by walking the tree from `root`.
3. `Matches(node)` walks up to the document root, evaluates the path against the whole document
   (JSONPath is always rooted at `$`), then tests whether `node` is in the result set.

Any adapter whose nodes wrap a `JsonNode` (the shipped `JsonTreeAdapter`, or a custom one) works
unchanged. A node whose `Underlying` is not a `JsonNode` never matches.

## 3. Captures

Wildcards, slices, and filters produce multiple matches; each match exposes *which* slice matched
via `MatchContext.Captures`:

- `"location"` — the RFC 9535 normalized path of the matched node, e.g. `$['users'][0]`.
- `"$0"`, `"$1"`, … — the addressable name/index segments of that location in document order. For
  `$['users'][0]` these are `"users"` (string) and `0` (int).

Router projections read these captures to recover the slice address that triggered a route — see
the sample below.

## 4. Specificity (arbitrary by design)

JSONPath has no native specificity. The mapping below is **deliberately chosen and documented**
(resolves `docs/03-tech-design.md` §12 Q1; the `docs/04-plan.md` §Phase 9 risk). It mirrors CSS
intuition by mapping selector kinds onto the `(A, B, C)` triple:

| Axis | JSONPath selector kind | CSS analogue |
|---|---|---|
| **A** | name selectors (`.foo`, `['foo']`) and index selectors (`[0]`) | id (`#id`) |
| **B** | filter selectors (`[?…]`) | attribute / pseudo-class |
| **C** | wildcards (`*`) and slices (`[a:b]`) | type |

So `$.users.settings` → `(2,0,0)`, `$.users[?@.role == 'admin']` → `(1,1,0)`, and `$.users[*]` →
`(1,0,1)`. More named steps outrank a wildcard, as a CSS author would expect. Recursive descent
(`..`) contributes through whatever selectors its segments carry.

## 5. Mixing CSS and JSONPath — accepted edge cases

CSS and JSONPath compute specificity on **different axes over different addressing models**. A
stylesheet that mixes both languages can therefore produce cascade orderings that surprise either
mental model. These are accepted edge cases, not bugs:

- A JSONPath filter (`B`) and a CSS attribute selector (`B`) both land on the `B` axis, but the
  JSONPath filter can express predicates (`@.cpu > 80`) a CSS attribute selector cannot. Two rules
  that "feel" equally specific may have been authored with very different selectivity.
- A JSONPath name step counts as `A` (id-like), so `$.users` can outrank a CSS class selector
  (`.user`, which is `B`) even though intuitively a class feels more specific than a bare property
  name. Authors mixing the two should lean on explicit ordering or `!important`-equivalent
  mechanisms rather than relying on cross-language specificity comparison.
- JSONPath addresses *positions* (`[0]`, `[0:2]`); CSS addresses *kinds* (`Process`, `.high-cpu`).
  A position-addressed rule and a kind-addressed rule can both match the same node with no
  meaningful "more specific" relationship between them.

**Guidance:** within a single language, specificity behaves predictably. Across languages, prefer
authoring rules so that the intended winner is unambiguous on its own axis, and treat
cross-language specificity ties as undefined-but-deterministic (the cascade still resolves them
deterministically by source order, it just may not match a CSS author's intuition).

## 6. RFC 9535 vs legacy filter syntax

The plan's example uses the legacy Goessner filter form `$.users[?(@.role == 'admin')]` (with
parentheses). RFC 9535 drops the parentheses: `$.users[?@.role == 'admin']`.

JsonPath.Net 1.1.6 **accepts both forms and produces identical results**, so the plan's example
parses unchanged. Strata's docs, tests, and sample prefer the RFC 9535 form. No syntax shim is
needed.

## 7. Success criterion

From `docs/04-plan.md` §Phase 9: `$.users[?@.role == 'admin']` matches the same logical nodes as
the CSS selector for the same concept. JSON nodes carry no CSS classes, so the canonical `.user`
class maps to the node `Kind` (sourced from `$type`). Over a shared state tree, the JSONPath
selector and the CSS selector `user[role="admin"]` return the **identical set of `ITreeNode`
references**. This equivalence is asserted by
`tests/Strata.JsonPath.Tests/JsonPathSelectorTests.cs`.

## 8. Sample: router projection

`samples/Strata.Demo.RouterProjection` maps state-tree slices to handler descriptors. The same
`ISelector` contract that drives the CSS cascade drives a routing table instead:

```csharp
var router = new RouterProjection(new[]
{
    new Route("$.users[?@.role == 'admin']", "admin-console"),
    new Route("$.users[?@.role == 'user']",  "user-home"),
    new Route("$.notifications[?@.kind == 'error']", "alert-banner"),
});

foreach (var d in router.Project(root))
    Console.WriteLine($"{d.Location} -> {d.Handler} (slice: {string.Join('.', d.Captures)})");
```

Output:

```
$['users'][0]          -> admin-console  (slice: users.0)
$['users'][2]          -> admin-console  (slice: users.2)
$['users'][1]          -> user-home      (slice: users.1)
$['notifications'][0]  -> alert-banner   (slice: notifications.0)
```

The projection only *describes* routing — it never invokes a handler. That is the consumer's job.
