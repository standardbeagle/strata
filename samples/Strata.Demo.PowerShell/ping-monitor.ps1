#!/usr/bin/env pwsh
# Live ping dashboard: a reactive card (current latency + scrolling sparkline history) authored
# in PowerShell and redrawn each sample by Strata.
#
#   dotnet build src/Strata.Dsl/Strata.Dsl.csproj
#   pwsh -File samples/Strata.Demo.PowerShell/ping-monitor.ps1            # real pings to google.com
#   pwsh -File samples/Strata.Demo.PowerShell/ping-monitor.ps1 -Simulate # offline, synthetic data
#
# -Simulate is an explicit opt-in to synthetic latency so the demo runs with no network.

param([switch]$Simulate, [int]$Samples = 30)

. "$PSScriptRoot/bootstrap.ps1"

$store = New-StrataStore @{ hosts = @{ google_com = @{ latency = 0; history = @() } } }

$layout = Stack -Class 'main' {
    Text 'Ping Monitor' -Class 'h1'
    HostCard -Name 'google.com' -Base '$.hosts.google_com'
}

$app = Start-StrataApp -Layout $layout -Store $store -Stylesheet "$PSScriptRoot/dashboard.css"
try {
    for ($i = 0; $i -lt $Samples; $i++) {
        if ($Simulate) {
            $ms = Get-Random -Minimum 8 -Maximum 60
        }
        else {
            $ms = Measure-Latency 'google.com'
            if ($null -eq $ms) { Start-Sleep -Seconds 1; continue }
        }

        Update-StrataStore $store -Append '$.hosts.google_com.history' -Value $ms -Cap 40
        Update-StrataStore $store -Set '$.hosts.google_com.latency' -Value $ms
        Start-Sleep -Milliseconds 1000
    }
}
finally {
    $app.Dispose()
}
