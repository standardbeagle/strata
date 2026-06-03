#!/usr/bin/env pwsh
# Multi-host uptime dashboard: the SAME HostCard template (StrataTemplates.psm1) reused once per
# target, each bound to its own slice of store state. Demonstrates template reuse across scripts —
# ping-monitor.ps1 uses one card, this uses many, from the identical template.
#
#   pwsh -File samples/Strata.Demo.PowerShell/uptime-monitor.ps1 -Simulate

param([switch]$Simulate, [int]$Samples = 30)

. "$PSScriptRoot/bootstrap.ps1"

$targets = 'google.com', 'github.com', 'cloudflare.com'

# Host keys must be dot-free to address them by a dotted store path.
function Key([string]$t) { $t -replace '\W', '_' }

$init = @{ hosts = @{} }
foreach ($t in $targets) { $init.hosts[(Key $t)] = @{ latency = 0; history = @() } }
$store = New-StrataStore $init

$layout = Stack -Class 'main' {
    Text 'Uptime Monitor' -Class 'h1'
    foreach ($t in $targets) {
        HostCard -Name $t -Base ('$.hosts.' + (Key $t))
    }
}

$app = Start-StrataApp -Layout $layout -Store $store -Stylesheet "$PSScriptRoot/dashboard.css"
try {
    for ($i = 0; $i -lt $Samples; $i++) {
        foreach ($t in $targets) {
            $key = Key $t
            if ($Simulate) {
                $ms = Get-Random -Minimum 8 -Maximum 120
            }
            else {
                $ms = Measure-Latency $t
                if ($null -eq $ms) { continue }
            }

            Update-StrataStore $store -Append "`$.hosts.$key.history" -Value $ms -Cap 40
            Update-StrataStore $store -Set "`$.hosts.$key.latency" -Value $ms
        }
        Start-Sleep -Milliseconds 1000
    }
}
finally {
    $app.Dispose()
}
