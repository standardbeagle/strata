# StandardBeagle.Strata.JsonPath

A [JSONPath (RFC 9535)](https://www.rfc-editor.org/rfc/rfc9535.html) selector language for
[Strata](https://github.com/standardbeagle/strata), backed by
[JsonPath.Net](https://www.nuget.org/packages/JsonPath.Net). It proves Strata's selector
languages are pluggable: the same cascade engine, a different selector syntax.

```bash
dotnet add package StandardBeagle.Strata.JsonPath --prerelease
```

```csharp
using Strata.JsonPath;

var language = new JsonPathSelectorLanguage();
ISelector sel = language.Parse("$.users[?(@.role == 'admin')]");
// hand `sel` to the cascade / use sel.Find(root); captures surface in MatchContext
```

Wildcards, slices, and filters bind their matched node and path key into `MatchContext.Captures`
for projections to read. Specificity mapping for JSONPath is documented in
[docs/02-spec.md §3.2](https://github.com/standardbeagle/strata/blob/main/docs/02-spec.md).
