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
