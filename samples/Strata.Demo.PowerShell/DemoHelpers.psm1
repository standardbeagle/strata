# Shared helpers for the Strata demo dashboards.

# A text progress bar:  ████████████░░░░░░░░░░░░   50%
function Format-Bar {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int]$Percent, [int]$Width = 24)
    $p = [Math]::Max(0, [Math]::Min(100, $Percent))
    $filled = [int][Math]::Round($Width * $p / 100.0)
    ('█' * $filled) + ('░' * ($Width - $filled)) + ('  {0,3}%' -f $p)
}

# last / min / avg / max of a numeric series, formatted compactly.
function Format-Stats {
    [CmdletBinding()]
    param([double[]]$Series, [string]$Unit = 'ms')
    if (-not $Series -or $Series.Count -eq 0) { return "no data" }
    $last = $Series[-1]
    $min = ($Series | Measure-Object -Minimum).Minimum
    $max = ($Series | Measure-Object -Maximum).Maximum
    $avg = ($Series | Measure-Object -Average).Average
    'last {0:0}{4} · min {1:0} · avg {2:0} · max {3:0}' -f $last, $min, $avg, $max, $Unit
}

# A real TCP port probe (Test-NetConnection style). Returns Up + Ms.
function Test-Port {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ComputerName, [Parameter(Mandatory)][int]$Port, [int]$TimeoutMs = 1000)
    $client = [System.Net.Sockets.TcpClient]::new()
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $task = $client.ConnectAsync($ComputerName, $Port)
        if ($task.Wait($TimeoutMs) -and $client.Connected) {
            $sw.Stop()
            return [pscustomobject]@{ Up = $true; Ms = [double]$sw.ElapsedMilliseconds }
        }
        return [pscustomobject]@{ Up = $false; Ms = 0.0 }
    }
    catch {
        return [pscustomobject]@{ Up = $false; Ms = 0.0 }
    }
    finally {
        $client.Dispose()
    }
}

# Store key for a host[:port] string that is a valid JSONPath dot-notation member: non-word chars
# become '_', and a 'k_' prefix guarantees the key starts with a letter (so an IP like 8.8.8.8
# doesn't produce a digit-leading member that JSONPath dot-notation rejects).
function ConvertTo-Key {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Value)
    'k_' + ($Value -replace '\W', '_')
}

Export-ModuleMember -Function Format-Bar, Format-Stats, Test-Port, ConvertTo-Key
