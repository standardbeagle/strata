# Changelog

All notable changes to Strata are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Packages are published to NuGet under the `StandardBeagle.Strata.*` prefix; the version is
derived from git tags via [MinVer](https://github.com/adamralph/minver) (tag prefix `v`).

## [Unreleased]

### Added

- **Native widgets & overlay layout** — kind-aware projection of interactive controls and
  floating surfaces, in both renderers:
  - **Button** — a node of kind `Button` projects to a native control: a Terminal.Gui `Button`
    (focusable, raises `Accept` on Enter/Space) and, in Spectre, bracket chrome (`[ Label ]`).
    Its `:focused` styling still cascades.
  - **Popup / modal / dialog** — a node of kind `Dialog` / `Modal` / `Popup` projects to a
    bordered, titled, centered surface: a modal Terminal.Gui `Window`, or a Spectre `Panel`.
    The title comes from a `Title` attribute, falling back to the node kind.
  - **`z-index` property** (`LayoutProperties.ZIndex`) — paint/stacking order for overlapping
    absolutely-positioned and overlay siblings; higher paints frontmost. Both projections
    composite by ascending z-index (stable, so equal z keeps document order).
  - **Absolute positioning in Terminal.Gui** — `position: absolute` children are placed at their
    `top` / `left` insets (and `width` / `height` when given) instead of vertical-stacking, so the
    full-screen projection supports floating layout, not only a vertical stack.
- **`ICascadeResult.TryGetComputed`** — non-throwing computed-value lookup, letting a projection
  read optional layout properties (`position`, `z-index`, insets) without requiring the host to
  have registered the layout descriptor set.

## [0.1.0-alpha.1] — first prerelease

The initial public prerelease: the selector → cascade → projection engine, proven end-to-end
through Phases 0–9 (11 packages).

### Added

- **Abstractions** — `ITreeNode` / `ITreeAdapter<TSource>`, `ISelector` / `ISelectorLanguage`,
  `Specificity`, `IRule` / `IStylesheet`, `IPropertyDescriptor` / `IPropertyValue` /
  `IPropertyRegistry`, `ICascade` / `ICascadeResult`, `IProjection<TOutput>`, and the
  `TreeChange` mutation model. No third-party dependencies. (Phase 0)
- **Tree adapters** — `Strata.Adapters.PSObject` (PowerShell `PSObject`, with class/id/
  pseudo-state selector hooks) and `Strata.Adapters.JsonNode` (`System.Text.Json` `JsonNode`),
  validated by a shared selector-equivalence test. (Phase 0)
- **CSS selector language** (`Strata.Css`) — type, universal, id, class, the five attribute
  operators, the four combinators, selector lists, and pseudo-classes `:not` / `:is` /
  `:where` / `:has` / `:nth-child` / `:focused` / `:selected` / `:hovered` / `:expanded` /
  `:root` / `:empty` / `:first-child` / `:last-child` / `:only-child`, plus a custom
  `IPseudoClassRegistry`. Specificity computed per spec. (Phase 1)
- **Typed `[expr]` predicates** — a hand-written, AOT-clean predicate DSL, with reflection-based
  evaluation gated behind the opt-in `CssPredicates.Enable()`. (Phase 1)
- **Cascade engine** (`Strata.Core`) — matched-rule resolution by importance, then specificity,
  then source order; iterative inheritance lookup; initial-value fallback; origin diagnostics;
  and stylesheet hot-reload on `Version` change. (Phase 2)
- **Built-in styling properties** (`Strata.Properties.Styling`) — `color`, `background`,
  `font-weight`, `font-style`, `text-decoration`, `wrap`, `overflow`, `padding`, `margin`,
  via `StylingProperties.RegisterAll` / `CreateRegistry`. (Phase 3)
- **Spectre.Console projection** (`Strata.Render.Spectre`) — `SpectreProjection :
  IProjection<IRenderable>`, AOT-verified end-to-end. (Phase 3)
- **Yoga layout** (`Strata.Layout.Yoga`) — `YogaLayoutPass` maps computed style onto a parallel
  Yoga tree (Yoga.Net 3.2, flex + grid) and computes terminal-cell rects. (Phase 4)
- **Interactions** (`Strata.Interaction`) — selector-bound model: the `command:` property,
  `IInputSource` / `HostEvent`, `ICommandRegistry`, focus/selection controllers toggling
  `:focused` / `:selected`, and a subscription-diff dispatcher over System.Reactive. (Phases 5–6)
- **Terminal.Gui v2 projection** (`Strata.Render.TerminalGui`) — `TerminalGuiProjection :
  IProjection<View>` with React-style reconciliation across cascade runs, plus
  `TerminalGuiInputSource`. (Phase 7)
- **JSONPath selector language** (`Strata.JsonPath`) — `JsonPathSelectorLanguage` (RFC 9535 via
  JsonPath.Net), proving pluggable selector languages over the one cascade engine. (Phase 9)
- A `text-align` styling property with Spectre grid column alignment.
- Libraries multi-target `net8.0;net10.0`; everything is Native-AOT/trim clean by default.
  Terminal.Gui pulls System.Text.Json 8.0.4 transitively; pinned up to the patched 8.0.5
  (advisory GHSA-8g4q-xg66-9fp4).

### Changed

- The `[expr]` predicate evaluator briefly used **System.Linq.Dynamic.Core**, then replaced it
  with a hand-written, AOT-clean DSL (P1.7b). **ExCSS** — the originally planned tokenizer —
  was never adopted; the selector/stylesheet parser is hand-written. Neither package is
  referenced by any project.

[Unreleased]: https://github.com/standardbeagle/strata/compare/v0.1.0-alpha.1...HEAD
[0.1.0-alpha.1]: https://github.com/standardbeagle/strata/releases/tag/v0.1.0-alpha.1
