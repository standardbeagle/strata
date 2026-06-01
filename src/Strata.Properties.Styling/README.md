# StandardBeagle.Strata.Properties.Styling

The common styling property descriptors for [Strata](https://github.com/standardbeagle/strata):
`color`, `background`, `font-weight`, `font-style`, `text-decoration`, `wrap`, `overflow`,
`padding`, `margin`. Depends only on `StandardBeagle.Strata.Core`.

```bash
dotnet add package StandardBeagle.Strata.Properties.Styling --prerelease
```

```csharp
using Strata.Properties.Styling;

// One call registers every built-in descriptor:
IPropertyRegistry registry = StylingProperties.CreateRegistry();
// or add to an existing registry:
StylingProperties.RegisterAll(registry);
```

Feed the registry to `Cascade` and a `CssStylesheetParser`. Pair with a projection such as
`StandardBeagle.Strata.Render.Spectre` to turn computed values into rendered output.
See the [docs](https://github.com/standardbeagle/strata/tree/main/docs).
