using System.Text.Json.Nodes;
using Json.Path;
using Strata.Core;
using Strata.Css;
using Strata.Interaction;
using Strata.Properties.Styling;
using Strata.Render.TerminalGui;
using Terminal.Gui;

namespace Strata.Dsl.TerminalGui;

/// <summary>
/// Runs a full-screen interactive Strata app on Terminal.Gui. Binds the reactive store into the UI,
/// reconciles the view tree on every store change, and routes native widget events to store writes
/// and author callbacks. The <see cref="Run"/> loop needs a real terminal; the binding/write
/// helpers are split out so they unit-test without a driver.
/// </summary>
public sealed class StrataInteractiveHost
{
    private readonly StrataElement _root;
    private readonly StrataStore _store;
    private readonly Action<string, StrataUiEvent> _invokeHandler;

    private StrataInteractiveHost(StrataElement root, StrataStore store, Action<string, StrataUiEvent> invokeHandler)
    {
        _root = root;
        _store = store;
        _invokeHandler = invokeHandler;
    }

    /// <summary>Copy each bound list's resolved array into its <c>items</c> attribute for the projection.</summary>
    public static void BindListItems(StrataElement element, JsonObject state)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(state);
        if (element.Kind == "List" && element.TryGetAttribute("bind-data", out var path) && path is string p)
        {
            var node = ResolveFirst(p, state);
            if (node is JsonArray array)
            {
                element.SetAttribute("items", array.Select(n => (object?)(n?.ToString())).ToArray());
            }
        }

        foreach (var child in element.Children)
        {
            if (child is StrataElement strataChild)
            {
                BindListItems(strataChild, state);
            }
        }
    }

    /// <summary>Write a widget's value back to the store at its <c>bind-value</c> path, if it has one.</summary>
    public static void WriteFieldValue(StrataElement field, StrataStore store, string value)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(store);
        if (field.TryGetAttribute("bind-value", out var path) && path is string p)
        {
            store.Set(p, value);
        }
    }

    private static JsonNode? ResolveFirst(string jsonPath, JsonObject state)
    {
        var result = JsonPath.Parse(jsonPath).Evaluate(state);
        foreach (var match in result.Matches)
        {
            return match.Value;
        }

        return null;
    }

    /// <summary>
    /// Run the app: build the UI, enter the Terminal.Gui loop, and block until the user quits
    /// (Esc). When stdio is redirected (CI/headless), project once and return a summary instead.
    /// </summary>
    public static string Run(
        StrataElement root,
        string cssPath,
        StrataStore store,
        Action<string, StrataUiEvent> invokeHandler)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(cssPath);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(invokeHandler);

        var host = new StrataInteractiveHost(root, store, invokeHandler);

        var registry = StylingProperties.CreateRegistry();
        InteractionProperties.RegisterAll(registry);
        var css = File.ReadAllText(cssPath);
        var stylesheet = new CssStylesheetParser(new CssSelectorLanguage(), registry).Parse(css);
        var cascade = new Cascade(registry);
        using var projection = new TerminalGuiProjection { TextSelector = StrataText.ForNode };

        void Rebind()
        {
            StrataBinder.Apply(root, store.State);
            BindListItems(root, store.State);
        }

        if (Console.IsOutputRedirected || Console.IsInputRedirected)
        {
            Rebind();
            var view = projection.Project(root, cascade.Compute(root, stylesheet));
            return $"Strata interactive (headless): projected {projection.LiveViewCount} views, " +
                   $"root has {view.Subviews.Count} children. Run in a real terminal for the UI.";
        }

        return host.RunLoop(stylesheet, cascade, projection, Rebind);
    }

    private string RunLoop(
        IStylesheet stylesheet,
        Cascade cascade,
        TerminalGuiProjection projection,
        Action rebind)
    {
        EventHandler? onStoreChanged = null;
        Application.Init();
        try
        {
            var top = new Toplevel();
            var window = new Window { Width = Dim.Fill(), Height = Dim.Fill() };
            top.Add(window);

            using var input = new TerminalGuiInputSource();
            var commands = new CommandRegistry();
            using var interaction = new InteractionHost(input, commands);

            void Render()
            {
                rebind();
                var current = cascade.Compute(_root, stylesheet);
                projection.Project(_root, current);
                interaction.Reconcile(_root, current);
                WireWidgetEvents(projection);
                window.SetNeedsDisplay();
            }

            onStoreChanged = (_, _) => Application.Invoke(Render);
            _store.Changed += onStoreChanged;

            rebind();
            var initial = cascade.Compute(_root, stylesheet);
            var rootView = projection.Project(_root, initial);
            window.Add(rootView);
            interaction.Reconcile(_root, initial);
            WireWidgetEvents(projection);

            window.KeyDown += (_, key) =>
            {
                if (key.KeyCode == KeyCode.Esc)
                {
                    Application.RequestStop(top);
                    key.Handled = true;
                    return;
                }

                if (input.HandleKey(key) is not null)
                {
                    key.Handled = true;
                }
            };

            Application.Run(top);
            top.Dispose();
            return "ok";
        }
        finally
        {
            if (onStoreChanged is not null)
            {
                _store.Changed -= onStoreChanged;
            }

            Application.Shutdown();
        }
    }

    private readonly HashSet<StrataElement> _wired = new();

    private void WireWidgetEvents(TerminalGuiProjection projection) => WireNode(_root, projection);

    private void WireNode(StrataElement element, TerminalGuiProjection projection)
    {
        if (!_wired.Contains(element) && projection.TryGetView(element, out var view))
        {
            switch (view)
            {
                case Button button when element.TryGetAttribute("on-select", out var id) && id is string sid:
                    button.Accept += (_, _) => _invokeHandler(sid, new StrataUiEvent(_store, element, null));
                    _wired.Add(element);
                    break;
                case TextField field when element.TryGetAttribute("bind-value", out _) || element.TryGetAttribute("on-change", out _):
                    // View.TextChanged is a plain EventHandler (no typed args); read the current text directly.
                    field.TextChanged += (_, _) =>
                    {
                        var text = field.Text ?? string.Empty;
                        WriteFieldValue(element, _store, text);
                        if (element.TryGetAttribute("on-change", out var cid) && cid is string scid)
                        {
                            _invokeHandler(scid, new StrataUiEvent(_store, element, text));
                        }
                    };
                    _wired.Add(element);
                    break;
                case ListView list when element.TryGetAttribute("on-enter", out var id) && id is string lid:
                    list.OpenSelectedItem += (_, args) => _invokeHandler(lid, new StrataUiEvent(_store, element, args.Value));
                    _wired.Add(element);
                    break;
            }
        }

        foreach (var child in element.Children)
        {
            if (child is StrataElement strataChild)
            {
                WireNode(strataChild, projection);
            }
        }
    }
}
