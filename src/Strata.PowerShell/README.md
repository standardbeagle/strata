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

### Layout
- `Stack` / `Card` — container elements; take a child scriptblock.
- `Text` — leaf element; positional content becomes the `text` attribute, or `-Bind '$.path'`
  binds it to store state.
- `Graph -Bind '$.history'` — sparkline widget bound to a numeric array in store state.
- `Element -Kind <name>` — generic escape hatch for any element kind.

### Static render
- `Show-Styled -Stylesheet <path>` — cascade the layout against a CSS file and render it once.

### Reactive live dashboards
- `New-StrataStore @{ ... }` — create a reactive state store from a hashtable.
- `Update-StrataStore $store -Set '$.latency' -Value 12` — set a value.
- `Update-StrataStore $store -Append '$.history' -Value 12 -Cap 40` — append to a capped array
  (scrolling history).
- `Start-StrataApp -Layout $layout -Store $store -Stylesheet ./dashboard.css` — attach a live host
  that re-binds and redraws every time the store changes. Drive your own sampling loop and push
  updates; the dashboard reacts.

```powershell
$store  = New-StrataStore @{ host = 'google.com'; latency = 0; history = @() }
$layout = Stack {
    Text 'Ping Monitor' -Class 'h1'
    Card { Graph -Bind '$.history'; Text -Bind '$.latency' -Class 'metric' }
}
$app = Start-StrataApp -Layout $layout -Store $store -Stylesheet ./dashboard.css
while ($true) {
    $ms = (Test-Connection google.com -Count 1).Latency
    Update-StrataStore $store -Append '$.history' -Value $ms -Cap 40
    Update-StrataStore $store -Set    '$.latency' -Value $ms
    Start-Sleep 1
}
```

### Interactive apps (full-screen Terminal.Gui)
- `Button 'Run' -OnSelect { param($ctx) ... }` — focusable button; handler gets `$ctx.Store/.Element/.Value`.
- `TextField -Bind '$.query' -OnChange { ... }` — text input, two-way bound to store state.
- `List -Bind '$.rows' -OnEnter { param($ctx) ... }` — scrollable, selectable list bound to an array.
- `Register-StrataCommand -Name '<name>' -Action { param($ctx) ... }` — handler for a CSS
  `command: "<name>" when "key.…"` keymap.
- `Show-StrataApp -Layout $layout -Store $store -Stylesheet ./app.css` — blocks on the full-screen
  Terminal.Gui loop (Tab/arrows move focus, Enter activates, Esc quits). Headless-safe.

See `samples/Strata.Demo.PowerShell/db-query.ps1`.

### Templates
A reusable layout is a function returning a parameterized subtree — see
`samples/Strata.Demo.PowerShell/StrataTemplates.psm1` (`HostCard`), reused across the ping and
uptime monitor samples.

Rendering uses the Spectre live-redraw loop (ideal for monitoring dashboards). A Terminal.Gui
interactive projection (focus/keyboard) over the same store + widgets is an additive future option.
