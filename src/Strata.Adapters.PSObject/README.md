# StandardBeagle.Strata.Adapters.PSObject

Wraps PowerShell `PSObject` instances as a [Strata](https://github.com/standardbeagle/strata)
tree, so cascade and projections operate over PowerShell pipeline output. Cmdlet-author
friendly: compiles against `PowerShellStandard.Library`.

```bash
dotnet add package StandardBeagle.Strata.Adapters.PSObject --prerelease
```

```csharp
using Strata.Adapters.PSObject;

var adapter = new PsObjectTreeAdapter();          // sensible defaults; all hooks optional
ITreeNode node = adapter.Wrap(psObject);

// Map properties to selector hooks (class/id/kind/pseudo-state) when you need them:
var configured = PsObjectTreeAdapter.Create(new PsObjectTreeAdapter.Options
{
    Classes = PsObjectTreeAdapter.ClassesFromProperty("Status"),
});
```

By default `Kind` comes from the object's type name and `Id` from its `Id`/`Name` property.
See the [docs](https://github.com/standardbeagle/strata/tree/main/docs).
