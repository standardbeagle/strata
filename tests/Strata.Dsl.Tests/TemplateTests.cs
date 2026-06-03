using System.Collections.ObjectModel;
using System.Management.Automation;
using FluentAssertions;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

/// <summary>
/// Sub-project 5: a reusable layout template is just a PowerShell function returning a
/// parameterized subtree. These tests prove one `HostCard` template, authored once, composes and
/// binds independently when reused for multiple hosts.
/// </summary>
public sealed class TemplateTests
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

    [Fact]
    public void Reused_template_binds_each_host_independently()
    {
        var script = """
            function HostCard {
                param([string]$Name, [string]$Base)
                Card -Class 'host' {
                    Text $Name -Class 'h2'
                    Graph -Bind "$Base.history"
                    Text -Bind "$Base.latency" -Class 'metric'
                }
            }

            $store = New-StrataStore @{
                hosts = @{
                    a = @{ latency = 5; history = @(1, 2) }
                    b = @{ latency = 9; history = @(3, 4) }
                }
            }

            $layout = Stack {
                HostCard -Name 'A' -Base '$.hosts.a'
                HostCard -Name 'B' -Base '$.hosts.b'
            }

            [Strata.Dsl.StrataBinder]::Apply($layout, $store.State)
            $layout
        """;

        var root = Run(script).Should().ContainSingle().Subject.BaseObject
            .Should().BeOfType<StrataElement>().Subject;

        var cards = root.Children.Cast<StrataElement>().ToList();
        cards.Should().HaveCount(2);

        // Each card: [Text(name), Graph(history), Text(metric=latency)]
        Metric(cards[0]).Should().Be("5");
        Metric(cards[1]).Should().Be("9");
        GraphData(cards[0]).Should().Equal(1.0, 2.0);
        GraphData(cards[1]).Should().Equal(3.0, 4.0);
    }

    private static string Metric(StrataElement card)
    {
        var metric = card.Children.Cast<StrataElement>().Last();
        metric.TryGetAttribute("text", out var v);
        return v?.ToString() ?? string.Empty;
    }

    private static double[] GraphData(StrataElement card)
    {
        var graph = card.Children.Cast<StrataElement>().First(c => c.Kind == "Graph");
        graph.TryGetAttribute("data", out var v);
        return (double[])(v ?? Array.Empty<double>());
    }
}
