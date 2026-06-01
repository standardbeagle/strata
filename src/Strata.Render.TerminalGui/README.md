# StandardBeagle.Strata.Render.TerminalGui

A [Terminal.Gui v2](https://github.com/gui-cs/Terminal.Gui) projection for
[Strata](https://github.com/standardbeagle/strata): reconciles a styled tree into a full-screen
`View` tree, sharing the cascade engine with the Spectre projection.

```bash
dotnet add package StandardBeagle.Strata.Render.TerminalGui --prerelease
```

```csharp
using Strata.Render.TerminalGui;

using var projection = new TerminalGuiProjection();
View view = projection.Project(root, cascadeResult);   // IProjection<View>

// pair with TerminalGuiInputSource to drive focus/selection + command dispatch
using var input = new TerminalGuiInputSource();
```

The projection reconciles across cascade runs (React-style diff) so focus and selection survive
re-cascades. See the [docs](https://github.com/standardbeagle/strata/tree/main/docs).
