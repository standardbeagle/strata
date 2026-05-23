# Active plan — Phase 5 Interactions (task 4SrLff53j5D6, iteration 2)

## Intent conflict resolved at intake (push-back, not blind execution)
- Dart task body "Phase 5 — Behaviors" (created 2026-05-13) describes the imperative
  IBehavior / IBehaviorContext / BehaviorHost design with MS.Ext.DI keyed-service resolution.
- That design is EXPLICITLY SUPERSEDED by `docs/05-interaction-redesign.md` (dated 2026-05-16,
  "supersedes 04-plan.md Phase 5 + Phase 6"), reinforced by supersession banners in
  `docs/04-plan.md` §Phase 5 ("Phase 5 now ships the `command:` property, IInputSource,
  ICommandRegistry, the subscription-diff dispatcher"), `docs/03-tech-design.md` §6,
  `docs/02-spec.md` §6, `docs/01-requirements.md` FR-12, the decision log, and recent commits
  (`replace Phase 5 behavior model with selector-bound observables`).
- DECISION: implement the REDESIGNED selector-bound interaction scope (the actual project goal
  the active plan points to), NOT the deprecated behavior-lifecycle spec. Implementing the dead
  design would violate the locked decision and burn the AOT trim budget the redesign exists to
  save. Karpathy goal-driven + push-back.
- Recommend the Dart task title/body be updated to "Phase 5 — Interactions" (noted in completion
  comment; the task body is stale relative to the docs).

## What was built (Strata.Interaction package)
1. `command:` property — list-valued, ADDITIVE cascade semantics (documented deviation from CSS
   override; canonical in redesign §2.3 / tech-design §12 Q2). Syntax `command: "name" when "event"`,
   comma-separated items = multiple bindings. `CommandValue` / `CommandPropertyDescriptor`.
2. `HostEvent` (Key/Focus/Tick/Custom records) + `IInputSource`; `InputSource` backed by a
   System.Reactive `Subject<HostEvent>`.
3. `ICommandRegistry` / `CommandRegistry` — command-name → handler delegate, single registration
   per name (second-to-register throws clearly). Replaces DI keyed services. No reflection.
4. `InteractionHost` — the subscription-diff dispatcher (replaces Attach/Detach). Per cascade run,
   collects the additive `(command, event)` set per node from `GetMatchedRules` (NOT the single
   cascade winner), diffs keyed by `(node, command, event)`: appear → subscribe, disappear →
   dispose, present-in-both → untouched (identity stable across re-cascade, no re-fire).
   Detach-before-re-attach ordering: disposes happen before any later reconcile's adds.
5. Sample handlers (redesign equivalents of the old sample behaviors): navigate-down/up keymap
   (Highlight role), kill-with-confirmation (KillProcessConfirm), render-sparkline ring buffer
   (ResourceMeter). `SparklineBuffer` ring buffer.
6. Tests: `Strata.Interaction.Tests` — parse, registry, end-to-end CSS→cascade→dispatch, additive
   semantics, subscription identity stability, detach-on-removal, descendant collection, samples.

## Integration decision (no core cascade change)
- Additive merge lives in the interaction layer via `ICascadeResult.GetMatchedRules(node)`. The
  core cascade keeps single-winner-per-property semantics untouched; `command:` is never read via
  `GetComputed`. Clean, minimal — no modification to Strata.Core.

## Acceptance
- All redesigned Phase 5 deliverables present; `command:` parses additive ident/event pairs;
  subscription-diff lifecycle correct; identity stable across re-cascade; ICommandRegistry single
  registration; sample handlers implemented + tested. 183 tests green (+28). Clean net8.0;net10.0
  build, Release AOT-analyzers clean (0 warnings, warnings-as-errors).
