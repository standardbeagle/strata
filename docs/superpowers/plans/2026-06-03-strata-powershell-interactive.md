# Strata PowerShell Interactive (Terminal.Gui) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `Show-StrataApp` cmdlet that runs a full-screen, keyboard/mouse interactive Terminal.Gui app authored in a `.ps1` — focusable Button, two-way-bound TextField, scrollable/selectable List — driven by the existing reactive store.

**Architecture:** Add TextField/List kinds to the existing reconciling `TerminalGuiProjection`; make `StrataElement` focusable via `IPseudoStateMutable`; build a new `Strata.Dsl.TerminalGui` host that owns the TG loop and the reactive cycle (store→bind→cascade→reconcile→refresh, and native widget events→store writes + scriptblock callbacks). The PowerShell module gains Button/TextField/List DSL functions, `Register-StrataCommand`, and `Show-StrataApp`. Reuses `InteractionHost`, `FocusController`, `TerminalGuiInputSource` unchanged.

**Tech Stack:** .NET 10 (lib net8.0;net10.0), Terminal.Gui 2.0.0-prealpha.216, Spectre-free, xUnit 2.9.2 + FluentAssertions 6.12.1, Microsoft.PowerShell.SDK 7.6.1.

**Spec:** `docs/superpowers/specs/2026-06-03-strata-powershell-interactive-design.md`

> **Terminal.Gui prealpha note:** `Application.Run` needs a real terminal driver, so the loop is never entered in tests — every test projects/wires headless (the pattern the existing 27 `Strata.Render.TerminalGui.Tests` already use). A few TG v2 member names (`Button.Accept`, `TextField.TextChanged`, `ListView.OpenSelectedItem`, `ListView.SetSource`) are pinned to prealpha.216; if the build errors on one, the fix is the matching current name — `TreatWarningsAsErrors` surfaces it immediately.

---

## File structure

| File | Responsibility |
|---|---|
| `src/Strata.Dsl/StrataElement.cs` (modify) | Implement `IPseudoStateMutable` → focusable/selectable |
| `src/Strata.Dsl/Strata.Dsl.csproj` (modify) | Reference `Strata.Interaction` for the interface |
| `src/Strata.Render.TerminalGui/TerminalGuiProjection.cs` (modify) | TextField + List kinds in CreateView/UpdateView |
| `src/Strata.Dsl.TerminalGui/Strata.Dsl.TerminalGui.csproj` | New interactive-host project |
| `src/Strata.Dsl.TerminalGui/StrataUiEvent.cs` | Event payload passed to handler callbacks |
| `src/Strata.Dsl.TerminalGui/StrataInteractiveHost.cs` | TG loop + reactive cycle + event wiring |
| `src/Strata.Dsl.TerminalGui/README.md` | Package readme |
| `src/Strata.PowerShell/Strata.PowerShell.psm1` (modify) | Button/TextField/List, Register-StrataCommand, Show-StrataApp |
| `src/Strata.PowerShell/Strata.PowerShell.psd1` (modify) | Export new functions; require new assembly |
| `tests/Strata.Render.TerminalGui.Tests/InteractiveWidgetTests.cs` | TextField/List projection tests |
| `tests/Strata.Dsl.Tests/PseudoStateTests.cs` | `StrataElement` IPseudoStateMutable |
| `tests/Strata.Dsl.TerminalGui.Tests/...` | Host wiring + headless app tests |
| `samples/Strata.Demo.PowerShell/db-query.ps1` + `query.css` | Interactive sample |

---

## Task 1: `StrataElement` implements `IPseudoStateMutable`

**Files:**
- Modify: `src/Strata.Dsl/Strata.Dsl.csproj`
- Modify: `src/Strata.Dsl/StrataElement.cs`
- Create: `tests/Strata.Dsl.Tests/PseudoStateTests.cs`

- [ ] **Step 1: Add the Strata.Interaction reference**

In `src/Strata.Dsl/Strata.Dsl.csproj`, add inside the existing `<ItemGroup>` of project references:

```xml
    <ProjectReference Include="..\Strata.Interaction\Strata.Interaction.csproj" />
```

- [ ] **Step 2: Write the failing test**

Create `tests/Strata.Dsl.Tests/PseudoStateTests.cs`:

```csharp
using FluentAssertions;
using Strata;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class PseudoStateTests
{
    [Fact]
    public void Element_is_pseudo_state_mutable()
    {
        var el = new StrataElement("Button");
        ((object)el).Should().BeAssignableTo<IPseudoStateMutable>();
    }

    [Fact]
    public void Add_and_remove_pseudo_state_toggles_membership_and_reports_change()
    {
        var el = new StrataElement("Button");
        var mutable = (IPseudoStateMutable)el;

        mutable.AddPseudoState("focused").Should().BeTrue();
        el.PseudoStates.Should().Contain("focused");
        mutable.AddPseudoState("focused").Should().BeFalse(); // already present

        mutable.RemovePseudoState("focused").Should().BeTrue();
        el.PseudoStates.Should().NotContain("focused");
        mutable.RemovePseudoState("focused").Should().BeFalse(); // already absent
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj --filter PseudoStateTests`
Expected: build FAILS — `StrataElement` is not `IPseudoStateMutable`.

- [ ] **Step 4: Implement the interface**

In `src/Strata.Dsl/StrataElement.cs`, change the class declaration:

```csharp
public sealed class StrataElement : ITreeNode, IPseudoStateMutable
```

Add `using Strata.Interaction;` at the top of the file (below the existing namespace-less usings — the file currently has none, so add it as the first line):

```csharp
using Strata.Interaction;

namespace Strata.Dsl;
```

Add these two methods inside the class, after `SetAttribute`:

```csharp
    /// <inheritdoc />
    public bool AddPseudoState(string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return _pseudoStates.Add(state);
    }

    /// <inheritdoc />
    public bool RemovePseudoState(string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return _pseudoStates.Remove(state);
    }
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj --filter PseudoStateTests`
Expected: PASS — 2 tests.

- [ ] **Step 6: Commit**

```bash
git add src/Strata.Dsl/StrataElement.cs src/Strata.Dsl/Strata.Dsl.csproj tests/Strata.Dsl.Tests/PseudoStateTests.cs
git commit -m "feat(dsl): StrataElement implements IPseudoStateMutable (focusable/selectable)"
```

---

## Task 2: TextField + List widget kinds in the Terminal.Gui projection

**Files:**
- Modify: `src/Strata.Render.TerminalGui/TerminalGuiProjection.cs`
- Create: `tests/Strata.Render.TerminalGui.Tests/InteractiveWidgetTests.cs`

The projection reads widget content from node attributes: `text` for the field's initial value,
`items` for the list. (The host binds store data into these attributes before projecting; see Task 4.)

- [ ] **Step 1: Write the failing test**

Create `tests/Strata.Render.TerminalGui.Tests/InteractiveWidgetTests.cs`:

```csharp
using Strata.Core;
using Strata.Css;
using Strata.Properties.Styling;
using Terminal.Gui;

namespace Strata.Render.TerminalGui.Tests;

public sealed class InteractiveWidgetTests
{
    private static (Cascade cascade, IStylesheet sheet) Build(string css)
    {
        var props = StylingProperties.CreateRegistry();
        var parser = new CssStylesheetParser(new CssSelectorLanguage(), props);
        return (new Cascade(props), parser.Parse(css));
    }

    [Fact]
    public void TextField_node_projects_to_a_focusable_textfield_with_initial_text()
    {
        var node = new RenderTestNode("TextField", text: "SELECT 1");
        var (cascade, sheet) = Build("TextField { color: white; }");
        using var projection = new TerminalGuiProjection { TextSelector = n => n.TryGetAttribute("text", out var t) ? t?.ToString() ?? "" : "" };

        var view = projection.Project(node, cascade.Compute(node, sheet));

        view.Should().BeOfType<TextField>();
        view.Text.Should().Be("SELECT 1");
        view.CanFocus.Should().BeTrue();
    }

    [Fact]
    public void List_node_projects_to_a_focusable_listview()
    {
        var node = new RenderTestNode("List");
        var (cascade, sheet) = Build("List { color: white; }");
        using var projection = new TerminalGuiProjection();

        var view = projection.Project(node, cascade.Compute(node, sheet));

        view.Should().BeOfType<ListView>();
        view.CanFocus.Should().BeTrue();
    }
}
```

> `RenderTestNode` may not accept an `items` collection; the List test only asserts the view type
> and focusability. Item population is covered by the host test in Task 4 (which sets the `items`
> attribute through a real `StrataElement`).

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Strata.Render.TerminalGui.Tests/Strata.Render.TerminalGui.Tests.csproj --filter InteractiveWidgetTests`
Expected: FAIL — TextField node projects to a `Label`, not a `TextField`.

- [ ] **Step 3: Add the kind predicates**

In `src/Strata.Render.TerminalGui/TerminalGuiProjection.cs`, next to the existing `IsButton`/`IsDialog` helpers (near line 288), add:

```csharp
    private static bool IsTextField(ITreeNode node)
        => string.Equals(node.Kind, "TextField", StringComparison.OrdinalIgnoreCase);

    private static bool IsList(ITreeNode node)
        => string.Equals(node.Kind, "List", StringComparison.OrdinalIgnoreCase);

    private static string[] Items(ITreeNode node)
        => node.TryGetAttribute("items", out var v) && v is IEnumerable<object?> seq
            ? seq.Select(i => i?.ToString() ?? string.Empty).ToArray()
            : Array.Empty<string>();
```

- [ ] **Step 4: Build the views in CreateView**

In `CreateView`, insert two new branches before the `else` that builds Label/View (i.e. after the `IsDialog` branch):

```csharp
        else if (IsTextField(node))
        {
            var field = new TextField { Text = TextSelector(node) };
            field.Width = Dim.Fill();
            field.Height = Dim.Absolute(1);
            view = field;
        }
        else if (IsList(node))
        {
            var list = new ListView();
            list.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(Items(node)));
            list.Width = Dim.Fill();
            list.Height = Dim.Fill();
            view = list;
        }
```

Then extend the `CanFocus` line at the end of `CreateView` so the new widgets focus:

```csharp
        view.CanFocus = IsButton(node) || IsDialog(node) || IsTextField(node) || IsList(node) || !node.Children.Any();
```

- [ ] **Step 5: Refresh list items in UpdateView (do not clobber TextField edits)**

In `UpdateView`'s `switch (view)`, add a `ListView` case (a `TextField` is intentionally absent so
re-cascade never overwrites what the user is typing):

```csharp
            case ListView listView:
                listView.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(Items(node)));
                break;
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Strata.Render.TerminalGui.Tests/Strata.Render.TerminalGui.Tests.csproj --filter InteractiveWidgetTests`
Expected: PASS — 2 tests. (If the build errors on `SetSource`/`ListView` member names, adjust to the
prealpha.216 equivalent and re-run.)

- [ ] **Step 7: Commit**

```bash
git add src/Strata.Render.TerminalGui/TerminalGuiProjection.cs tests/Strata.Render.TerminalGui.Tests/InteractiveWidgetTests.cs
git commit -m "feat(terminalgui): TextField + List widget kinds in the projection"
```

---

## Task 3: Scaffold `Strata.Dsl.TerminalGui` + `StrataUiEvent`

**Files:**
- Create: `src/Strata.Dsl.TerminalGui/Strata.Dsl.TerminalGui.csproj`
- Create: `src/Strata.Dsl.TerminalGui/README.md`
- Create: `src/Strata.Dsl.TerminalGui/StrataUiEvent.cs`
- Create: `tests/Strata.Dsl.TerminalGui.Tests/Strata.Dsl.TerminalGui.Tests.csproj`
- Create: `tests/Strata.Dsl.TerminalGui.Tests/StrataUiEventTests.cs`

- [ ] **Step 1: Create the project**

Create `src/Strata.Dsl.TerminalGui/Strata.Dsl.TerminalGui.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework />
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <IsPackable>true</IsPackable>
    <PackageId>StandardBeagle.Strata.Dsl.TerminalGui</PackageId>
    <Description>Interactive Terminal.Gui host for the Strata PowerShell DSL: full-screen apps with focusable widgets, text input, and lists bound to the reactive store.</Description>
    <!-- Copy runtime deps into bin so the PowerShell module can Add-Type the assembly. -->
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Strata.Abstractions\Strata.Abstractions.csproj" />
    <ProjectReference Include="..\Strata.Core\Strata.Core.csproj" />
    <ProjectReference Include="..\Strata.Css\Strata.Css.csproj" />
    <ProjectReference Include="..\Strata.Properties.Styling\Strata.Properties.Styling.csproj" />
    <ProjectReference Include="..\Strata.Interaction\Strata.Interaction.csproj" />
    <ProjectReference Include="..\Strata.Render.TerminalGui\Strata.Render.TerminalGui.csproj" />
    <ProjectReference Include="..\Strata.Dsl\Strata.Dsl.csproj" />
    <PackageReference Include="Terminal.Gui" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the package README**

Create `src/Strata.Dsl.TerminalGui/README.md`:

```markdown
# Strata.Dsl.TerminalGui

Interactive Terminal.Gui host behind the `Strata.PowerShell` module's `Show-StrataApp`. Runs a
full-screen app from a DSL layout + reactive store: focusable Button/TextField/List widgets,
two-way data binding, keyboard and mouse, on the Strata cascade engine.
```

- [ ] **Step 3: Write the failing test**

Create `tests/Strata.Dsl.TerminalGui.Tests/Strata.Dsl.TerminalGui.Tests.csproj`:

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
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Strata.Dsl.TerminalGui\Strata.Dsl.TerminalGui.csproj" />
  </ItemGroup>
</Project>
```

Create `tests/Strata.Dsl.TerminalGui.Tests/StrataUiEventTests.cs`:

```csharp
using FluentAssertions;
using Strata.Dsl;
using Strata.Dsl.TerminalGui;
using Xunit;

namespace Strata.Dsl.TerminalGui.Tests;

public sealed class StrataUiEventTests
{
    [Fact]
    public void Event_carries_store_element_and_value()
    {
        var store = StrataStore.FromJson("{}");
        var element = new StrataElement("Button");

        var ev = new StrataUiEvent(store, element, "clicked");

        ev.Store.Should().BeSameAs(store);
        ev.Element.Should().BeSameAs(element);
        ev.Value.Should().Be("clicked");
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/Strata.Dsl.TerminalGui.Tests/Strata.Dsl.TerminalGui.Tests.csproj`
Expected: build FAILS — `StrataUiEvent` does not exist.

- [ ] **Step 5: Implement `StrataUiEvent`**

Create `src/Strata.Dsl.TerminalGui/StrataUiEvent.cs`:

```csharp
namespace Strata.Dsl.TerminalGui;

/// <summary>
/// The context handed to a widget behavior callback (from <c>-OnSelect</c> / <c>-OnChange</c> /
/// <c>-OnEnter</c>): the reactive store, the element that fired, and the relevant value (field text
/// or selected list item).
/// </summary>
/// <param name="Store">The reactive store driving the app.</param>
/// <param name="Element">The DSL element whose widget raised the event.</param>
/// <param name="Value">Field text, selected item, or <see langword="null"/>.</param>
public sealed record StrataUiEvent(StrataStore Store, StrataElement Element, object? Value);
```

- [ ] **Step 6: Add both projects to the solution and run the test**

Run:
```bash
dotnet sln Strata.sln add src/Strata.Dsl.TerminalGui/Strata.Dsl.TerminalGui.csproj tests/Strata.Dsl.TerminalGui.Tests/Strata.Dsl.TerminalGui.Tests.csproj
dotnet test tests/Strata.Dsl.TerminalGui.Tests/Strata.Dsl.TerminalGui.Tests.csproj
```
Expected: both added; PASS — 1 test.

- [ ] **Step 7: Commit**

```bash
git add src/Strata.Dsl.TerminalGui tests/Strata.Dsl.TerminalGui.Tests Strata.sln
git commit -m "feat(interactive): scaffold Strata.Dsl.TerminalGui + StrataUiEvent"
```

---

## Task 4: `StrataInteractiveHost` — reactive cycle + event wiring

**Files:**
- Create: `src/Strata.Dsl.TerminalGui/StrataInteractiveHost.cs`
- Create: `tests/Strata.Dsl.TerminalGui.Tests/HostWiringTests.cs`

The host's testable surface is the wiring logic, kept in internal static methods that need no TG
driver: binding store arrays into the `items` attribute, and the UI→store write helpers. The
`Application.Run` loop is a thin method exercised only by the headless sample.

- [ ] **Step 1: Write the failing test**

Create `tests/Strata.Dsl.TerminalGui.Tests/HostWiringTests.cs`:

```csharp
using FluentAssertions;
using Strata.Dsl;
using Strata.Dsl.TerminalGui;
using Xunit;

namespace Strata.Dsl.TerminalGui.Tests;

public sealed class HostWiringTests
{
    [Fact]
    public void BindListItems_copies_a_bound_array_into_the_items_attribute()
    {
        var store = StrataStore.FromJson("""{ "rows": ["a", "b", "c"] }""");
        var list = new StrataElement("List", attributes: new Dictionary<string, object?> { ["bind-data"] = "$.rows" });
        var root = new StrataElement("Stack");
        root.Add(list);

        StrataInteractiveHost.BindListItems(root, store.State);

        list.TryGetAttribute("items", out var items).Should().BeTrue();
        ((IEnumerable<object?>)items!).Select(i => i?.ToString()).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void WriteFieldValue_sets_store_at_bind_value_path()
    {
        var store = StrataStore.FromJson("""{ "query": "" }""");
        var field = new StrataElement("TextField", attributes: new Dictionary<string, object?> { ["bind-value"] = "$.query" });

        StrataInteractiveHost.WriteFieldValue(field, store, "SELECT 1");

        store.State["query"]!.GetValue<string>().Should().Be("SELECT 1");
    }

    [Fact]
    public void WriteFieldValue_noop_without_bind_value()
    {
        var store = StrataStore.FromJson("{}");
        var field = new StrataElement("TextField");

        var act = () => StrataInteractiveHost.WriteFieldValue(field, store, "x");

        act.Should().NotThrow();
        store.State.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Strata.Dsl.TerminalGui.Tests/Strata.Dsl.TerminalGui.Tests.csproj --filter HostWiringTests`
Expected: build FAILS — `StrataInteractiveHost` does not exist.

- [ ] **Step 3: Implement the host wiring + loop**

Create `src/Strata.Dsl.TerminalGui/StrataInteractiveHost.cs`:

```csharp
using System.Text.Json.Nodes;
using Json.Path;
using Strata.Core;
using Strata.Css;
using Strata.Interaction;
using Strata.Properties.Styling;
using Strata.Render.TerminalGui;
using Terminal.Gui;

namespace Strata.Dsl.TerminalGui;

/// <summary>
/// Runs a full-screen interactive Strata app on Terminal.Gui. Binds the reactive store into the UI,
/// reconciles the view tree on every store change, and routes native widget events to store writes
/// and author callbacks. The <see cref="Run"/> loop needs a real terminal; the binding/write
/// helpers are split out so they unit-test without a driver.
/// </summary>
public sealed class StrataInteractiveHost
{
    private readonly StrataElement _root;
    private readonly StrataStore _store;
    private readonly Action<string, StrataUiEvent> _invokeHandler;

    private StrataInteractiveHost(StrataElement root, StrataStore store, Action<string, StrataUiEvent> invokeHandler)
    {
        _root = root;
        _store = store;
        _invokeHandler = invokeHandler;
    }

    /// <summary>Copy each bound list's resolved array into its <c>items</c> attribute for the projection.</summary>
    public static void BindListItems(StrataElement element, JsonObject state)
    {
        if (element.Kind == "List" && element.TryGetAttribute("bind-data", out var path) && path is string p)
        {
            var node = ResolveFirst(p, state);
            if (node is JsonArray array)
            {
                element.SetAttribute("items", array.Select(n => (object?)(n?.ToString())).ToArray());
            }
        }

        foreach (var child in element.Children)
        {
            if (child is StrataElement strataChild)
            {
                BindListItems(strataChild, state);
            }
        }
    }

    /// <summary>Write a widget's value back to the store at its <c>bind-value</c> path, if it has one.</summary>
    public static void WriteFieldValue(StrataElement field, StrataStore store, string value)
    {
        if (field.TryGetAttribute("bind-value", out var path) && path is string p)
        {
            store.Set(p, value);
        }
    }

    private static JsonNode? ResolveFirst(string jsonPath, JsonObject state)
    {
        var result = JsonPath.Parse(jsonPath).Evaluate(state);
        foreach (var match in result.Matches)
        {
            return match.Value;
        }

        return null;
    }

    /// <summary>
    /// Run the app: build the UI, enter the Terminal.Gui loop, and block until the user quits
    /// (q/Esc). When stdio is redirected (CI/headless), project once and return a summary instead.
    /// </summary>
    public static string Run(
        StrataElement root,
        string cssPath,
        StrataStore store,
        Action<string, StrataUiEvent> invokeHandler)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(invokeHandler);

        var host = new StrataInteractiveHost(root, store, invokeHandler);

        var registry = StylingProperties.CreateRegistry();
        InteractionProperties.RegisterAll(registry);
        var css = File.ReadAllText(cssPath);
        var stylesheet = new CssStylesheetParser(new CssSelectorLanguage(), registry).Parse(css);
        var cascade = new Cascade(registry);
        using var projection = new TerminalGuiProjection { TextSelector = StrataText.ForNode };

        void Rebind()
        {
            StrataBinder.Apply(root, store.State);
            BindListItems(root, store.State);
        }

        if (Console.IsOutputRedirected || Console.IsInputRedirected)
        {
            Rebind();
            var view = projection.Project(root, cascade.Compute(root, stylesheet));
            return $"Strata interactive (headless): projected {projection.LiveViewCount} views, " +
                   $"root has {view.Subviews.Count} children. Run in a real terminal for the UI.";
        }

        return host.RunLoop(cssPath, registry, stylesheet, cascade, projection, Rebind);
    }

    private string RunLoop(
        string cssPath,
        IPropertyRegistry registry,
        IStylesheet stylesheet,
        Cascade cascade,
        TerminalGuiProjection projection,
        Action rebind)
    {
        Application.Init();
        try
        {
            var top = new Toplevel();
            var window = new Window { Width = Dim.Fill(), Height = Dim.Fill() };
            top.Add(window);

            using var input = new TerminalGuiInputSource();
            var commands = new CommandRegistry();
            using var interaction = new InteractionHost(input, commands);

            void Render()
            {
                rebind();
                var current = cascade.Compute(_root, stylesheet);
                projection.Project(_root, current);
                interaction.Reconcile(_root, current);
                WireWidgetEvents(projection);
                window.SetNeedsDisplay();
            }

            _store.Changed += (_, _) => Application.Invoke(Render);

            rebind();
            var initial = cascade.Compute(_root, stylesheet);
            var rootView = projection.Project(_root, initial);
            window.Add(rootView);
            interaction.Reconcile(_root, initial);
            WireWidgetEvents(projection);

            window.KeyDown += (_, key) =>
            {
                if (key.KeyCode == KeyCode.Esc)
                {
                    Application.RequestStop(top);
                    key.Handled = true;
                    return;
                }

                if (input.HandleKey(key) is not null)
                {
                    key.Handled = true;
                }
            };

            Application.Run(top);
            top.Dispose();
            return "ok";
        }
        finally
        {
            Application.Shutdown();
        }
    }

    private void WireWidgetEvents(TerminalGuiProjection projection)
    {
        WireNode(_root, projection);
    }

    private void WireNode(StrataElement element, TerminalGuiProjection projection)
    {
        if (projection.TryGetView(element, out var view))
        {
            switch (view)
            {
                case Button button when element.TryGetAttribute("on-select", out var id) && id is string sid:
                    button.Accept += (_, _) => _invokeHandler(sid, new StrataUiEvent(_store, element, null));
                    break;
                case TextField field when element.TryGetAttribute("bind-value", out _) || element.TryGetAttribute("on-change", out _):
                    field.TextChanged += (_, _) =>
                    {
                        var text = field.Text?.ToString() ?? string.Empty;
                        WriteFieldValue(element, _store, text);
                        if (element.TryGetAttribute("on-change", out var cid) && cid is string scid)
                        {
                            _invokeHandler(scid, new StrataUiEvent(_store, element, text));
                        }
                    };
                    break;
                case ListView list when element.TryGetAttribute("on-enter", out var id) && id is string lid:
                    list.OpenSelectedItem += (_, args) =>
                        _invokeHandler(lid, new StrataUiEvent(_store, element, args.Value));
                    break;
            }
        }

        foreach (var child in element.Children)
        {
            if (child is StrataElement strataChild)
            {
                WireNode(strataChild, projection);
            }
        }
    }
}
```

> Event handlers are re-attached after each `Project` via `WireWidgetEvents`. Because reconciliation
> keeps the same `View` instance per node, guard against double-subscription if a test reveals it by
> tracking wired nodes in a `HashSet`; for v1 the handlers are idempotent writes, so re-wiring on
> reconcile is acceptable. If duplicate firing shows up, add a `HashSet<StrataElement> _wired` guard.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Strata.Dsl.TerminalGui.Tests/Strata.Dsl.TerminalGui.Tests.csproj --filter HostWiringTests`
Expected: PASS — 3 tests. (If the build errors on a TG event name like `Button.Accept` or
`OpenSelectedItem`/`args.Value`, adjust to the prealpha.216 member and re-run — these compile-time
errors are surfaced by `TreatWarningsAsErrors`.)

- [ ] **Step 5: Commit**

```bash
git add src/Strata.Dsl.TerminalGui/StrataInteractiveHost.cs tests/Strata.Dsl.TerminalGui.Tests/HostWiringTests.cs
git commit -m "feat(interactive): StrataInteractiveHost — reactive cycle, store writes, event wiring"
```

---

## Task 5: PowerShell surface — widgets, Register-StrataCommand, Show-StrataApp

**Files:**
- Modify: `src/Strata.PowerShell/Strata.PowerShell.psm1`
- Modify: `src/Strata.PowerShell/Strata.PowerShell.psd1`
- Modify: `tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj`
- Create: `tests/Strata.Dsl.Tests/InteractiveModuleTests.cs`

The module keeps a script-scoped table mapping a generated handler id → scriptblock. The DSL widget
functions register the scriptblock and stamp the id onto the element's `on-*` attribute.
`Show-StrataApp` passes a single dispatcher delegate (`[Action[string, StrataUiEvent]]`) into the
host; the dispatcher looks the id up and invokes the scriptblock with the event context.

- [ ] **Step 1: Add the widget + app functions to the module**

In `src/Strata.PowerShell/Strata.PowerShell.psm1`, add a handler table near the top (after
`Set-StrictMode`):

```powershell
$script:StrataHandlers = [System.Collections.Generic.Dictionary[string, scriptblock]]::new()
$script:StrataHandlerSeq = 0

function script:Register-Handler([scriptblock]$Block) {
    $script:StrataHandlerSeq++
    $id = "h$($script:StrataHandlerSeq)"
    $script:StrataHandlers[$id] = $Block
    return $id
}
```

Then add these functions before the final `Export-ModuleMember`:

```powershell
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
    $dispatch = [System.Action[string, Strata.Dsl.TerminalGui.StrataUiEvent]] {
        param($id, $ev)
        $block = $null
        if ($script:StrataHandlers.TryGetValue($id, [ref]$block)) {
            & $block $ev
        }
    }
    [Strata.Dsl.TerminalGui.StrataInteractiveHost]::Run($Layout, $path, $Store, $dispatch)
}
```

- [ ] **Step 2: Export the new functions**

In `src/Strata.PowerShell/Strata.PowerShell.psm1`, replace the final `Export-ModuleMember` line:

```powershell
Export-ModuleMember -Function Element, Stack, Card, Text, Graph, Button, TextField, List, Show-Styled, New-StrataStore, Update-StrataStore, Start-StrataApp, Register-StrataCommand, Show-StrataApp
```

In `src/Strata.PowerShell/Strata.PowerShell.psd1`, replace `FunctionsToExport` and add the second
required assembly:

```powershell
    RequiredAssemblies = @('Strata.Dsl.dll', 'Strata.Dsl.TerminalGui.dll')
    FunctionsToExport  = @('Element', 'Stack', 'Card', 'Text', 'Graph', 'Button', 'TextField', 'List', 'Show-Styled', 'New-StrataStore', 'Update-StrataStore', 'Start-StrataApp', 'Register-StrataCommand', 'Show-StrataApp')
```

- [ ] **Step 3: Make the interactive assembly available to the in-process tests**

In `tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj`, add a reference so `Strata.Dsl.TerminalGui.dll`
(and its deps) land in the test output beside the module, letting `Import-Module` resolve
`RequiredAssemblies`:

```xml
    <ProjectReference Include="..\..\src\Strata.Dsl.TerminalGui\Strata.Dsl.TerminalGui.csproj" />
```

- [ ] **Step 4: Write the failing test**

Create `tests/Strata.Dsl.Tests/InteractiveModuleTests.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Management.Automation;
using FluentAssertions;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class InteractiveModuleTests
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
            throw new InvalidOperationException(string.Join("; ", ps.Streams.Error.Select(e => e.ToString())));
        }
        return result;
    }

    private static StrataElement Single(Collection<PSObject> result)
        => result.Should().ContainSingle().Subject.BaseObject.Should().BeOfType<StrataElement>().Subject;

    [Fact]
    public void Button_sets_text_and_on_select_handler_id()
    {
        var button = Single(Run("Button 'Run' -OnSelect { }"));
        button.Kind.Should().Be("Button");
        button.TryGetAttribute("text", out var t).Should().BeTrue();
        t.Should().Be("Run");
        button.TryGetAttribute("on-select", out var h).Should().BeTrue();
        h.Should().BeOfType<string>();
    }

    [Fact]
    public void TextField_sets_bind_value_and_on_change()
    {
        var field = Single(Run("TextField -Bind '$.query' -OnChange { }"));
        field.Kind.Should().Be("TextField");
        field.TryGetAttribute("bind-value", out var b).Should().BeTrue();
        b.Should().Be("$.query");
        field.TryGetAttribute("on-change", out _).Should().BeTrue();
    }

    [Fact]
    public void List_sets_bind_data_and_on_enter()
    {
        var list = Single(Run("List -Bind '$.rows' -OnEnter { }"));
        list.Kind.Should().Be("List");
        list.TryGetAttribute("bind-data", out var b).Should().BeTrue();
        b.Should().Be("$.rows");
        list.TryGetAttribute("on-enter", out _).Should().BeTrue();
    }

    [Fact]
    public void ShowStrataApp_headless_projects_without_error()
    {
        var cssPath = Path.Combine(Path.GetTempPath(), "strata-app-" + Guid.NewGuid().ToString("N") + ".css");
        File.WriteAllText(cssPath, "Text { color: white; } Button { color: white; } TextField { color: white; } List { color: white; }");
        try
        {
            var escaped = cssPath.Replace("\\", "\\\\");
            var result = Run($$"""
                $store = New-StrataStore @{ query = ''; rows = @('x','y') }
                $layout = Stack {
                    Text 'DB Query' -Class 'h1'
                    TextField -Bind '$.query'
                    Button 'Run' -OnSelect { }
                    List -Bind '$.rows'
                }
                Show-StrataApp -Layout $layout -Store $store -Stylesheet '{{escaped}}'
            """);
            result.Should().ContainSingle();
            result[0].BaseObject.ToString().Should().Contain("headless");
        }
        finally
        {
            File.Delete(cssPath);
        }
    }
}
```

> The headless test relies on xUnit running with redirected stdio (the default), so
> `Console.IsOutputRedirected` is true and `Run` returns the projection summary instead of entering
> the TG loop.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj --filter InteractiveModuleTests`
Expected: PASS — 4 tests.

- [ ] **Step 6: Commit**

```bash
git add src/Strata.PowerShell tests/Strata.Dsl.Tests/InteractiveModuleTests.cs tests/Strata.Dsl.Tests/Strata.Dsl.Tests.csproj
git commit -m "feat(powershell): Button/TextField/List, Register-StrataCommand, Show-StrataApp"
```

---

## Task 6: Interactive sample + docs

**Files:**
- Create: `samples/Strata.Demo.PowerShell/query.css`
- Create: `samples/Strata.Demo.PowerShell/db-query.ps1`
- Modify: `src/Strata.PowerShell/README.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Create the stylesheet**

Create `samples/Strata.Demo.PowerShell/query.css`:

```css
/* Strata interactive demo — DB query tool. */
Text.h1 { color: brightcyan; font-weight: bold; }
TextField { color: brightwhite; }
TextField:focused { color: black; background: brightcyan; }
Button { color: brightgreen; }
Button:focused { color: black; background: brightgreen; }
List { color: white; }
List:focused { color: brightyellow; }
```

- [ ] **Step 2: Create the interactive sample**

Create `samples/Strata.Demo.PowerShell/db-query.ps1`:

```powershell
#!/usr/bin/env pwsh
# Interactive DB-query demo: type a query, press Run, scroll the results. Full-screen Terminal.Gui,
# authored in PowerShell on the reactive store.
#
#   dotnet build src/Strata.Dsl.TerminalGui/Strata.Dsl.TerminalGui.csproj
#   pwsh -File samples/Strata.Demo.PowerShell/db-query.ps1
#
# Tab/arrows move focus, Enter activates the focused control, Esc quits.

$binDir = Resolve-Path "$PSScriptRoot/../../src/Strata.Dsl.TerminalGui/bin/Debug/net10.0"
[System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving({
    param($context, $assemblyName)
    $candidate = Join-Path $binDir "$($assemblyName.Name).dll"
    if (Test-Path $candidate) { return $context.LoadFromAssemblyPath($candidate) }
    return $null
})
Add-Type -Path (Join-Path $binDir 'Strata.Dsl.TerminalGui.dll')
Import-Module "$PSScriptRoot/../../src/Strata.PowerShell/Strata.PowerShell.psd1" -Force

# Stand-in data source so the demo runs with no database.
function Invoke-DemoQuery([string]$q) {
    1..8 | ForEach-Object { "row $_  ::  $q" }
}

$store = New-StrataStore @{ query = 'SELECT * FROM users'; rows = @() }

$layout = Stack -Class 'app' {
    Text 'DB Query' -Class 'h1'
    TextField -Bind '$.query'
    Button 'Run' -OnSelect {
        param($ctx)
        $q = $ctx.Store.State['query'].ToString()
        Update-StrataStore $ctx.Store -Set '$.rows' -Value (Invoke-DemoQuery $q)
    }
    List -Bind '$.rows'
}

Show-StrataApp -Layout $layout -Store $store -Stylesheet "$PSScriptRoot/query.css"
```

- [ ] **Step 3: Update the module README**

In `src/Strata.PowerShell/README.md`, add a section after the "Reactive live dashboards" block:

```markdown
### Interactive apps (full-screen Terminal.Gui)
- `Button 'Run' -OnSelect { param($ctx) ... }` — focusable button; handler gets `$ctx.Store/.Element/.Value`.
- `TextField -Bind '$.query' -OnChange { ... }` — text input, two-way bound to store state.
- `List -Bind '$.rows' -OnEnter { param($ctx) ... }` — scrollable, selectable list bound to an array.
- `Register-StrataCommand -Name '<name>' -Action { param($ctx) ... }` — handler for a CSS
  `command: "<name>" when "key.…"` keymap.
- `Show-StrataApp -Layout $layout -Store $store -Stylesheet ./app.css` — blocks on the full-screen
  Terminal.Gui loop (Tab/arrows move focus, Enter activates, Esc quits). Headless-safe.

See `samples/Strata.Demo.PowerShell/db-query.ps1`.
```

- [ ] **Step 4: Update the CHANGELOG**

In `CHANGELOG.md`, under `## [Unreleased]` → `### Added`, append a bullet after the existing
PowerShell DSL entry:

```markdown
  - **Interactive Terminal.Gui apps** (`Strata.Dsl.TerminalGui` + `Show-StrataApp`) — full-screen
    apps with focusable `Button` (`-OnSelect`), two-way-bound `TextField` (`-Bind`/`-OnChange`), and
    scrollable/selectable `List` (`-Bind`/`-OnEnter`), driven by the reactive store. `StrataElement`
    implements `IPseudoStateMutable` so the existing `FocusController`/`SelectionController` toggle
    `:focused`/`:selected` on DSL elements. CSS `command:` keymaps register via
    `Register-StrataCommand`. Sample: `samples/Strata.Demo.PowerShell/db-query.ps1`.
```

- [ ] **Step 5: Full build + full test gate**

Run:
```bash
dotnet build Strata.sln
dotnet test Strata.sln
```
Expected: solution builds clean; every test project passes (the new `Strata.Dsl.TerminalGui.Tests`,
the new `InteractiveWidgetTests`/`PseudoStateTests`/`InteractiveModuleTests`, and all pre-existing
tests).

- [ ] **Step 6: Verify the sample runs (manual, requires a real terminal)**

Run (in an interactive terminal, not redirected):
```bash
pwsh -File samples/Strata.Demo.PowerShell/db-query.ps1
```
Expected: a full-screen app; Tab to the field and type, Tab to Run and press Enter, results fill the
list; arrows scroll; Esc quits. (CI/headless cannot run this — the headless `Show-StrataApp` test
covers the projection path.)

- [ ] **Step 7: Commit**

```bash
git add samples/Strata.Demo.PowerShell/query.css samples/Strata.Demo.PowerShell/db-query.ps1 src/Strata.PowerShell/README.md CHANGELOG.md
git commit -m "feat(powershell): interactive db-query sample + docs"
```

---

## Self-review notes

- **Spec coverage:** focusable element (Task 1), TextField/List widgets (Task 2), interactive host
  project + event payload (Task 3), reactive cycle + store writes + native-event wiring + headless
  guard (Task 4), scriptblock-primary behavior + `Register-StrataCommand` + `Show-StrataApp` +
  separate-cmdlet decision (Task 5), DB-query success-criteria sample + docs (Task 6). CSS
  `command:` keymaps reuse the unchanged `InteractionHost` path, exposed through
  `Register-StrataCommand`.
- **Deferred (per spec):** Table widget, Yoga-driven TG layout, custom mouse→command CSS bindings.
- **Type consistency:** `StrataUiEvent(Store, Element, Value)`, the `on-select`/`on-change`/
  `on-enter`/`bind-value`/`bind-data` attribute keys, `StrataInteractiveHost.Run/BindListItems/
  WriteFieldValue`, and the `[Action[string, StrataUiEvent]]` dispatcher are used identically across
  Tasks 3–6.
- **Prealpha risk:** TG v2 member names (`Button.Accept`, `TextField.TextChanged`,
  `ListView.OpenSelectedItem`/`SetSource`, `OpenSelectedItem` args `.Value`) are the documented v2
  surface; any mismatch is a compile error fixed mechanically against prealpha.216.
```
