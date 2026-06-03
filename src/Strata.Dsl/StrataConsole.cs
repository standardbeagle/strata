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
