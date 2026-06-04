# Strata.PowerShell — DSL functions that build a Strata element tree and render it.
# Authoring model: pure-PowerShell functions over the Strata.Dsl C# node factory.

Set-StrictMode -Version Latest

$script:StrataHandlers = [System.Collections.Generic.Dictionary[string, scriptblock]]::new()
$script:StrataHandlerSeq = 0

function script:Register-Handler([scriptblock]$Block) {
    $script:StrataHandlerSeq++
    $id = "h$($script:StrataHandlerSeq)"
    $script:StrataHandlers[$id] = $Block
    return $id
}

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
    param([string]$Class, [string]$Id, [string]$BindClass, [hashtable]$Attr, [Parameter(Position = 0)][scriptblock]$Children)
    $merged = if ($Attr) { $Attr.Clone() } else { @{} }
    if ($BindClass) { $merged['bind-class'] = $BindClass }
    Element -Kind 'Card' -Class $Class -Id $Id -Attr $merged -Children $Children
}

function Text {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0)][string]$Content,
        [string]$Bind,
        [string]$BindClass,
        [string]$Class,
        [string]$Id,
        [hashtable]$Attr
    )
    $merged = if ($Attr) { $Attr.Clone() } else { @{} }
    if ($Content) { $merged['text'] = $Content }
    if ($Bind) { $merged['bind-text'] = $Bind }
    if ($BindClass) { $merged['bind-class'] = $BindClass }
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

function Button {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)][string]$Label,
        [scriptblock]$OnSelect,
        [string]$Class, [string]$Id, [hashtable]$Attr
    )
    $merged = if ($Attr) { $Attr.Clone() } else { @{} }
    $merged['text'] = $Label
    if ($OnSelect) { $merged['on-select'] = Register-Handler $OnSelect }
    Element -Kind 'Button' -Class $Class -Id $Id -Attr $merged
}

function TextField {
    [CmdletBinding()]
    param(
        [string]$Bind,
        [scriptblock]$OnChange,
        [string]$Class, [string]$Id, [hashtable]$Attr
    )
    $merged = if ($Attr) { $Attr.Clone() } else { @{} }
    if ($Bind) { $merged['bind-value'] = $Bind }
    if ($OnChange) { $merged['on-change'] = Register-Handler $OnChange }
    Element -Kind 'TextField' -Class $Class -Id $Id -Attr $merged
}

function List {
    [CmdletBinding()]
    param(
        [string]$Bind,
        [scriptblock]$OnEnter,
        [string]$Class, [string]$Id, [hashtable]$Attr
    )
    $merged = if ($Attr) { $Attr.Clone() } else { @{} }
    if ($Bind) { $merged['bind-data'] = $Bind }
    if ($OnEnter) { $merged['on-enter'] = Register-Handler $OnEnter }
    Element -Kind 'List' -Class $Class -Id $Id -Attr $merged
}

function Register-StrataCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action
    )
    $script:StrataHandlers["cmd:$Name"] = $Action
}

function Show-StrataApp {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][Strata.Dsl.StrataElement]$Layout,
        [Parameter(Mandatory)][Strata.Dsl.StrataStore]$Store,
        [Parameter(Mandatory)][string]$Stylesheet
    )
    $path = (Resolve-Path -LiteralPath $Stylesheet).ProviderPath
    $handlers = $script:StrataHandlers
    $dispatch = {
        param($id, $ev)
        $block = $null
        if ($handlers.TryGetValue($id, [ref]$block)) {
            & $block $ev
        }
    }.GetNewClosure()
    $action = [System.Action[string, Strata.Dsl.TerminalGui.StrataUiEvent]]$dispatch
    $commandNames = [string[]]@($script:StrataHandlers.Keys | Where-Object { $_ -like 'cmd:*' } | ForEach-Object { $_.Substring(4) })
    [Strata.Dsl.TerminalGui.StrataInteractiveHost]::Run($Layout, $path, $Store, $action, $commandNames)
}

Export-ModuleMember -Function Element, Stack, Card, Text, Graph, Button, TextField, List, Show-Styled, New-StrataStore, Update-StrataStore, Start-StrataApp, Register-StrataCommand, Show-StrataApp
