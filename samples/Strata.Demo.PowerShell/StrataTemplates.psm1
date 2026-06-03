# Reusable Strata layout templates — import this into any monitor script.
#
#   Import-Module ./StrataTemplates.psm1
#   HostCard -Name 'google.com' -Base '$.hosts.google_com'
#
# A template is just a function returning a parameterized subtree. `Base` is the JSONPath prefix
# for this host's slice of store state; the card binds its graph and metric beneath it.

function HostCard {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Base
    )
    Card -Class 'host' {
        Text $Name -Class 'h2'
        Graph -Bind "$Base.history" -Class 'spark'
        Text -Bind "$Base.latency" -Class 'metric'
    }
}

Export-ModuleMember -Function HostCard
