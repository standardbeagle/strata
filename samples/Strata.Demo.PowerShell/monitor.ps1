#!/usr/bin/env pwsh
# Strata PowerShell DSL demo: author a static layout and render it once.
#
# Run from the repo root after building Strata.Dsl:
#   dotnet build src/Strata.Dsl/Strata.Dsl.csproj
#   pwsh -NoProfile -File samples/Strata.Demo.PowerShell/monitor.ps1
#
# Strata.Dsl.dll and its runtime deps (Spectre.Console, the other Strata.* assemblies)
# all live in the build output dir. We point the default load context at that dir so
# transitive assemblies resolve, then import the module.

$binDir = Resolve-Path "$PSScriptRoot/../../src/Strata.Dsl/bin/Debug/net10.0"

[System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving({
    param($context, $assemblyName)
    $candidate = Join-Path $binDir "$($assemblyName.Name).dll"
    if (Test-Path $candidate) { return $context.LoadFromAssemblyPath($candidate) }
    return $null
})

Add-Type -Path (Join-Path $binDir 'Strata.Dsl.dll')
Import-Module "$PSScriptRoot/../../src/Strata.PowerShell/Strata.PowerShell.psd1" -Force

$layout = Stack -Class 'main' {
    Text 'Ping Monitor' -Class 'h1'
    Card -Class 'host' {
        Text 'google.com  12ms  ▁▂▃▅▂▁'
    }
}

$layout | Show-Styled -Stylesheet "$PSScriptRoot/monitor.css"
