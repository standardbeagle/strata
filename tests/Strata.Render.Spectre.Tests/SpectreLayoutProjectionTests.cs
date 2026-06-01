namespace Strata.Render.Spectre.Tests;

using global::Spectre.Console;
using global::Spectre.Console.Rendering;
using Strata.Core;
using Strata.Css;
using Strata.Layout.Yoga;
using Strata.Properties.Styling;

public sealed class SpectreLayoutProjectionTests
{
    private static (ICascadeResult cascade, LayoutResult layout) StyleAndLayout(
        string css, ITreeNode root, int width, int height)
    {
        var registry = StylingProperties.CreateRegistry();
        LayoutProperties.RegisterAll(registry);
        var stylesheet = new CssStylesheetParser(new CssSelectorLanguage(), registry).Parse(css);
        var cascade = new Cascade(registry).Compute(root, stylesheet);
        var layout = YogaLayoutPass.Compute(root, cascade, new global::Strata.Layout.Yoga.Size(width, height));
        return (cascade, layout);
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
    public void Grid_display_renders_children_in_multiple_columns()
    {
        var root = new LayoutNode("Grid", "grid")
            .Add(new LayoutNode("Cell", text: "AAA"))
            .Add(new LayoutNode("Cell", text: "BBB"))
            .Add(new LayoutNode("Cell", text: "CCC"))
            .Add(new LayoutNode("Cell", text: "DDD"));

        var (cascade, layout) = StyleAndLayout(
            "#grid { display: grid; grid-template-columns: 8 8; grid-template-rows: 1 1; }",
            root, width: 16, height: 2);

        var text = RenderToText(new SpectreProjection().Project(root, cascade, layout));

        // Row-major: AAA and BBB share the first line; CCC and DDD the second.
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Contain("AAA").And.Contain("BBB");
        lines[1].Should().Contain("CCC").And.Contain("DDD");
    }

    [Fact]
    public void Trivial_layout_falls_back_to_document_order_rows()
    {
        var root = new LayoutNode("Window", "win")
            .Add(new LayoutNode("Cell", text: "first"))
            .Add(new LayoutNode("Cell", text: "second"));

        // No layout properties => trivial; projection stacks as rows.
        var (cascade, layout) = StyleAndLayout("Cell { color: white; }", root, 40, 10);
        layout.Trivial.Should().BeTrue();

        var text = RenderToText(new SpectreProjection().Project(root, cascade, layout));
        var firstIdx = text.IndexOf("first", StringComparison.Ordinal);
        var secondIdx = text.IndexOf("second", StringComparison.Ordinal);
        firstIdx.Should().BeLessThan(secondIdx, "rows stack in document order");
    }

    [Fact]
    public void Absolute_child_is_offset_by_its_top_inset()
    {
        var root = new LayoutNode("Panel", "panel")
            .Add(new LayoutNode("Flow", text: "body"))
            .Add(new LayoutNode("Float", "float", text: "note"));

        var (cascade, layout) = StyleAndLayout(
            """
            #panel { display: flex; flex-direction: column; width: 30; height: 6; }
            #float { position: absolute; top: 3; left: 0; width: 4; height: 1; }
            """, root, 30, 6);

        var text = RenderToText(new SpectreProjection().Project(root, cascade, layout));

        // The absolute "note" is pushed down by blank lines (top: 3), so it appears after "body".
        text.Should().Contain("note");
        var lines = text.Split('\n');
        var noteLine = Array.FindIndex(lines, l => l.Contains("note", StringComparison.Ordinal));
        noteLine.Should().BeGreaterThan(0, "an absolutely positioned child with top:3 is offset downward");
    }

    [Fact]
    public void Absolute_children_compose_in_z_index_order_not_document_order()
    {
        // Document order is top-then-bottom, but z-index inverts the paint order: the lower
        // z-index ("bottom", z:1) composes first, the higher ("top", z:5) composes last (frontmost).
        var root = new LayoutNode("Panel", "panel")
            .Add(new LayoutNode("Float", "top", text: "TOPLAYER"))
            .Add(new LayoutNode("Float", "bottom", text: "BOTLAYER"));

        var (cascade, layout) = StyleAndLayout(
            """
            #panel  { display: flex; flex-direction: column; width: 30; height: 6; }
            #top    { position: absolute; top: 0; left: 0; width: 8; height: 1; z-index: 5; }
            #bottom { position: absolute; top: 1; left: 0; width: 8; height: 1; z-index: 1; }
            """, root, 30, 6);

        var text = RenderToText(new SpectreProjection().Project(root, cascade, layout));

        var topIdx = text.IndexOf("TOPLAYER", StringComparison.Ordinal);
        var botIdx = text.IndexOf("BOTLAYER", StringComparison.Ordinal);
        topIdx.Should().BeGreaterThan(0);
        botIdx.Should().BeGreaterThan(0);
        botIdx.Should().BeLessThan(topIdx, "the lower z-index composes first; the higher paints last");
    }

    /// <summary>In-memory node carrying an id, classes, and fixed render text.</summary>
    private sealed class LayoutNode : ITreeNode
    {
        private readonly List<LayoutNode> _children = new();
        private readonly string? _text;

        public LayoutNode(string kind, string? id = null, string? text = null)
        {
            Kind = kind;
            Id = id;
            _text = text;
        }

        public string Kind { get; }

        public string? Id { get; }

        public IReadOnlySet<string> Classes { get; } = new HashSet<string>();

        public IReadOnlySet<string> PseudoStates { get; } = new HashSet<string>();

        public ITreeNode? Parent { get; private set; }

        public IEnumerable<ITreeNode> Children => _children;

        public object? Underlying => _text;

        public LayoutNode Add(LayoutNode child)
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
