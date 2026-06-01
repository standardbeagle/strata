# StandardBeagle.Strata.Render.Spectre

A [Spectre.Console](https://spectreconsole.net/) projection for
[Strata](https://github.com/standardbeagle/strata): turns a styled tree into an `IRenderable`
for inline console output.

```bash
dotnet add package StandardBeagle.Strata.Render.Spectre --prerelease
```

End-to-end: adapter → cascade → projection.

```csharp
using Strata.Core;
using Strata.Css;
using Strata.Properties.Styling;
using Strata.Render.Spectre;

var registry = StylingProperties.CreateRegistry();
var sheet    = new CssStylesheetParser(new CssSelectorLanguage(), registry).Parse(cssText);
var result   = new Cascade(registry).Compute(root, sheet);

IRenderable view = new SpectreProjection().Project(root, result);
AnsiConsole.Write(view);
```

`root` is any `ITreeNode` — wrap your data with `StandardBeagle.Strata.Adapters.JsonNode`,
`StandardBeagle.Strata.Adapters.PSObject`, or your own adapter.
See the [docs](https://github.com/standardbeagle/strata/tree/main/docs).
