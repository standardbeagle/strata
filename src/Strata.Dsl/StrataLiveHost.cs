using Spectre.Console;
using Strata.Core;
using Strata.Css;
using Strata.Properties.Styling;
using Strata.Render.Spectre;

namespace Strata.Dsl;

/// <summary>
/// Drives a live, reactive Strata dashboard. Parses the stylesheet once, then re-renders the UI
/// tree to an <see cref="IAnsiConsole"/> every time the store's state changes: rebind data → run
/// the cascade → clear → write. The author owns their own sampling loop and pushes updates into
/// the store; the host reacts.
/// </summary>
public sealed class StrataLiveHost : IDisposable
{
    private readonly StrataElement _root;
    private readonly StrataStore _store;
    private readonly IAnsiConsole _console;
    private readonly IStylesheet _stylesheet;
    private readonly Cascade _cascade;
    private readonly SpectreProjection _projection;
    private bool _disposed;

    private StrataLiveHost(StrataElement root, string cssPath, StrataStore store, IAnsiConsole console)
    {
        _root = root;
        _store = store;
        _console = console;

        var registry = StylingProperties.CreateRegistry();
        var css = File.ReadAllText(cssPath);
        _stylesheet = new CssStylesheetParser(new CssSelectorLanguage(), registry).Parse(css);
        _cascade = new Cascade(registry);
        _projection = new SpectreProjection { TextSelector = StrataText.ForNode };
    }

    /// <summary>Attach a live host that renders to the shared <see cref="AnsiConsole.Console"/>.</summary>
    public static StrataLiveHost Attach(StrataElement root, string cssPath, StrataStore store)
        => Attach(root, cssPath, store, AnsiConsole.Console);

    /// <summary>
    /// Attach a live host: subscribe to the store, render once immediately, and return the host as
    /// an <see cref="IDisposable"/> that unsubscribes when disposed.
    /// </summary>
    public static StrataLiveHost Attach(StrataElement root, string cssPath, StrataStore store, IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(console);

        var host = new StrataLiveHost(root, cssPath, store, console);
        store.Changed += host.OnStoreChanged;
        host.Render();
        return host;
    }

    private void OnStoreChanged(object? sender, EventArgs e) => Render();

    /// <summary>Rebind state, re-cascade, and repaint the console.</summary>
    public void Render()
    {
        StrataBinder.Apply(_root, _store.State);
        var result = _cascade.Compute(_root, _stylesheet);
        _console.Clear();
        _console.Write(_projection.Project(_root, result));
    }

    /// <summary>Stop reacting to store changes.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _store.Changed -= OnStoreChanged;
        _disposed = true;
    }
}
