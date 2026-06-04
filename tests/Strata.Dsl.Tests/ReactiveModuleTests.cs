using System.Collections.ObjectModel;
using System.Management.Automation;
using FluentAssertions;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class ReactiveModuleTests
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

    private static T Single<T>(Collection<PSObject> result)
        => result.Should().ContainSingle().Subject.BaseObject.Should().BeOfType<T>().Subject;

    [Fact]
    public void Graph_sets_bind_data_attribute()
    {
        var graph = Single<StrataElement>(Run("Graph -Bind '$.history'"));
        graph.Kind.Should().Be("Graph");
        graph.TryGetAttribute("bind-data", out var v).Should().BeTrue();
        v.Should().Be("$.history");
    }

    [Fact]
    public void Text_bind_sets_bind_text_attribute()
    {
        var text = Single<StrataElement>(Run("Text -Bind '$.latency'"));
        text.TryGetAttribute("bind-text", out var v).Should().BeTrue();
        v.Should().Be("$.latency");
    }

    [Fact]
    public void Text_bindclass_sets_bind_class_attribute()
    {
        var text = Single<StrataElement>(Run("Text 'x' -BindClass '$.status' -Class 'metric'"));
        text.TryGetAttribute("bind-class", out var v).Should().BeTrue();
        v.Should().Be("$.status");
        text.Classes.Should().Contain("metric");
    }

    [Fact]
    public void New_and_Update_store_roundtrip()
    {
        var store = Single<StrataStore>(Run("""
            $s = New-StrataStore @{ latency = 0; history = @() }
            Update-StrataStore $s -Set '$.latency' -Value 12
            Update-StrataStore $s -Append '$.history' -Value 12 -Cap 40
            $s
        """));

        store.State["latency"]!.GetValue<int>().Should().Be(12);
        store.State["history"]!.AsArray().Should().ContainSingle();
    }

    [Fact]
    public void Update_store_without_target_throws()
    {
        var act = () => Run("Update-StrataStore (New-StrataStore @{}) -Value 1");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void StartApp_attaches_live_host_and_renders_on_update()
    {
        // Start-StrataApp renders to the shared console; assert the reactive wiring runs without error.
        var cssPath = Path.Combine(Path.GetTempPath(), "strata-app-" + Guid.NewGuid().ToString("N") + ".css");
        File.WriteAllText(cssPath, "Text { color: white; } Graph { color: white; }");
        try
        {
            var escaped = cssPath.Replace("\\", "\\\\");
            var act = () => Run($$"""
                $store = New-StrataStore @{ latency = 0; history = @() }
                $layout = Stack {
                    Text -Bind '$.latency'
                    Graph -Bind '$.history'
                }
                $app = Start-StrataApp -Layout $layout -Store $store -Stylesheet '{{escaped}}'
                Update-StrataStore $store -Set '$.latency' -Value 21
                Update-StrataStore $store -Append '$.history' -Value 21 -Cap 40
                $app.Dispose()
            """);
            act.Should().NotThrow();
        }
        finally
        {
            File.Delete(cssPath);
        }
    }
}
