# Strata.PowerShell

Author a responsive terminal UI in PowerShell — the TUI equivalent of an HTA app.

```powershell
Import-Module Strata.PowerShell

$layout = Stack -Class 'main' {
    Text 'Ping Monitor' -Class 'h1'
    Card -Class 'host' { Text 'google.com  12ms  ▁▂▃▅▂▁' }
}
$layout | Show-Styled -Stylesheet ./monitor.css
```

## Functions

- `Stack` / `Card` — container elements; take a child scriptblock.
- `Text` — leaf element; positional content becomes the `text` attribute.
- `Element -Kind <name>` — generic escape hatch for any element kind.
- `Show-Styled -Stylesheet <path>` — cascade the layout against a CSS file and render
  it once to the console via the Spectre projection.

This is the walking skeleton (static render-once). Live data binding, a reactive store,
the Terminal.Gui loop, and graph/history widgets land in later sub-projects.
