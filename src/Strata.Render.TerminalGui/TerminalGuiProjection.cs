using Strata.Core.Properties;
using Strata.Properties.Styling;
using Terminal.Gui;
using TgAttribute = Terminal.Gui.Attribute;
using TgColor = Terminal.Gui.Color;

namespace Strata.Render.TerminalGui;

/// <summary>
/// Projects a styled Strata tree into a Terminal.Gui v2 <see cref="View"/> tree for full-screen
/// output, sharing the cascade engine with <c>SpectreProjection</c>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the Spectre projection — which is stateless and rebuilds an <c>IRenderable</c> on every
/// call — Terminal.Gui <see cref="View"/>s are stateful objects that own focus, layout, and a
/// driver-side display cache. Recreating them on every cascade would discard focus and flicker.
/// This projection therefore <em>reconciles</em>: it keeps a <c>node → View</c> map and, on each
/// <see cref="Project"/>, updates the existing view tree in place (the React-style diff described
/// in <c>docs/03-tech-design.md</c> §5.2).
/// </para>
/// <list type="bullet">
///   <item>A node seen for the first time gets a freshly created view, attached to its parent.</item>
///   <item>A node seen before keeps its view; only its mutable properties (text, colors) are
///         refreshed. The view instance — and thus its focus and layout state — is preserved
///         across cascades.</item>
///   <item>A node that disappeared from the tree has its view removed from its parent and disposed,
///         and is dropped from the map.</item>
/// </list>
/// <para>
/// Reconciliation rests on <see cref="ITreeNode"/> identity being stable across cascade runs
/// (reinforcing the Phase 0 design): the same logical node must compare equal between cascades for
/// its view — and the focus living on it — to survive. When that holds, focus and selection state
/// persist across re-cascade with no extra bookkeeping.
/// </para>
/// <para>
/// <b>Fallback not taken.</b> The plan's mid-phase checkpoint allowed a tear-down-and-recreate
/// fallback if diff reconciliation proved intractable. It did not: Terminal.Gui's
/// <see cref="View"/> exposes mutable <see cref="View.Text"/>, <see cref="View.ColorScheme"/>, and
/// an <see cref="View.Subviews"/> collection with add/remove, so in-place update is
/// straightforward. The diff path is the one shipped.
/// </para>
/// </remarks>
public sealed class TerminalGuiProjection : IProjection<View>, IDisposable
{
    // The reconciliation map: one persistent View per logical node, keyed by node identity.
    private readonly Dictionary<ITreeNode, View> _viewByNode = new();

    private bool _disposed;

    /// <summary>
    /// Optional hook to turn a node's underlying object into display text. Defaults to
    /// <see cref="object.ToString"/> on <see cref="ITreeNode.Underlying"/>.
    /// </summary>
    public Func<ITreeNode, string> TextSelector { get; init; } = DefaultText;

    /// <summary>The number of nodes currently backed by a live view.</summary>
    public int LiveViewCount => _viewByNode.Count;

    /// <summary>
    /// Look up the live view backing <paramref name="node"/>, if the node has been projected. A host
    /// uses this to drive Terminal.Gui focus onto the node its <c>FocusController</c> just focused.
    /// </summary>
    public bool TryGetView(ITreeNode node, out View view)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _viewByNode.TryGetValue(node, out view!);
    }

    /// <summary>
    /// Project (or re-project) the styled tree rooted at <paramref name="root"/> into a Terminal.Gui
    /// view, reconciling against the views produced by any prior call. The returned view is the
    /// root's view; mount it once into an <see cref="Application"/> top-level and call
    /// <see cref="Project"/> again after each re-cascade to refresh it in place.
    /// </summary>
    public View Project(ITreeNode root, ICascadeResult cascade)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(cascade);

        var live = new HashSet<ITreeNode>();
        var view = Reconcile(root, cascade, parent: null, live);
        PruneRemoved(live);
        return view;
    }

    private View Reconcile(ITreeNode node, ICascadeResult cascade, View? parent, HashSet<ITreeNode> live)
    {
        live.Add(node);

        if (!_viewByNode.TryGetValue(node, out var view))
        {
            view = CreateView(node, cascade);
            _viewByNode[node] = view;
            parent?.Add(view);
        }
        else
        {
            UpdateView(view, node, cascade);
        }

        ReconcileChildren(view, node, cascade, live);
        return view;
    }

    private View CreateView(ITreeNode node, ICascadeResult cascade)
    {
        var isLeaf = !node.Children.Any();
        var view = isLeaf
            ? new Label { Text = TextSelector(node) }
            : new View();

        view.X = 0;
        view.Y = Pos.Bottom(view); // overwritten below by ReconcileChildren stacking
        view.Width = Dim.Fill();
        view.Height = isLeaf ? Dim.Auto() : Dim.Fill();
        view.ColorScheme = BuildColorScheme(node, cascade);

        // Container rows participate in focus traversal so the live FocusController's :focused
        // node can receive Terminal.Gui focus when the host drives SetFocus.
        view.CanFocus = isLeaf;
        return view;
    }

    private void UpdateView(View view, ITreeNode node, ICascadeResult cascade)
    {
        // Only mutable, cascade-derived state is refreshed; the view instance (and its focus) stays.
        if (view is Label && !node.Children.Any())
        {
            var text = TextSelector(node);
            if (!string.Equals(view.Text, text, StringComparison.Ordinal))
            {
                view.Text = text;
            }
        }

        view.ColorScheme = BuildColorScheme(node, cascade);
    }

    private void ReconcileChildren(View view, ITreeNode node, ICascadeResult cascade, HashSet<ITreeNode> live)
    {
        var children = node.Children.ToList();
        View? previous = null;
        foreach (var child in children)
        {
            var childView = Reconcile(child, cascade, view, live);

            // Stack children vertically: each child sits directly below the previous one.
            childView.Y = previous is null ? 0 : Pos.Bottom(previous);
            previous = childView;
        }
    }

    private void PruneRemoved(HashSet<ITreeNode> live)
    {
        if (_viewByNode.Count == live.Count)
        {
            return;
        }

        var removed = _viewByNode.Keys.Where(n => !live.Contains(n)).ToList();
        foreach (var node in removed)
        {
            var view = _viewByNode[node];
            view.SuperView?.Remove(view);
            view.Dispose();
            _viewByNode.Remove(node);
        }
    }

    private static ColorScheme BuildColorScheme(ITreeNode node, ICascadeResult cascade)
    {
        var color = cascade.GetComputed<ColorValue>(node, StylingProperties.Color);
        var background = cascade.GetComputed<ColorValue>(node, StylingProperties.Background);

        var foreground = TerminalGuiColorMap.ToTerminalGui(color, new TgColor(ColorName.Gray));
        var back = TerminalGuiColorMap.ToTerminalGui(background, new TgColor(ColorName.Black));

        var normal = new TgAttribute(foreground, back);

        // Focus inverts foreground/background so the :focused row reads as a cursor even before the
        // stylesheet's own :focused rule re-cascades.
        var focus = new TgAttribute(back, foreground);
        return new ColorScheme(normal, focus, normal, normal, focus);
    }

    private static string DefaultText(ITreeNode node)
        => node.Underlying?.ToString() ?? node.Kind;

    /// <summary>Dispose every view this projection created and clear the reconciliation map.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var view in _viewByNode.Values)
        {
            view.SuperView?.Remove(view);
            view.Dispose();
        }

        _viewByNode.Clear();
        _disposed = true;
    }
}
