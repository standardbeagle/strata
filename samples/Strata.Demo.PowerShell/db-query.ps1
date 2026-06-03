#!/usr/bin/env pwsh
# Interactive DB-query demo: type a query, press Run, scroll the results. Full-screen Terminal.Gui,
# authored in PowerShell on the reactive store.
#
#   dotnet build src/Strata.Dsl.TerminalGui/Strata.Dsl.TerminalGui.csproj
#   pwsh -File samples/Strata.Demo.PowerShell/db-query.ps1
#
# Tab/arrows move focus, Enter activates the focused control, Esc quits.

$binDir = Resolve-Path "$PSScriptRoot/../../src/Strata.Dsl.TerminalGui/bin/Debug/net10.0"
[System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving({
    param($context, $assemblyName)
    $candidate = Join-Path $binDir "$($assemblyName.Name).dll"
    if (Test-Path $candidate) { return $context.LoadFromAssemblyPath($candidate) }
    return $null
})
Add-Type -Path (Join-Path $binDir 'Strata.Dsl.TerminalGui.dll')
Import-Module "$PSScriptRoot/../../src/Strata.PowerShell/Strata.PowerShell.psd1" -Force

# Stand-in data source so the demo runs with no database.
function Invoke-DemoQuery([string]$q) {
    1..8 | ForEach-Object { "row $_  ::  $q" }
}

$store = New-StrataStore @{ query = 'SELECT * FROM users'; rows = @() }

$layout = Stack -Class 'app' {
    Text 'DB Query' -Class 'h1'
    TextField -Bind '$.query'
    Button 'Run' -OnSelect {
        param($ctx)
        $q = $ctx.Store.State['query'].ToString()
        Update-StrataStore $ctx.Store -Set '$.rows' -Value (Invoke-DemoQuery $q)
    }
    List -Bind '$.rows'
}

Show-StrataApp -Layout $layout -Store $store -Stylesheet "$PSScriptRoot/query.css"
