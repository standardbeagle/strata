namespace Strata.Render.Spectre.Tests;

using global::Spectre.Console;
using global::Spectre.Console.Rendering;
using Strata.Core;
using Strata.Css;
using Strata.Properties.Styling;

public sealed class SpectreProjectionTests
{
    private static (ICascadeResult cascade, ITreeNode root) StyleTree(string css, ITreeNode root)
    {
        var registry = StylingProperties.CreateRegistry();
        var parser = new CssStylesheetParser(new CssSelectorLanguage(), registry);
        var stylesheet = parser.Parse(css);
        var cascade = new Cascade(registry).Compute(root, stylesheet);
        return (cascade, root);
    }

    private static string RenderToText(IRenderable renderable, int width = 80)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });
        console.Profile.Width = width;
        console.Write(renderable);
        return writer.ToString();
    }

    [Fact]
    public void Leaf_node_projects_to_text_with_underlying_content()
    {
        var nodeWithText = new TextNode("Process", "chrome");
        var (cascade, root) = StyleTree("Process { color: red; }", nodeWithText);

        var projection = new SpectreProjection();
        var renderable = projection.Project(root, cascade);

        renderable.Should().BeOfType<Text>();
        RenderToText(renderable).Should().Contain("chrome");
    }

    [Fact]
    public void Container_node_projects_children_as_rows()
    {
        var root = new TextNode("Window", "")
            .Add(new TextNode("Process", "init"))
            .Add(new TextNode("Process", "chrome"));

        var (cascade, styledRoot) = StyleTree("Process { color: green; }", root);
        var renderable = new SpectreProjection().Project(styledRoot, cascade);

        var text = RenderToText(renderable);
        text.Should().Contain("init");
        text.Should().Contain("chrome");
    }

    [Fact]
    public void Projection_is_pure_same_inputs_same_output()
    {
        var root = new TextNode("Window", "")
            .Add(new TextNode("Process", "a"))
            .Add(new TextNode("Process", "b"));

        var (cascade, styledRoot) = StyleTree("Process { color: blue; }", root);
        var projection = new SpectreProjection();

        var first = RenderToText(projection.Project(styledRoot, cascade));
        var second = RenderToText(projection.Project(styledRoot, cascade));
        first.Should().Be(second);
    }

    [Fact]
    public void Custom_text_selector_is_honored()
    {
        var node = new TextNode("Process", "ignored");
        var (cascade, root) = StyleTree("Process { color: red; }", node);

        var projection = new SpectreProjection
        {
            TextSelector = n => $"<{n.Kind}>",
        };

        RenderToText(projection.Project(root, cascade)).Should().Contain("<Process>");
    }

    [Fact]
    public void Button_kind_leaf_renders_with_bracket_chrome()
    {
        var button = new TextNode("Button", "OK");
        var (cascade, root) = StyleTree("Button { color: white; }", button);

        var text = RenderToText(new SpectreProjection().Project(root, cascade));

        // A native button wears bracket chrome so it reads as an actionable control, not a label.
        text.Should().Contain("[ OK ]");
    }

    [Fact]
    public void Dialog_kind_renders_as_a_bordered_titled_panel()
    {
        var dialog = new TextNode("Dialog", "")
            .Add(new TextNode("Cell", "Delete this file?"));
        var (cascade, root) = StyleTree("Cell { color: white; }", dialog);

        var text = RenderToText(new SpectreProjection().Project(root, cascade));

        // A Dialog floats its content inside a bordered box titled by its kind.
        text.Should().Contain("Delete this file?");
        text.Should().Contain("Dialog");
        text.Should().Contain("│", "the dialog is drawn inside a bordered panel");
    }

    /// <summary>In-memory node whose Underlying renders as a fixed string.</summary>
    private sealed class TextNode(string kind, string text) : ITreeNode
    {
        private readonly List<TextNode> _children = new();

        public string Kind { get; } = kind;

        public string? Id => null;

        public IReadOnlySet<string> Classes { get; } = new HashSet<string>();

        public IReadOnlySet<string> PseudoStates { get; } = new HashSet<string>();

        public ITreeNode? Parent { get; private set; }

        public IEnumerable<ITreeNode> Children => _children;

        public object? Underlying { get; } = text;

        public TextNode Add(TextNode child)
        {
            child.Parent = this;
            _children.Add(child);
            return this;
        }

        public bool TryGetAttribute(string name, out object? value)
        {
            value = null;
            return false;
        }
    }
}
