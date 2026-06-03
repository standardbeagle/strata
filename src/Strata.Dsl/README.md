# Strata.Dsl

Element model and console render facade behind the `Strata.PowerShell` module.
`StrataElement` is a mutable `ITreeNode`; `StrataNode` is the factory the PowerShell
DSL calls; `StrataConsole.Render` reads a stylesheet, runs the Strata cascade, and
writes the styled tree to the console via the Spectre projection.
