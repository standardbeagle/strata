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
}
