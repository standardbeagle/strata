# Shared loader for the Strata PowerShell demos. Dot-source from a monitor script:
#   . "$PSScriptRoot/bootstrap.ps1"
#
# Points the default load context at the Strata.Dsl.TerminalGui build output — its bin (via
# CopyLocalLockFileAssemblies) holds every runtime dep the module needs: Strata.Dsl, Spectre.Console,
# Terminal.Gui, JsonPath.Net and the rest. The module manifest requires both Strata.Dsl.dll and
# Strata.Dsl.TerminalGui.dll, so we load from the dir that has both. Build it first:
#   dotnet build src/Strata.Dsl.TerminalGui/Strata.Dsl.TerminalGui.csproj

$binDir = Resolve-Path "$PSScriptRoot/../../src/Strata.Dsl.TerminalGui/bin/Debug/net10.0"

[System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving({
    param($context, $assemblyName)
    $candidate = Join-Path $binDir "$($assemblyName.Name).dll"
    if (Test-Path $candidate) { return $context.LoadFromAssemblyPath($candidate) }
    return $null
})

Add-Type -Path (Join-Path $binDir 'Strata.Dsl.TerminalGui.dll')
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
