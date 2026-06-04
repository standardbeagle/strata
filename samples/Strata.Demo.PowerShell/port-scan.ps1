#!/usr/bin/env pwsh
# Test-NetConnection-style multi-target port monitor: one card per host:port, UP/DOWN status +
# connect-latency sparkline, live.
#
#   pwsh -File samples/Strata.Demo.PowerShell/port-scan.ps1
#   pwsh -File samples/Strata.Demo.PowerShell/port-scan.ps1 -Target github.com:443 -Target 1.1.1.1:53
#   pwsh -File samples/Strata.Demo.PowerShell/port-scan.ps1 -Simulate
#
# -Simulate is an explicit opt-in to synthetic up/down + latency so the demo runs with no network.

param(
    [string[]]$Target = @('github.com:443', 'cloudflare.com:443', '8.8.8.8:53'),
    [switch]$Simulate,
    [int]$Samples = 40
)

. "$PSScriptRoot/bootstrap.ps1"
Import-Module "$PSScriptRoot/DemoHelpers.psm1" -Force

# Parse host:port specs into objects with a dot-free store key.
$targets = $Target | ForEach-Object {
    $parts = $_ -split ':'
    [pscustomobject]@{ Name = $_; Host = $parts[0]; Port = [int]$parts[1]; Key = (ConvertTo-Key $_) }
}

$init = @{ targets = @{} }
foreach ($t in $targets) { $init.targets[$t.Key] = @{ status = '· probing'; cls = 'pending'; ms = ''; history = @() } }
$store = New-StrataStore $init

$layout = Stack -Class 'main' {
    Text 'PORT SCAN' -Class 'h1'
    foreach ($t in $targets) {
        $base = '$.targets.' + $t.Key
        Card -Class 'host' {
            Text $t.Name -Class 'h2'
            Text -Bind "$base.status" -BindClass "$base.cls"
            Graph -Bind "$base.history" -Class 'spark'
            Text -Bind "$base.ms" -Class 'stat'
        }
    }
}

$app = Start-StrataApp -Layout $layout -Store $store -Stylesheet "$PSScriptRoot/demos.css"
try {
    for ($i = 0; $i -lt $Samples; $i++) {
        foreach ($t in $targets) {
            if ($Simulate) {
                $up = (Get-Random -Maximum 100) -lt 88
                $probe = [pscustomobject]@{ Up = $up; Ms = if ($up) { [double](Get-Random -Minimum 6 -Maximum 70) } else { 0.0 } }
            }
            else {
                $probe = Test-Port -ComputerName $t.Host -Port $t.Port -TimeoutMs 1200
            }

            $base = '$.targets.' + $t.Key
            if ($probe.Up) {
                Update-StrataStore $store -Append "$base.history" -Value $probe.Ms -Cap 40
                Update-StrataStore $store -Set "$base.status" -Value ('▲ UP   {0:0} ms' -f $probe.Ms)
                Update-StrataStore $store -Set "$base.cls" -Value 'up'
                Update-StrataStore $store -Set "$base.ms" -Value ('connect {0:0} ms' -f $probe.Ms)
            }
            else {
                Update-StrataStore $store -Set "$base.status" -Value '▼ DOWN'
                Update-StrataStore $store -Set "$base.cls" -Value 'down'
                Update-StrataStore $store -Set "$base.ms" -Value 'no connection'
            }
        }
        Start-Sleep -Milliseconds 900
    }
}
finally {
    $app.Dispose()
}
