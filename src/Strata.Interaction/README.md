# StandardBeagle.Strata.Interaction

The selector-bound interaction layer for [Strata](https://github.com/standardbeagle/strata): the
`command:` property, `IInputSource` / `HostEvent`, `ICommandRegistry`, and a subscription-diff
dispatcher that wires stylesheet-declared commands to [System.Reactive](https://github.com/dotnet/reactive)
event streams as the cascade changes.

```bash
dotnet add package StandardBeagle.Strata.Interaction --prerelease
```

```csharp
using Strata.Interaction;

// Register the command: property descriptor so stylesheets can declare commands,
// then run an interactive session over an input source.
registry.Register(new CommandPropertyDescriptor());

var commands = new CommandRegistry();
using var session = new InteractiveSession(/* host, input source, controllers */);
// FocusController + SelectionController toggle :focused / :selected pseudo-states;
// the dispatcher (re)binds keys to commands as the resolved `command:` values change.
```

Cascade-declared `command:` values attach/detach handlers additively as nodes match.
See [docs/05-interaction-redesign.md](https://github.com/standardbeagle/strata/blob/main/docs/05-interaction-redesign.md).
