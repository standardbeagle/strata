# StandardBeagle.Strata.Css

The CSS subset selector language for [Strata](https://github.com/standardbeagle/strata):
type, id, class, attribute (5 operators), the four combinators, and pseudo-classes
(`:not` `:is` `:where` `:has` `:nth-child` and friends). AOT- and trim-clean by default.

```bash
dotnet add package StandardBeagle.Strata.Css --prerelease
```

```csharp
using Strata.Css;

var language = new CssSelectorLanguage();
ISelector sel = language.Parse("Window > Process.highlighted:focused");

// Parse a whole stylesheet against a property registry:
var sheet = new CssStylesheetParser(language, registry).Parse(cssText);
```

Typed `[expr]` predicates (e.g. `Process[CPU > 50 and Name.StartsWith("chr")]`) use reflection
and are **opt-in** via `CssPredicates.Enable()` — keeping the common path AOT-safe.
Selector grammar is documented in
[docs/02-spec.md §3.1](https://github.com/standardbeagle/strata/blob/main/docs/02-spec.md).
