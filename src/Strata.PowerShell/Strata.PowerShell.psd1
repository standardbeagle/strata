@{
    RootModule         = 'Strata.PowerShell.psm1'
    ModuleVersion      = '0.1.0'
    GUID               = 'b3f1c2a4-5d6e-47f8-9a0b-1c2d3e4f5a6b'
    Author             = 'StandardBeagle'
    CompanyName        = 'StandardBeagle'
    Copyright          = 'Copyright (c) 2026 Andy Brummer'
    Description        = 'Author responsive terminal UIs in PowerShell, rendered by Strata.'
    PowerShellVersion  = '7.4'
    RequiredAssemblies = @('Strata.Dsl.dll', 'Strata.Dsl.TerminalGui.dll')
    FunctionsToExport  = @('Element', 'Stack', 'Card', 'Text', 'Graph', 'Button', 'TextField', 'List', 'Show-Styled', 'New-StrataStore', 'Update-StrataStore', 'Start-StrataApp', 'Register-StrataCommand', 'Show-StrataApp')
    CmdletsToExport    = @()
    VariablesToExport  = @()
    AliasesToExport    = @()
    PrivateData = @{
        PSData = @{
            Tags       = @('strata', 'tui', 'terminal', 'css')
            LicenseUri = 'https://opensource.org/licenses/MIT'
            ProjectUri = 'https://github.com/standardbeagle/strata'
        }
    }
}
