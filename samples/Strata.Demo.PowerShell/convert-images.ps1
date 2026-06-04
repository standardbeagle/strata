#!/usr/bin/env pwsh
# Long-running dev task with live progress: a simulated image-conversion batch showing a progress
# bar, percent, current file, a throughput sparkline (images/sec), and an ETA.
#
#   pwsh -File samples/Strata.Demo.PowerShell/convert-images.ps1            # 60 synthetic images
#   pwsh -File samples/Strata.Demo.PowerShell/convert-images.ps1 -Count 200
#
# This is a demo of the progress PATTERN — it does not write files. Swap the inner body for a real
# converter (System.Drawing / ImageMagick) and keep the same store updates.

param([int]$Count = 60)

. "$PSScriptRoot/bootstrap.ps1"
Import-Module "$PSScriptRoot/DemoHelpers.psm1" -Force

$store = New-StrataStore @{
    current = 'starting…'; bar = (Format-Bar 0); rate = ''; eta = ''; throughput = @()
}

$layout = Stack -Class 'main' {
    Text 'IMAGE CONVERSION  ·  png → webp' -Class 'h1'
    Card -Class 'job' {
        Text -Bind '$.current'    -Class 'big'
        Text -Bind '$.bar'        -Class 'bar'
        Graph -Bind '$.throughput' -Class 'load'
        Text -Bind '$.rate'       -Class 'metric'
        Text -Bind '$.eta'        -Class 'stat'
    }
}

$app = Start-StrataApp -Layout $layout -Store $store -Stylesheet "$PSScriptRoot/demos.css"
$started = Get-Date
try {
    for ($n = 1; $n -le $Count; $n++) {
        $file = 'IMG_{0:D4}.png' -f $n

        # --- stand-in for real conversion work ---
        Start-Sleep -Milliseconds (Get-Random -Minimum 40 -Maximum 160)
        # ------------------------------------------

        $pct = [int][Math]::Round(100 * $n / $Count)
        $elapsed = ((Get-Date) - $started).TotalSeconds
        $rate = if ($elapsed -gt 0) { $n / $elapsed } else { 0 }
        $remaining = if ($rate -gt 0) { ($Count - $n) / $rate } else { 0 }

        Update-StrataStore $store -Set '$.current' -Value ("converting  $file  →  $($file -replace '\.png$', '.webp')")
        Update-StrataStore $store -Set '$.bar' -Value (Format-Bar $pct)
        Update-StrataStore $store -Append '$.throughput' -Value ([Math]::Round($rate, 2)) -Cap 40
        Update-StrataStore $store -Set '$.rate' -Value ('{0:0.0} img/s   ·   {1}/{2} done' -f $rate, $n, $Count)
        Update-StrataStore $store -Set '$.eta' -Value ('ETA {0:mm\:ss}' -f [TimeSpan]::FromSeconds($remaining))
    }
    Update-StrataStore $store -Set '$.current' -Value ("done  ·  $Count images converted")
}
finally {
    $app.Dispose()
}
