using Strata;
using Strata.Core;
using Strata.Css;
using Strata.Interaction;
using Strata.Properties.Styling;
using Strata.Render.TerminalGui;
using Terminal.Gui;
using ITreeNode = Strata.ITreeNode;

// Show-Processes: a full-screen process explorer driven entirely by a stylesheet, sharing the
// Strata cascade engine with the inline Format-Styled (Spectre) demo.
//
// Pipeline:  process rows -> ProcessNode (ITreeNode + IPseudoStateMutable)
//            show-processes.css -> CssStylesheetParser -> IStylesheet
//            Cascade.Compute -> ICascadeResult
//            TerminalGuiProjection -> View tree (reconciled in place across re-cascades)
//            Terminal.Gui keystrokes -> TerminalGuiInputSource -> InteractionHost dispatcher
//                                    -> FocusController -> :focused pseudo-state -> re-cascade
//
// See docs/06-stateful-projection.md for the authoring model.

var cssPath = Path.Combine(AppContext.BaseDirectory, "show-processes.css");
var css = File.ReadAllText(cssPath);

// The "process list". In a real ps-bash command these come from Get-Process; here they are static
// so the sample is self-contained and the stylesheet does all the styling work.
string[] highCpu = ["high-cpu"];
string[] zombie = ["zombie"];

var table = new ProcessNode("Table");
var rows = new[]
{
    new ProcessNode("Process", "system", "running", 2, classes: null),
    new ProcessNode("Process", "chrome", "running", 71, classes: highCpu),
    new ProcessNode("Process", "vim", "running", 1, classes: null),
    new ProcessNode("Process", "old-job", "stopped", 0, classes: zombie),
};
foreach (var row in rows)
{
    table.Add(row);
}

// Shared engine: register both styling and interaction (command:) descriptors.
var registry = StylingProperties.CreateRegistry();
InteractionProperties.RegisterAll(registry);
var stylesheet = new CssStylesheetParser(new CssSelectorLanguage(), registry).Parse(css);
var cascade = new Cascade(registry);

var projection = new TerminalGuiProjection
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

// Headless / redirected output (CI, piping): build the view tree once to prove the projection path
// works, then exit without entering the interactive loop (which needs a real terminal driver).
if (Console.IsOutputRedirected || Console.IsInputRedirected)
{
    var result = cascade.Compute(table, stylesheet);
    var view = projection.Project(table, result);
    Console.WriteLine(
        $"Show-Processes (headless): projected {projection.LiveViewCount} views, " +
        $"root has {view.Subviews.Count} rows. Run in a real terminal for the interactive UI.");
    projection.Dispose();
    return;
}

Application.Init();
try
{
    var top = new Toplevel();
    var window = new Window
    {
        Title = "Show-Processes — Strata + Terminal.Gui (q to quit, j/k or arrows to move)",
        X = 0,
        Y = 0,
        Width = Dim.Fill(),
        Height = Dim.Fill(),
    };
    top.Add(window);

    // Live input source: Terminal.Gui keystrokes feed the same IInputSource the interaction layer
    // already speaks. The dispatcher routes them to the FocusController via the command: bindings.
    using var input = new TerminalGuiInputSource();
    var commands = new CommandRegistry();
    using var host = new InteractionHost(input, commands);

    var current = cascade.Compute(table, stylesheet);

    // The re-cascade loop: a focus move toggles :focused, which re-cascades and re-projects in place.
    // FocusController is assigned before this closure runs (it only fires on a later keystroke), so
    // capturing it here is safe.
    FocusController focus = null!;
    void OnFocusChanged(TreeChange _)
    {
        current = cascade.Compute(table, stylesheet);
        projection.Project(table, current); // reconciles the existing view tree
        host.Reconcile(table, current);
        if (focus.Focused is { } focused && projection.TryGetView(focused, out var focusedView))
        {
            focusedView.SetFocus();
        }

        window.SetNeedsDisplay();
    }

    focus = new FocusController(rows, onChange: OnFocusChanged);
    SampleCommands.RegisterNavigation(commands, focus);

    var rootView = projection.Project(table, current);
    window.Add(rootView);
    host.Reconcile(table, current);

    window.KeyDown += (_, key) =>
    {
        if (key.KeyCode == KeyCode.Q || key.KeyCode == KeyCode.Esc)
        {
            Application.RequestStop(top);
            key.Handled = true;
            return;
        }

        var name = input.HandleKey(key);
        if (name is not null)
        {
            key.Handled = true;
        }
    };

    Application.Run(top);
    top.Dispose();
}
finally
{
    projection.Dispose();
    Application.Shutdown();
}

// A process row as a Strata tree node. Reference identity (so the projection's reconciliation map
// and the FocusController's focus ring stay stable) plus IPseudoStateMutable (so focus can toggle
// :focused at runtime). This is the "stateful projection" authoring contract from
// docs/06-stateful-projection.md: nodes must be stable, mutable-pseudo-state objects.
internal sealed class ProcessNode : ITreeNode, IPseudoStateMutable
{
    private readonly List<ProcessNode> _children = new();
    private readonly HashSet<string> _pseudoStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _attributes = new(StringComparer.Ordinal);

    public ProcessNode(string kind)
    {
        Kind = kind;
        Classes = new HashSet<string>(StringComparer.Ordinal);
    }

    public ProcessNode(string kind, string name, string status, int cpu, string[]? classes)
        : this(kind)
    {
        _attributes["Name"] = name;
        _attributes["Status"] = status;
        _attributes["Cpu"] = cpu;
        Classes = (classes ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
    }

    public string Kind { get; }

    public string? Id => null;

    public IReadOnlySet<string> Classes { get; }

    public IReadOnlySet<string> PseudoStates => _pseudoStates;

    public bool AddPseudoState(string state) => _pseudoStates.Add(state);

    public bool RemovePseudoState(string state) => _pseudoStates.Remove(state);

    public ITreeNode? Parent { get; private set; }

    public IEnumerable<ITreeNode> Children => _children;

    public object? Underlying => this;

    public void Add(ProcessNode child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    public bool TryGetAttribute(string name, out object? value)
        => _attributes.TryGetValue(name, out value);
}
