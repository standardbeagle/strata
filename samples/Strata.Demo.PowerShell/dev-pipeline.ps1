#!/usr/bin/env pwsh
# Dev task pipeline dashboard: a row per stage (restore/build/test/lint/pack) showing live status
# (pending → running → ok/fail) and elapsed time, plus a rolling log line. Models a CI run.
#
#   pwsh -File samples/Strata.Demo.PowerShell/dev-pipeline.ps1
#   pwsh -File samples/Strata.Demo.PowerShell/dev-pipeline.ps1 -FailAt test   # force a failure
#
# Simulated stages (each just sleeps) so it runs anywhere. Replace each stage body with a real
# command and set status from $LASTEXITCODE to wire it to an actual build.

param([string]$FailAt = '')

. "$PSScriptRoot/bootstrap.ps1"
Import-Module "$PSScriptRoot/DemoHelpers.psm1" -Force

$stages = 'restore', 'build', 'test', 'lint', 'pack'

$init = @{ log = 'pipeline queued'; stages = @{} }
foreach ($s in $stages) { $init.stages[$s] = @{ status = '·  pending'; elapsed = '' } }
$store = New-StrataStore $init

$layout = Stack -Class 'main' {
    Text 'DEV PIPELINE' -Class 'h1'
    foreach ($s in $stages) {
        $base = '$.stages.' + $s
        Card -Class 'stage' {
            Text ($s.PadRight(10)) -Class 'h2'
            Text -Bind "$base.status" -Class 'metric'
            Text -Bind "$base.elapsed" -Class 'stat'
        }
    }
    Card -Class 'logbox' {
        Text 'log' -Class 'dim'
        Text -Bind '$.log' -Class 'stat'
    }
}

$app = Start-StrataApp -Layout $layout -Store $store -Stylesheet "$PSScriptRoot/demos.css"
try {
    foreach ($s in $stages) {
        $base = '$.stages.' + $s
        Update-StrataStore $store -Set "$base.status" -Value '●  running'
        Update-StrataStore $store -Set '$.log' -Value "→ $s started"

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $steps = Get-Random -Minimum 6 -Maximum 14
        for ($k = 1; $k -le $steps; $k++) {
            Start-Sleep -Milliseconds 180
            Update-StrataStore $store -Set "$base.elapsed" -Value ('{0:0.0}s' -f $sw.Elapsed.TotalSeconds)
        }
        $sw.Stop()

        if ($s -eq $FailAt) {
            Update-StrataStore $store -Set "$base.status" -Value '✗  failed'
            Update-StrataStore $store -Set '$.log' -Value "✗ $s FAILED after $('{0:0.0}s' -f $sw.Elapsed.TotalSeconds) — pipeline aborted"
            break
        }

        Update-StrataStore $store -Set "$base.status" -Value '✓  ok'
        Update-StrataStore $store -Set "$base.elapsed" -Value ('{0:0.0}s' -f $sw.Elapsed.TotalSeconds)
        Update-StrataStore $store -Set '$.log' -Value "✓ $s passed in $('{0:0.0}s' -f $sw.Elapsed.TotalSeconds)"
    }
    if (-not $FailAt) {
        Update-StrataStore $store -Set '$.log' -Value '✓ pipeline green — all stages passed'
    }
}
finally {
    $app.Dispose()
}
