# Active plan — Phase 4 Yoga layout (task CbtIu8VdrXE2, iteration 1)

## Risk resolved at intake
- `Directory.Packages.props` pinned `Facebook.Yoga 2.0.1` which does NOT exist on nuget.org (max 1.5.0-pre1).
- Correct package is `Yoga.Net 3.2.3` by Chenrensong (matches plan's "Yoga.Net / chenrensong").
- Verified: pure-managed (no native runtimes), multi-targets net8.0/net9.0/net10.0 (matches our TFMs), `Display.Grid` present (grid 3.2+ confirmed), namespace is `Facebook.Yoga`.
- Smoke-verified flex split (row, flex-grow:1 each, 30 wide → 15/15) and grid template columns compute.
- API gotcha: `CalculateLayout` clones children (CoW). Read rects by index-walking from root (`root.GetChild(i)`), not via original `Node` refs. So `YogaLayoutPass` zips ITreeNode children with `parentYoga.GetChild(i)` in build order.

## Plan adjustment 1 (Phase 4 risk realized): Yoga.Net grid is non-functional
- Smoke-tested Yoga.Net 3.2.3 grid extensively: `Display.Grid` + grid templates + explicit
  GridColumnStart/RowStart placement ALL ignored by CalculateLayout. Children land at (0,0)
  with full container width and zero height. The grid API surface exists but the layout
  algorithm does not implement it in this port.
- docs/04-plan.md §Phase 4 risk explicitly authorizes the fallback:
  "If not, fall back to flex-only for v1.0 and document grid as v1.1."
- DECISION: implement `display: grid` + `grid-template-columns/rows` via flexbox emulation in
  YogaLayoutPass (wrap children into flex rows of N columns; column track sizes drive cell
  widths, row track sizes drive row heights). This delivers a REAL multi-column terminal
  layout (what the dashboard demo + acceptance "multi-column grid" need) without depending on
  the broken native grid. Document the substitution in code + tech-design. Real flex grid is
  still v1.1 once the port matures. Grid placement test asserts the emulated offsets.

## Slices (coherent chunks)
1. Packages: Facebook.Yoga 2.0.1 → Yoga.Net 3.2.3.
2. Layout properties in Strata.Properties.Styling: display, flex-direction, flex-grow/shrink/basis,
   align-items, justify-content, width, height, min/max-width/height, position, top/right/bottom/left,
   gap/row-gap/column-gap, grid-template-columns, grid-template-rows. (None exist yet.)
3. Strata.Layout.Yoga: Rect, Size value types; LayoutResult; YogaLayoutPass (mapping per §4.1,
   parallel tree, index-walk rect recovery, integer cell rounding, Trivial flag).
4. SpectreProjection: optional LayoutResult; Grid for display:grid, Canvas for absolute position.
5. Dashboard demo: multi-column grid stylesheet over the process list.
6. Tests: Strata.Layout.Yoga.Tests (flex split, grid placement, cell rounding, absolute, trivial,
   skip-layout) + a SpectreProjection grid/layout test.
7. Solution wiring (sln add for new src + test projects).

## Acceptance
- All Phase 4 deliverables present; mapping matches §4.1; LayoutPass computes rects;
  SpectreProjection honors them; dashboard renders Get-Process as multi-column grid;
  tests green; build clean net8.0;net10.0.
