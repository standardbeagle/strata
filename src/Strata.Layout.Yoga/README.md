# StandardBeagle.Strata.Layout.Yoga

A [Yoga](https://www.yogalayout.dev/)-backed flexbox/grid layout pass for
[Strata](https://github.com/standardbeagle/strata) (via the pure-managed
[Yoga.Net](https://www.nuget.org/packages/Yoga.Net) port). Maps computed style onto a parallel
Yoga tree and computes terminal-cell rects, so `display: flex` / `display: grid` lay out against
character cells.

```bash
dotnet add package StandardBeagle.Strata.Layout.Yoga --prerelease
```

```csharp
using Strata.Layout.Yoga;

// After a cascade, build a parallel Yoga tree and compute rects:
LayoutResult layout = new YogaLayoutPass().Compute(root, cascadeResult);
// rects feed a layout-aware projection (e.g. the Spectre or Terminal.Gui renderer)
```

Yoga returns float rects; the pass rounds to whole cells for consistent alignment.
See the [docs](https://github.com/standardbeagle/strata/tree/main/docs).
