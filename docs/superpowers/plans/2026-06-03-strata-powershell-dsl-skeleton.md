# Strata PowerShell DSL — Walking Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A PowerShell module that builds a Strata element tree from a `.ps1` via pure-PowerShell DSL functions and renders it once to the console through the existing Spectre projection.

**Architecture:** A new C# project `Strata.Dsl` provides the element node (`StrataElement : ITreeNode`), a static factory (`StrataNode`), and a render facade (`StrataConsole`) that reuses the cascade + Spectre pipeline already proven by `samples/Strata.Demo.Spectre`. A new module `Strata.PowerShell` (`.psd1` + `.psm1`) exposes thin DSL functions (`Stack`, `Card`, `Text`, `Element`) over the factory and a `Show-Styled` wrapper over the facade. Tests run PowerShell in-process via `Microsoft.PowerShell.SDK` from xUnit — no external `pwsh`/Pester needed.

**Tech Stack:** .NET 10 (libs multi-target net8.0;net10.0), Spectre.Console 0.49.1, xUnit 2.9.2 + FluentAssertions 6.12.1, Microsoft.PowerShell.SDK 7.6.1. Central package management (`Directory.Packages.props`); global build props in `Directory.Build.props` (Nullable, ImplicitUsings, TreatWarningsAsErrors, AOT-by-default).

**Spec:** `docs/superpowers/specs/2026-06-03-strata-powershell-dsl-skeleton-design.md`

---

## File structure

| File | Responsibility |
|---|---|
| `src/Strata.Dsl/Strata.Dsl.csproj` | New packable lib, net8.0;net10.0 |
| `src/Strata.Dsl/StrataElement.cs` | Mutable `ITreeNode` element built by the DSL |
| `src/Strata.Dsl/StrataNode.cs` | Static factory the PS DSL calls |
| `src/Strata.Dsl/StrataConsole.cs` | Render facade: tree + CSS path → console |
| `src/Strata.Dsl/README.md` | Package readme (required by `PackageReadmeFile`) |
| `src/Strata.PowerShell/Strata.PowerShell.psm1` | DSL functions + `Show-Styled` |
| `src/Strata.PowerShell/Strata.PowerShell.psd1` | Module manifest |
| `src/Strata.PowerShell/README.md` | Module usage doc |
| `tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj` | xUnit test project (+ PowerShell SDK) |
| `tests/Strata.Dsl.Tests/StrataElementTests.cs` | Node contract tests |
| `tests/Strata.Dsl.Tests/StrataNodeTests.cs` | Factory tests |
| `tests/Strata.Dsl.Tests/StrataConsoleTests.cs` | Render facade tests (captured console) |
| `tests/Strata.Dsl.Tests/DslModuleTests.cs` | In-process PowerShell DSL + `Show-Styled` tests |
| `samples/Strata.Demo.PowerShell/monitor.ps1` | Runnable example |
| `samples/Strata.Demo.PowerShell/monitor.css` | Example stylesheet |

---

## Task 1: Scaffold `Strata.Dsl` + `StrataElement` node

**Files:**
- Create: `src/Strata.Dsl/Strata.Dsl.csproj`
- Create: `src/Strata.Dsl/README.md`
- Create: `src/Strata.Dsl/StrataElement.cs`
- Create: `tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj`
- Create: `tests/Strata.Dsl.Tests/StrataElementTests.cs`

- [ ] **Step 1: Create the `Strata.Dsl` project file**

Create `src/Strata.Dsl/Strata.Dsl.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework />
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <IsPackable>true</IsPackable>
    <PackageId>StandardBeagle.Strata.Dsl</PackageId>
    <Description>PowerShell-facing DSL element model and console render facade for Strata: build a styled element tree from PowerShell and render it via the Spectre projection.</Description>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Strata.Abstractions\Strata.Abstractions.csproj" />
    <ProjectReference Include="..\Strata.Core\Strata.Core.csproj" />
    <ProjectReference Include="..\Strata.Css\Strata.Css.csproj" />
    <ProjectReference Include="..\Strata.Properties.Styling\Strata.Properties.Styling.csproj" />
    <ProjectReference Include="..\Strata.Render.Spectre\Strata.Render.Spectre.csproj" />
    <PackageReference Include="Spectre.Console" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the package README** (required — `Directory.Build.props` sets `PackageReadmeFile=README.md` and packs it when present)

Create `src/Strata.Dsl/README.md`:

```markdown
# Strata.Dsl

Element model and console render facade behind the `Strata.PowerShell` module.
`StrataElement` is a mutable `ITreeNode`; `StrataNode` is the factory the PowerShell
DSL calls; `StrataConsole.Render` reads a stylesheet, runs the Strata cascade, and
writes the styled tree to the console via the Spectre projection.
```

- [ ] **Step 3: Add both projects to the solution**

Run:
```bash
cd /home/beagle/work/core/strata
dotnet sln Strata.sln add src/Strata.Dsl/Strata.Dsl.csproj
```
Expected: `Project ... added to the solution.`

- [ ] **Step 4: Create the test project file**

Create `tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsAotCompatible>false</IsAotCompatible>
    <IsTrimmable>false</IsTrimmable>
    <EnableTrimAnalyzer>false</EnableTrimAnalyzer>
    <EnableAotAnalyzer>false</EnableAotAnalyzer>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CA1707;CA1062;CA1822;CA2007;CA1859;CA1861;CA1303;CA1031;NU1903</NoWarn>
    <NuGetAuditMode>direct</NuGetAuditMode>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.PowerShell.SDK" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Strata.Dsl\Strata.Dsl.csproj" />
  </ItemGroup>

  <!-- Module files copy beside Strata.Dsl.dll at the test output root so the manifest's
       RequiredAssemblies='Strata.Dsl.dll' resolves and Import-Module works in-process. -->
  <ItemGroup>
    <None Include="..\..\src\Strata.PowerShell\Strata.PowerShell.psd1" Link="Strata.PowerShell.psd1" CopyToOutputDirectory="PreserveNewest" />
    <None Include="..\..\src\Strata.PowerShell\Strata.PowerShell.psm1" Link="Strata.PowerShell.psm1" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

Then add to the solution:
```bash
dotnet sln Strata.sln add tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj
```
Expected: `Project ... added to the solution.`

> Note: the `None Include` items reference module files created in Task 4. They do not exist yet; MSBuild treats missing `None` globs as no-ops at build time until the files are created, so Tasks 1–3 build fine.

- [ ] **Step 5: Write the failing test**

Create `tests/Strata.Dsl.Tests/StrataElementTests.cs`:

```csharp
using FluentAssertions;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class StrataElementTests
{
    [Fact]
    public void Constructor_sets_kind_id_and_filters_blank_classes()
    {
        var el = new StrataElement("Stack", id: "root", classes: new[] { "a", "", "  ", "b" });

        el.Kind.Should().Be("Stack");
        el.Id.Should().Be("root");
        el.Classes.Should().BeEquivalentTo(new[] { "a", "b" });
        el.PseudoStates.Should().BeEmpty();
        el.Parent.Should().BeNull();
        el.Children.Should().BeEmpty();
        el.Underlying.Should().BeNull();
    }

    [Fact]
    public void TryGetAttribute_returns_value_on_hit_and_false_on_miss()
    {
        var attrs = new Dictionary<string, object?> { ["text"] = "hello" };
        var el = new StrataElement("Text", attributes: attrs);

        el.TryGetAttribute("text", out var hit).Should().BeTrue();
        hit.Should().Be("hello");
        el.TryGetAttribute("missing", out var miss).Should().BeFalse();
        miss.Should().BeNull();
    }

    [Fact]
    public void Add_appends_child_and_sets_its_parent()
    {
        var parent = new StrataElement("Stack");
        var child = new StrataElement("Text");

        var returned = parent.Add(child);

        returned.Should().BeSameAs(parent);
        parent.Children.Should().ContainSingle().Which.Should().BeSameAs(child);
        child.Parent.Should().BeSameAs(parent);
    }

    [Fact]
    public void Identity_is_reference_based()
    {
        var a = new StrataElement("Text");
        var b = new StrataElement("Text");

        a.Equals(a).Should().BeTrue();
        a.Equals(b).Should().BeFalse();
        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }
}
```

- [ ] **Step 6: Run the test to verify it fails to compile**

Run:
```bash
dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj
```
Expected: build FAILS — `StrataElement` does not exist.

- [ ] **Step 7: Implement `StrataElement`**

Create `src/Strata.Dsl/StrataElement.cs`:

```csharp
namespace Strata.Dsl;

/// <summary>
/// A mutable Strata element built by the PowerShell DSL. Implements <see cref="ITreeNode"/>
/// with reference identity so it stays stable across cascade runs, and exposes mutable class,
/// pseudo-state, and child collections so later sub-projects (focus, live state) can mutate it
/// in place.
/// </summary>
public sealed class StrataElement : ITreeNode
{
    private readonly List<StrataElement> _children = new();
    private readonly HashSet<string> _classes;
    private readonly HashSet<string> _pseudoStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _attributes;

    /// <summary>Create an element with a kind and optional id, classes, and attributes.</summary>
    public StrataElement(
        string kind,
        string? id = null,
        IEnumerable<string>? classes = null,
        IDictionary<string, object?>? attributes = null)
    {
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Id = id;
        _classes = classes is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(
                classes.Where(c => !string.IsNullOrWhiteSpace(c)),
                StringComparer.Ordinal);
        _attributes = attributes is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(attributes, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public string? Id { get; }

    /// <inheritdoc />
    public IReadOnlySet<string> Classes => _classes;

    /// <inheritdoc />
    public IReadOnlySet<string> PseudoStates => _pseudoStates;

    /// <inheritdoc />
    public ITreeNode? Parent { get; private set; }

    /// <inheritdoc />
    public IEnumerable<ITreeNode> Children => _children;

    /// <inheritdoc />
    public object? Underlying => null;

    /// <inheritdoc />
    public bool TryGetAttribute(string name, out object? value)
        => _attributes.TryGetValue(name, out value);

    /// <summary>Append <paramref name="child"/> and set its parent to this element.</summary>
    /// <returns>This element, to allow fluent chaining.</returns>
    public StrataElement Add(StrataElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        child.Parent = this;
        _children.Add(child);
        return this;
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run:
```bash
dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj
```
Expected: PASS — 4 tests passing.

- [ ] **Step 9: Commit**

```bash
git add src/Strata.Dsl tests/Strata.Dsl.Tests Strata.sln
git commit -m "feat(dsl): StrataElement node + Strata.Dsl/test projects"
```

---

## Task 2: `StrataNode` factory

**Files:**
- Create: `src/Strata.Dsl/StrataNode.cs`
- Create: `tests/Strata.Dsl.Tests/StrataNodeTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Strata.Dsl.Tests/StrataNodeTests.cs`:

```csharp
using FluentAssertions;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class StrataNodeTests
{
    [Fact]
    public void Create_builds_detached_element_with_kind_classes_attributes()
    {
        var node = StrataNode.Create(
            "Card",
            id: "host1",
            classes: new[] { "host", "up" },
            attributes: new Dictionary<string, object?> { ["text"] = "google.com" });

        node.Kind.Should().Be("Card");
        node.Id.Should().Be("host1");
        node.Classes.Should().BeEquivalentTo(new[] { "host", "up" });
        node.TryGetAttribute("text", out var t).Should().BeTrue();
        t.Should().Be("google.com");
        node.Parent.Should().BeNull();
    }

    [Fact]
    public void Create_normalizes_blank_id_to_null()
    {
        StrataNode.Create("Text", id: "   ").Id.Should().BeNull();
        StrataNode.Create("Text").Id.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj --filter StrataNodeTests
```
Expected: build FAILS — `StrataNode` does not exist.

- [ ] **Step 3: Implement `StrataNode`**

Create `src/Strata.Dsl/StrataNode.cs`:

```csharp
namespace Strata.Dsl;

/// <summary>
/// Factory the PowerShell DSL calls to build <see cref="StrataElement"/> nodes. Keeping a
/// static entry point gives the `.psm1` functions a single, stable call shape and normalizes a
/// blank id to <see langword="null"/>.
/// </summary>
public static class StrataNode
{
    /// <summary>Build a detached <see cref="StrataElement"/>; the caller wires children via Add.</summary>
    public static StrataElement Create(
        string kind,
        string? id = null,
        IEnumerable<string>? classes = null,
        IDictionary<string, object?>? attributes = null)
        => new(kind, string.IsNullOrWhiteSpace(id) ? null : id, classes, attributes);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:
```bash
dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj --filter StrataNodeTests
```
Expected: PASS — 2 tests passing.

- [ ] **Step 5: Commit**

```bash
git add src/Strata.Dsl/StrataNode.cs tests/Strata.Dsl.Tests/StrataNodeTests.cs
git commit -m "feat(dsl): StrataNode factory"
```

---

## Task 3: `StrataConsole` render facade

**Files:**
- Create: `src/Strata.Dsl/StrataConsole.cs`
- Create: `tests/Strata.Dsl.Tests/StrataConsoleTests.cs`

- [ ] **Step 1: Write the failing test**

The render path writes to an `IAnsiConsole`; the test passes a `StringWriter`-backed, no-color console and asserts the text appears.

Create `tests/Strata.Dsl.Tests/StrataConsoleTests.cs`:

```csharp
using FluentAssertions;
using Spectre.Console;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class StrataConsoleTests
{
    private static string CaptureRender(StrataElement root, string css)
    {
        var cssPath = Path.Combine(Path.GetTempPath(), "strata-" + Guid.NewGuid().ToString("N") + ".css");
        File.WriteAllText(cssPath, css);
        try
        {
            var writer = new StringWriter();
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Out = new AnsiConsoleOutput(writer),
            });
            StrataConsole.Render(root, cssPath, console);
            return writer.ToString();
        }
        finally
        {
            File.Delete(cssPath);
        }
    }

    [Fact]
    public void Render_writes_text_attribute_content()
    {
        var root = new StrataElement("Stack");
        root.Add(new StrataElement("Text", attributes: new Dictionary<string, object?> { ["text"] = "Ping Monitor" }));

        var output = CaptureRender(root, "Text { color: white; }");

        output.Should().Contain("Ping Monitor");
    }

    [Fact]
    public void Render_throws_when_stylesheet_missing()
    {
        var root = new StrataElement("Stack");
        var act = () => StrataConsole.Render(root, "does-not-exist.css");
        act.Should().Throw<FileNotFoundException>();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj --filter StrataConsoleTests
```
Expected: build FAILS — `StrataConsole` does not exist.

- [ ] **Step 3: Implement `StrataConsole`**

Create `src/Strata.Dsl/StrataConsole.cs`:

```csharp
using Spectre.Console;
using Strata.Core;
using Strata.Css;
using Strata.Properties.Styling;
using Strata.Render.Spectre;

namespace Strata.Dsl;

/// <summary>
/// Renders a DSL-built <see cref="StrataElement"/> tree to the console via the Spectre
/// projection: read the stylesheet, run the cascade, project, write. Stateless, render-once —
/// the same pipeline the <c>Strata.Demo.Spectre</c> sample proves.
/// </summary>
public static class StrataConsole
{
    /// <summary>Render to the shared <see cref="AnsiConsole.Console"/>.</summary>
    public static void Render(StrataElement root, string cssPath)
        => Render(root, cssPath, AnsiConsole.Console);

    /// <summary>Render to a caller-supplied console (used by tests to capture output).</summary>
    public static void Render(StrataElement root, string cssPath, IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(console);

        var css = File.ReadAllText(cssPath);
        var registry = StylingProperties.CreateRegistry();
        var stylesheet = new CssStylesheetParser(new CssSelectorLanguage(), registry).Parse(css);
        var cascade = new Cascade(registry).Compute(root, stylesheet);

        var projection = new SpectreProjection
        {
            TextSelector = node =>
                node.TryGetAttribute("text", out var value) ? value?.ToString() ?? string.Empty : string.Empty,
        };

        console.Write(projection.Project(root, cascade));
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:
```bash
dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj --filter StrataConsoleTests
```
Expected: PASS — 2 tests passing.

- [ ] **Step 5: Commit**

```bash
git add src/Strata.Dsl/StrataConsole.cs tests/Strata.Dsl.Tests/StrataConsoleTests.cs
git commit -m "feat(dsl): StrataConsole render facade over Spectre projection"
```

---

## Task 4: `Strata.PowerShell` module + DSL composition tests

**Files:**
- Create: `src/Strata.PowerShell/Strata.PowerShell.psm1`
- Create: `src/Strata.PowerShell/Strata.PowerShell.psd1`
- Create: `tests/Strata.Dsl.Tests/DslModuleTests.cs`

- [ ] **Step 1: Create the module script `Strata.PowerShell.psm1`**

Create `src/Strata.PowerShell/Strata.PowerShell.psm1`:

```powershell
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
    $classes = if ($Class) { [string[]]($Class -split '\s+' | Where-Object { $_ }) } else { [string[]]@() }

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
```

- [ ] **Step 2: Create the module manifest `Strata.PowerShell.psd1`**

Create `src/Strata.PowerShell/Strata.PowerShell.psd1` (GUID is a fixed, hand-assigned module identity — do not regenerate):

```powershell
@{
    RootModule         = 'Strata.PowerShell.psm1'
    ModuleVersion      = '0.1.0'
    GUID               = 'b3f1c2a4-5d6e-47f8-9a0b-1c2d3e4f5a6b'
    Author             = 'StandardBeagle'
    CompanyName        = 'StandardBeagle'
    Copyright          = 'Copyright (c) 2026 Andy Brummer'
    Description        = 'Author responsive terminal UIs in PowerShell, rendered by Strata.'
    PowerShellVersion  = '7.4'
    RequiredAssemblies = @('Strata.Dsl.dll')
    FunctionsToExport  = @('Element', 'Stack', 'Card', 'Text', 'Show-Styled')
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
```

> The test project (Task 1, Step 4) already copies these two files to the test output root beside `Strata.Dsl.dll`, so `RequiredAssemblies='Strata.Dsl.dll'` resolves when `Import-Module` runs in-process.

- [ ] **Step 3: Write the failing DSL composition test**

Create `tests/Strata.Dsl.Tests/DslModuleTests.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Management.Automation;
using FluentAssertions;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class DslModuleTests
{
    private static readonly string ModulePath =
        Path.Combine(AppContext.BaseDirectory, "Strata.PowerShell.psd1");

    private static Collection<PSObject> Run(string script)
    {
        using var ps = PowerShell.Create();
        ps.AddScript($"Import-Module '{ModulePath}' -Force").Invoke();
        ps.Commands.Clear();
        var result = ps.AddScript(script).Invoke();
        if (ps.HadErrors)
        {
            var errors = string.Join("; ", ps.Streams.Error.Select(e => e.ToString()));
            throw new InvalidOperationException($"PowerShell errors: {errors}");
        }
        return result;
    }

    private static StrataElement Single(Collection<PSObject> result)
        => result.Should().ContainSingle().Subject.BaseObject.Should().BeOfType<StrataElement>().Subject;

    [Fact]
    public void Stack_with_text_children_builds_typed_tree()
    {
        var root = Single(Run("Stack -Class 'a b' { Text 'x'; Text 'y' }"));

        root.Kind.Should().Be("Stack");
        root.Classes.Should().BeEquivalentTo(new[] { "a", "b" });

        var kids = root.Children.Cast<StrataElement>().ToList();
        kids.Should().HaveCount(2);
        kids[0].Kind.Should().Be("Text");
        kids[0].TryGetAttribute("text", out var t0).Should().BeTrue();
        t0.Should().Be("x");
        kids[1].TryGetAttribute("text", out var t1).Should().BeTrue();
        t1.Should().Be("y");
    }

    [Fact]
    public void Element_escape_hatch_builds_arbitrary_kind()
    {
        Single(Run("Element -Kind 'Gauge' -Id 'g1'")).Kind.Should().Be("Gauge");
    }

    [Fact]
    public void Nested_card_inside_stack_sets_parent_chain()
    {
        var root = Single(Run("Stack { Card -Class 'host' { Text 'google.com' } }"));

        var card = root.Children.Cast<StrataElement>().Should().ContainSingle().Subject;
        card.Kind.Should().Be("Card");
        card.Parent.Should().BeSameAs(root);

        var text = card.Children.Cast<StrataElement>().Should().ContainSingle().Subject;
        text.Kind.Should().Be("Text");
        text.Parent.Should().BeSameAs(card);
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run:
```bash
dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj --filter DslModuleTests
```
Expected: FAIL — `Import-Module` cannot find the module / functions not defined (module files are now created, so this run should actually pass; if it FAILS instead with a copy/resolution error, fix the `None Include` paths from Task 1 Step 4). The intent: this test exercises the freshly authored module. Treat a green run here as success and proceed to Step 5.

- [ ] **Step 5: Run the test to verify it passes**

Run:
```bash
dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj --filter DslModuleTests
```
Expected: PASS — 3 tests passing.

- [ ] **Step 6: Commit**

```bash
git add src/Strata.PowerShell tests/Strata.Dsl.Tests/DslModuleTests.cs tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj
git commit -m "feat(powershell): Strata.PowerShell DSL module (Stack/Card/Text/Element) + composition tests"
```

---

## Task 5: `Show-Styled` end-to-end test + sample + module README

**Files:**
- Modify: `tests/Strata.Dsl.Tests/DslModuleTests.cs` (add e2e test)
- Create: `samples/Strata.Demo.PowerShell/monitor.css`
- Create: `samples/Strata.Demo.PowerShell/monitor.ps1`
- Create: `src/Strata.PowerShell/README.md`

- [ ] **Step 1: Write the failing end-to-end test**

`Show-Styled` writes to the shared console; this test proves the wrapper wires layout → stylesheet → render without raising a PowerShell error. (Render output correctness is already covered by `StrataConsoleTests` via captured console.)

Add this method to the `DslModuleTests` class in `tests/Strata.Dsl.Tests/DslModuleTests.cs`:

```csharp
    [Fact]
    public void ShowStyled_renders_layout_without_error()
    {
        var cssPath = Path.Combine(Path.GetTempPath(), "strata-show-" + Guid.NewGuid().ToString("N") + ".css");
        File.WriteAllText(cssPath, "Text { color: white; }");
        try
        {
            var escaped = cssPath.Replace("\\", "\\\\");
            var act = () => Run($"Stack {{ Text 'Ping Monitor' }} | Show-Styled -Stylesheet '{escaped}'");
            act.Should().NotThrow();
        }
        finally
        {
            File.Delete(cssPath);
        }
    }
```

- [ ] **Step 2: Run the test to verify it passes**

Run:
```bash
dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj --filter DslModuleTests
```
Expected: PASS — 4 tests passing (the 3 from Task 4 plus this one).

> This test exercises already-implemented code (`Show-Styled` from Task 4 + `StrataConsole` from Task 3), so it passes on first run. That is correct for an integration test over finished units; no separate red step is meaningful here.

- [ ] **Step 3: Create the sample stylesheet**

Create `samples/Strata.Demo.PowerShell/monitor.css`:

```css
/* Strata PowerShell DSL demo — styling a simple ping-monitor layout. */
Text {
    color: white;
}

Text.h1 {
    color: brightcyan;
    font-weight: bold;
}

Card {
    color: brightgreen;
}
```

- [ ] **Step 4: Create the runnable sample script**

Create `samples/Strata.Demo.PowerShell/monitor.ps1`:

```powershell
#!/usr/bin/env pwsh
# Strata PowerShell DSL demo: author a static layout and render it once.
#
# Run from the repo root after building Strata.Dsl:
#   dotnet build src/Strata.Dsl/Strata.Dsl.csproj
#   pwsh -NoProfile -File samples/Strata.Demo.PowerShell/monitor.ps1
#
# The module's RequiredAssemblies needs Strata.Dsl.dll discoverable. For the demo we
# load it explicitly from the build output, then import the module.

$dll = Resolve-Path "$PSScriptRoot/../../src/Strata.Dsl/bin/Debug/net10.0/Strata.Dsl.dll"
Add-Type -Path $dll
Import-Module "$PSScriptRoot/../../src/Strata.PowerShell/Strata.PowerShell.psd1" -Force

$layout = Stack -Class 'main' {
    Text 'Ping Monitor' -Class 'h1'
    Card -Class 'host' {
        Text 'google.com  12ms  ▁▂▃▅▂▁'
    }
}

$layout | Show-Styled -Stylesheet "$PSScriptRoot/monitor.css"
```

- [ ] **Step 5: Create the module README**

Create `src/Strata.PowerShell/README.md`:

```markdown
# Strata.PowerShell

Author a responsive terminal UI in PowerShell — the TUI equivalent of an HTA app.

```powershell
Import-Module Strata.PowerShell

$layout = Stack -Class 'main' {
    Text 'Ping Monitor' -Class 'h1'
    Card -Class 'host' { Text 'google.com  12ms  ▁▂▃▅▂▁' }
}
$layout | Show-Styled -Stylesheet ./monitor.css
```

## Functions

- `Stack` / `Card` — container elements; take a child scriptblock.
- `Text` — leaf element; positional content becomes the `text` attribute.
- `Element -Kind <name>` — generic escape hatch for any element kind.
- `Show-Styled -Stylesheet <path>` — cascade the layout against a CSS file and render
  it once to the console via the Spectre projection.

This is the walking skeleton (static render-once). Live data binding, a reactive store,
the Terminal.Gui loop, and graph/history widgets land in later sub-projects.
```

- [ ] **Step 6: Run the full Strata.Dsl test suite + solution build**

Run:
```bash
dotnet build Strata.sln
dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj
```
Expected: solution builds with no errors; all Strata.Dsl.Tests pass (StrataElement 4, StrataNode 2, StrataConsole 2, DslModule 4 = 12 tests).

- [ ] **Step 7: Verify the sample runs (optional, requires pwsh)**

Run:
```bash
pwsh -NoProfile -File samples/Strata.Demo.PowerShell/monitor.ps1
```
Expected: prints "Ping Monitor" and the host card line, styled. If `pwsh` is unavailable, skip — the e2e test already covers the render path.

- [ ] **Step 8: Commit**

```bash
git add tests/Strata.Dsl.Tests/DslModuleTests.cs samples/Strata.Demo.PowerShell src/Strata.PowerShell/README.md
git commit -m "feat(powershell): Show-Styled e2e test, runnable sample, module README"
```

---

## Self-review notes

- **Spec coverage:** element model (Task 1), factory (Task 2), render facade + fail-fast errors (Task 3), DSL functions incl. `Element` escape hatch + composition (Task 4), `Show-Styled` + sample + success-criteria `.ps1` (Task 5). The spec's Pester/CI risk is resolved by running PowerShell in-process via `Microsoft.PowerShell.SDK` (already a test dependency) instead of external Pester.
- **Deferred (out of scope, per spec):** reactive store, live loop, Terminal.Gui, graph/history widgets, template packaging.
- **Type consistency:** `StrataElement` / `StrataNode.Create` / `StrataConsole.Render(root, cssPath[, console])` signatures and the `text` attribute key are used identically across Tasks 1–5 and the `.psm1`.
```
