using FluentAssertions;
using Spectre.Console;
using Strata.Dsl;
using Xunit;

namespace Strata.Dsl.Tests;

public sealed class StrataLiveHostTests
{
    private static (IAnsiConsole Console, StringWriter Writer) RecordingConsole()
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });
        return (console, writer);
    }

    private static string WriteTempCss(string css)
    {
        var path = Path.Combine(Path.GetTempPath(), "strata-live-" + Guid.NewGuid().ToString("N") + ".css");
        File.WriteAllText(path, css);
        return path;
    }

    [Fact]
    public void Attach_renders_initial_state_then_updates_on_change()
    {
        var cssPath = WriteTempCss("Text { color: white; } Graph { color: white; }");
        try
        {
            var store = StrataStore.FromJson("""{ "latency": 0, "history": [] }""");
            var root = new StrataElement("Stack");
            root.Add(new StrataElement("Text", attributes: new Dictionary<string, object?> { ["bind-text"] = "$.latency" }));
            root.Add(new StrataElement("Graph", attributes: new Dictionary<string, object?> { ["bind-data"] = "$.history" }));

            var (console, writer) = RecordingConsole();
            using var host = StrataLiveHost.Attach(root, cssPath, store, console);

            store.Set("$.latency", 42);
            store.Append("$.history", 10);
            store.Append("$.history", 90);

            var output = writer.ToString();
            output.Should().Contain("42");
            output.Should().Contain("█"); // sparkline rendered the [10, 90] series
        }
        finally
        {
            File.Delete(cssPath);
        }
    }

    [Fact]
    public void Dispose_stops_reacting_to_changes()
    {
        var cssPath = WriteTempCss("Text { color: white; }");
        try
        {
            var store = StrataStore.FromJson("""{ "v": 1 }""");
            var root = new StrataElement("Text", attributes: new Dictionary<string, object?> { ["bind-text"] = "$.v" });

            var (console, writer) = RecordingConsole();
            var host = StrataLiveHost.Attach(root, cssPath, store, console);
            host.Dispose();

            var lengthAfterDispose = writer.ToString().Length;
            store.Set("$.v", 2);

            writer.ToString().Length.Should().Be(lengthAfterDispose);
        }
        finally
        {
            File.Delete(cssPath);
        }
    }
}
