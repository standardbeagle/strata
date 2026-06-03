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
        // RenderTestNode stores `text:` in this.Text, not in _attributes.
        // The default TextSelector calls node.Underlying.ToString() which returns this.Text ?? Kind.
        var node = new RenderTestNode("TextField", text: "SELECT 1");
        var (cascade, sheet) = Build("TextField { color: white; }");
        using var projection = new TerminalGuiProjection();

        var view = projection.Project(node, cascade.Compute(node, sheet));

        view.Should().BeOfType<TextField>();
        ((TextField)view).Text.Should().Be("SELECT 1");
        view.CanFocus.Should().BeTrue();
    }

    [Fact]
    public void TextField_initial_text_comes_from_the_TextSelector()
    {
        var node = new RenderTestNode("TextField", text: "ignored-underlying");
        var (cascade, sheet) = Build("TextField { color: white; }");
        using var projection = new TerminalGuiProjection { TextSelector = _ => "CUSTOM" };

        var view = projection.Project(node, cascade.Compute(node, sheet));

        ((TextField)view).Text.ToString().Should().Be("CUSTOM");
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
