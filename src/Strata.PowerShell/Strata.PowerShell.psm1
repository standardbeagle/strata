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
    param(
        [Parameter(Position = 0)][string]$Content,
        [string]$Bind,
        [string]$Class,
        [string]$Id,
        [hashtable]$Attr
    )
    $merged = if ($Attr) { $Attr.Clone() } else { @{} }
    if ($Content) { $merged['text'] = $Content }
    if ($Bind) { $merged['bind-text'] = $Bind }
    Element -Kind 'Text' -Class $Class -Id $Id -Attr $merged
}

function Graph {
    [CmdletBinding()]
    param([string]$Bind, [string]$Class, [string]$Id, [hashtable]$Attr)
    $merged = if ($Attr) { $Attr.Clone() } else { @{} }
    if ($Bind) { $merged['bind-data'] = $Bind }
    Element -Kind 'Graph' -Class $Class -Id $Id -Attr $merged
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

function New-StrataStore {
    [CmdletBinding()]
    param([Parameter(Mandatory, Position = 0)][hashtable]$InitialState)
    $json = $InitialState | ConvertTo-Json -Depth 25 -Compress
    [Strata.Dsl.StrataStore]::FromJson($json)
}

function Update-StrataStore {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)][Strata.Dsl.StrataStore]$Store,
        [string]$Set,
        [string]$Append,
        $Value,
        [int]$Cap = 0
    )
    if (-not $Set -and -not $Append) {
        throw "Update-StrataStore requires -Set or -Append."
    }
    if ($Set) { $Store.Set($Set, $Value) }
    if ($Append) { $Store.Append($Append, $Value, $Cap) }
}

function Start-StrataApp {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][Strata.Dsl.StrataElement]$Layout,
        [Parameter(Mandatory)][Strata.Dsl.StrataStore]$Store,
        [Parameter(Mandatory)][string]$Stylesheet
    )
    $path = (Resolve-Path -LiteralPath $Stylesheet).ProviderPath
    [Strata.Dsl.StrataLiveHost]::Attach($Layout, $path, $Store)
}

Export-ModuleMember -Function Element, Stack, Card, Text, Graph, Show-Styled, New-StrataStore, Update-StrataStore, Start-StrataApp
