#!/usr/bin/env pwsh
# Enhanced ping monitor: latency sparkline + last/min/avg/max + packet loss, live.
#
#   pwsh -File samples/Strata.Demo.PowerShell/ping-plus.ps1 -Target github.com
#   pwsh -File samples/Strata.Demo.PowerShell/ping-plus.ps1 -Simulate   # offline, synthetic data
#
# -Simulate is an explicit opt-in to synthetic latency so the demo runs with no network.

param([string]$Target = 'google.com', [switch]$Simulate, [int]$Samples = 40)

. "$PSScriptRoot/bootstrap.ps1"
Import-Module "$PSScriptRoot/DemoHelpers.psm1" -Force

$store = New-StrataStore @{ big = '—'; stats = 'waiting…'; loss = '0% loss'; history = @() }

$layout = Stack -Class 'main' {
    Text "PING  $Target" -Class 'h1'
    Card -Class 'host' {
        Text -Bind '$.big'     -Class 'big'
        Graph -Bind '$.history' -Class 'spark'
        Text -Bind '$.stats'   -Class 'stat'
        Text -Bind '$.loss'    -Class 'metric'
    }
}

$app = Start-StrataApp -Layout $layout -Store $store -Stylesheet "$PSScriptRoot/demos.css"
$sent = 0; $recv = 0; $series = @()
try {
    for ($i = 0; $i -lt $Samples; $i++) {
        $sent++
        if ($Simulate) {
            $ms = if ((Get-Random -Maximum 100) -lt 8) { $null } else { Get-Random -Minimum 9 -Maximum 48 }
        }
        else {
            try {
                $r = Test-Connection -TargetName $Target -Count 1 -TimeoutSeconds 2 -ErrorAction Stop
                $ms = [double]($r.Latency | Select-Object -First 1)
            }
            catch { $ms = $null }
        }

        if ($null -ne $ms) {
            $recv++
            $series += [double]$ms
            if ($series.Count -gt 50) { $series = $series[-50..-1] }
            Update-StrataStore $store -Append '$.history' -Value $ms -Cap 50
            Update-StrataStore $store -Set '$.big' -Value ('{0:0} ms' -f $ms)
            Update-StrataStore $store -Set '$.stats' -Value (Format-Stats -Series $series -Unit 'ms')
        }
        else {
            Update-StrataStore $store -Set '$.big' -Value 'timeout'
        }

        $lossPct = if ($sent -gt 0) { [int][Math]::Round(100 * ($sent - $recv) / $sent) } else { 0 }
        Update-StrataStore $store -Set '$.loss' -Value ('{0}% loss   ({1}/{2} received)' -f $lossPct, $recv, $sent)
        Start-Sleep -Milliseconds 700
    }
}
finally {
    $app.Dispose()
}
