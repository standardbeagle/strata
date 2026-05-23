using System.Text.Json.Nodes;
using Spectre.Console;
using Strata;
using Strata.Adapters.JsonNode;
using Strata.Core;
using Strata.Css;
using Strata.Layout.Yoga;
using Strata.Properties.Styling;
using Strata.Render.Spectre;

// Strata demo: style a process list with a CSS stylesheet and render it inline.
//
// Pipeline:  JSON tree -> JsonTreeAdapter -> ITreeNode
//            procs.css -> CssStylesheetParser -> IStylesheet
//            Cascade.Compute -> ICascadeResult
//            SpectreProjection -> IRenderable -> console

var cssPath = Path.Combine(AppContext.BaseDirectory, "procs.css");
var css = File.ReadAllText(cssPath);

// The "process list" as a JSON document. $type drives the Strata Kind.
var json = JsonNode.Parse(
    """
    [
      { "$type": "Process", "Name": "system",   "Status": "running",  "Cpu": 2,  "class": "" },
      { "$type": "Process", "Name": "chrome",   "Status": "running",  "Cpu": 71, "class": "high-cpu" },
      { "$type": "Process", "Name": "vim",      "Status": "running",  "Cpu": 1,  "class": "" },
      { "$type": "Process", "Name": "old-job",  "Status": "stopped",  "Cpu": 0,  "class": "zombie" }
    ]
    """)!;

// Strata's JsonNode adapter does not parse a "class" attribute into Classes yet,
// so apply class labels through a thin wrapping node. For the demo we lean on a
// custom ITreeNode that reads "class" + "$type" from the JSON object.
var adapter = new JsonTreeAdapter();
var rawRoot = adapter.Wrap(json);

ITreeNode root = new ClassAwareNode(rawRoot, parent: null);

var registry = StylingProperties.CreateRegistry();
var stylesheet = new CssStylesheetParser(new CssSelectorLanguage(), registry).Parse(css);
var cascade = new Cascade(registry).Compute(root, stylesheet);

var projection = new SpectreProjection
{
    TextSelector = node =>
    {
        node.TryGetAttribute("Name", out var name);
        node.TryGetAttribute("Status", out var status);
        node.TryGetAttribute("Cpu", out var cpu);
        return node.Kind == "Process"
            ? $"  {name,-12} {status,-10} cpu:{cpu,3}"
            : string.Empty;
    },
};

AnsiConsole.MarkupLine("[bold]Strata demo[/] — [dim]process list styled by procs.css[/]");
AnsiConsole.WriteLine();
AnsiConsole.Write(projection.Project(root, cascade));
AnsiConsole.WriteLine();

// --- Dashboard: lay the same process list out as a multi-column grid (Phase 4). -----------
var dashCssPath = Path.Combine(AppContext.BaseDirectory, "dashboard.css");
var dashCss = File.ReadAllText(dashCssPath);

// Root grid container with id="dashboard"; each Process is a grid cell.
ITreeNode dashRoot = new ClassAwareNode(rawRoot, parent: null, id: "dashboard");

var dashRegistry = StylingProperties.CreateRegistry();
LayoutProperties.RegisterAll(dashRegistry);
var dashStylesheet = new CssStylesheetParser(new CssSelectorLanguage(), dashRegistry).Parse(dashCss);
var dashCascade = new Cascade(dashRegistry).Compute(dashRoot, dashStylesheet);

// Compute layout against the terminal width, then project honoring the grid rects.
var available = new Strata.Layout.Yoga.Size(
    Math.Max(20, Console.IsOutputRedirected ? 80 : Console.WindowWidth), 8);
var layout = YogaLayoutPass.Compute(dashRoot, dashCascade, available);

var dashProjection = new SpectreProjection
{
    TextSelector = node =>
    {
        node.TryGetAttribute("Name", out var name);
        node.TryGetAttribute("Cpu", out var cpu);
        return node.Kind == "Process" ? $"{name,-12} cpu:{cpu,3}" : string.Empty;
    },
};

AnsiConsole.MarkupLine("[bold]Dashboard[/] — [dim]Get-Process as a multi-column grid (dashboard.css)[/]");
AnsiConsole.WriteLine();
AnsiConsole.Write(dashProjection.Project(dashRoot, dashCascade, layout));
AnsiConsole.WriteLine();

// A minimal ITreeNode wrapper that surfaces the JSON "class" property as Classes,
// preserving the underlying JsonTreeNode for everything else.
internal sealed class ClassAwareNode : ITreeNode
{
    private readonly ITreeNode _inner;
    private readonly List<ClassAwareNode> _children;

    private readonly string? _id;

    public ClassAwareNode(ITreeNode inner, ITreeNode? parent, string? id = null)
    {
        _inner = inner;
        Parent = parent;
        _id = id ?? inner.Id;

        var classes = new HashSet<string>(StringComparer.Ordinal);
        if (inner.TryGetAttribute("class", out var c) && c is string s)
        {
            foreach (var label in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                classes.Add(label);
            }
        }

        Classes = classes;

        // In this demo each Process is a render leaf — its JSON properties are attributes,
        // not child rows. Only the top-level array contributes child rows.
        _children = inner.Kind == "array"
            ? inner.Children.Select(child => new ClassAwareNode(child, this)).ToList()
            : new List<ClassAwareNode>();
    }

    public string Kind => _inner.Kind;

    public string? Id => _id;

    public IReadOnlySet<string> Classes { get; }

    public IReadOnlySet<string> PseudoStates => _inner.PseudoStates;

    public ITreeNode? Parent { get; }

    public IEnumerable<ITreeNode> Children => _children;

    public object? Underlying => _inner.Underlying;

    public bool TryGetAttribute(string name, out object? value) => _inner.TryGetAttribute(name, out value);
}
