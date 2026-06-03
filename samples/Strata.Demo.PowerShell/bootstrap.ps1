# Shared loader for the Strata PowerShell demos. Dot-source from a monitor script:
#   . "$PSScriptRoot/bootstrap.ps1"
#
# Points the default load context at the Strata.Dsl build output (so Spectre.Console and the
# other Strata.* runtime deps resolve), loads Strata.Dsl, then imports the module + templates.

$binDir = Resolve-Path "$PSScriptRoot/../../src/Strata.Dsl/bin/Debug/net10.0"

[System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving({
    param($context, $assemblyName)
    $candidate = Join-Path $binDir "$($assemblyName.Name).dll"
    if (Test-Path $candidate) { return $context.LoadFromAssemblyPath($candidate) }
    return $null
})

Add-Type -Path (Join-Path $binDir 'Strata.Dsl.dll')
Import-Module "$PSScriptRoot/../../src/Strata.PowerShell/Strata.PowerShell.psd1" -Force
Import-Module "$PSScriptRoot/StrataTemplates.psm1" -Force

function Measure-Latency {
    param([Parameter(Mandatory)][string]$Target)
    try {
        $reply = Test-Connection -TargetName $Target -Count 1 -TimeoutSeconds 2 -ErrorAction Stop
        return [double]($reply.Latency | Select-Object -First 1)
    }
    catch {
        return $null  # unreachable this round; caller skips rather than fabricating a value
    }
}
