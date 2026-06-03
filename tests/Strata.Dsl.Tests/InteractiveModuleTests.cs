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
