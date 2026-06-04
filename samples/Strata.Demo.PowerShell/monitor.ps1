#!/usr/bin/env pwsh
# Strata PowerShell DSL demo: author a static layout and render it once.
#
# Run from the repo root after building the host project:
#   dotnet build src/Strata.Dsl.TerminalGui/Strata.Dsl.TerminalGui.csproj
#   pwsh -NoProfile -File samples/Strata.Demo.PowerShell/monitor.ps1
#
# The module manifest requires both Strata.Dsl.dll and Strata.Dsl.TerminalGui.dll; the latter's
# build output (via CopyLocalLockFileAssemblies) holds both plus Spectre.Console and the other
# runtime deps. We point the default load context there so transitive assemblies resolve.

$binDir = Resolve-Path "$PSScriptRoot/../../src/Strata.Dsl.TerminalGui/bin/Debug/net10.0"

[System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving({
    param($context, $assemblyName)
    $candidate = Join-Path $binDir "$($assemblyName.Name).dll"
    if (Test-Path $candidate) { return $context.LoadFromAssemblyPath($candidate) }
    return $null
})

Add-Type -Path (Join-Path $binDir 'Strata.Dsl.TerminalGui.dll')
Import-Module "$PSScriptRoot/../../src/Strata.PowerShell/Strata.PowerShell.psd1" -Force

$layout = Stack -Class 'main' {
    Text 'Ping Monitor' -Class 'h1'
    Card -Class 'host' {
        Text 'google.com  12ms  ▁▂▃▅▂▁'
    }
}

$layout | Show-Styled -Stylesheet "$PSScriptRoot/monitor.css"
