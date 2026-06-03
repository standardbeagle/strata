# Strata.PowerShell — DSL functions that build a Strata element tree and render it.
# Authoring model: pure-PowerShell functions over the Strata.Dsl C# node factory.

Set-StrictMode -Version Latest

function Element {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)][string]$Kind,
        [string]$Class,
        [string]$Id,
        [hashtable]$Attr,
        [Parameter(Position = 1)][scriptblock]$Children
    )
    $classes = $null
    if ($Class) { $classes = [string[]]@($Class -split '\s+' | Where-Object { $_ }) }

    $attrs = $null
    if ($Attr) {
        $attrs = [System.Collections.Generic.Dictionary[string, object]]::new()
        foreach ($key in $Attr.Keys) { $attrs[$key] = $Attr[$key] }
    }

    $node = [Strata.Dsl.StrataNode]::Create($Kind, $Id, $classes, $attrs)

    if ($Children) {
        foreach ($child in (& $Children)) {
            if ($child) { [void]$node.Add($child) }
        }
    }
    $node
}

function Stack {
    [CmdletBinding()]
    param([string]$Class, [string]$Id, [hashtable]$Attr, [Parameter(Position = 0)][scriptblock]$Children)
    Element -Kind 'Stack' -Class $Class -Id $Id -Attr $Attr -Children $Children
}

function Card {
    [CmdletBinding()]
    param([string]$Class, [string]$Id, [hashtable]$Attr, [Parameter(Position = 0)][scriptblock]$Children)
    Element -Kind 'Card' -Class $Class -Id $Id -Attr $Attr -Children $Children
}

function Text {
    [CmdletBinding()]
    param([Parameter(Mandatory, Position = 0)][string]$Content, [string]$Class, [string]$Id, [hashtable]$Attr)
    $merged = if ($Attr) { $Attr.Clone() } else { @{} }
    $merged['text'] = $Content
    Element -Kind 'Text' -Class $Class -Id $Id -Attr $merged
}

function Show-Styled {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]$Layout,
        [Parameter(Mandatory)][string]$Stylesheet
    )
    process {
        $path = (Resolve-Path -LiteralPath $Stylesheet).ProviderPath
        [Strata.Dsl.StrataConsole]::Render($Layout, $path)
    }
}

Export-ModuleMember -Function Element, Stack, Card, Text, Show-Styled
