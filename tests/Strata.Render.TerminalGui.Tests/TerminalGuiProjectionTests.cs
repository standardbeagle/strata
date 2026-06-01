using Strata.Core;
using Strata.Css;
using Strata.Properties.Styling;
using Terminal.Gui;

namespace Strata.Render.TerminalGui.Tests;

public sealed class TerminalGuiProjectionTests
{
    private static (Cascade cascade, IStylesheet sheet) Build(string css)
    {
        var props = StylingProperties.CreateRegistry();
        var parser = new CssStylesheetParser(new CssSelectorLanguage(), props);
        return (new Cascade(props), parser.Parse(css));
    }

    private static RenderTestNode Table(out RenderTestNode[] rows, int count = 2)
    {
        var table = new RenderTestNode("Table");
        rows = Enumerable.Range(0, count)
            .Select(i => new RenderTestNode("Row", id: $"r{i}", text: $"row {i}"))
            .ToArray();
        foreach (var row in rows)
        {
            table.Add(row);
        }

        return table;
    }

    [Fact]
    public void Leaf_node_projects_to_a_label_carrying_its_text()
    {
        var leaf = new RenderTestNode("Row", text: "alpha");
        var (cascade, sheet) = Build("Row { color: red; }");
        using var projection = new TerminalGuiProjection();

        var view = projection.Project(leaf, cascade.Compute(leaf, sheet));

        view.Should().BeOfType<Label>();
        view.Text.Should().Be("alpha");
    }

    [Fact]
    public void Container_node_projects_to_a_view_with_one_subview_per_child()
    {
        var table = Table(out var rows);
        var (cascade, sheet) = Build("Row { color: white; }");
        using var projection = new TerminalGuiProjection();

        var view = projection.Project(table, cascade.Compute(table, sheet));

        view.Should().NotBeOfType<Label>();
        view.Subviews.Should().HaveCount(rows.Length);
        projection.LiveViewCount.Should().Be(rows.Length + 1); // table + rows
    }

    [Fact]
    public void Reconcile_preserves_the_same_view_instance_for_an_unchanged_node()
    {
        var table = Table(out var rows);
        var (cascade, sheet) = Build("Row { color: white; }");
        using var projection = new TerminalGuiProjection();

        var result = cascade.Compute(table, sheet);
        var first = projection.Project(table, result);
        var firstRowView = first.Subviews[0];

        // Re-project against an equivalent cascade: the row's view object must be reused, not rebuilt,
        // so any focus living on it survives the re-cascade.
        var second = projection.Project(table, cascade.Compute(table, sheet));

        second.Should().BeSameAs(first);
        second.Subviews[0].Should().BeSameAs(firstRowView);
    }

    [Fact]
    public void Reconcile_attaches_a_view_for_a_newly_added_node()
    {
        var table = Table(out _, count: 2);
        var (cascade, sheet) = Build("Row { color: white; }");
        using var projection = new TerminalGuiProjection();

        projection.Project(table, cascade.Compute(table, sheet));

        var added = new RenderTestNode("Row", id: "r2", text: "row 2");
        table.Add(added);
        var view = projection.Project(table, cascade.Compute(table, sheet));

        view.Subviews.Should().HaveCount(3);
        projection.LiveViewCount.Should().Be(4); // table + 3 rows
    }

    [Fact]
    public void Reconcile_removes_and_disposes_the_view_of_a_removed_node()
    {
        var table = Table(out var rows, count: 3);
        var (cascade, sheet) = Build("Row { color: white; }");
        using var projection = new TerminalGuiProjection();

        var first = projection.Project(table, cascade.Compute(table, sheet));
        var removedView = first.Subviews[2];

        table.Remove(rows[2]);
        var view = projection.Project(table, cascade.Compute(table, sheet));

        view.Subviews.Should().HaveCount(2);
        view.Subviews.Should().NotContain(removedView);
        projection.LiveViewCount.Should().Be(3); // table + 2 surviving rows
    }

    [Fact]
    public void Update_refreshes_mutable_text_in_place_without_replacing_the_view()
    {
        var leaf = new RenderTestNode("Row", text: "before");
        var (cascade, sheet) = Build("Row { color: white; }");
        using var projection = new TerminalGuiProjection();

        var view = projection.Project(leaf, cascade.Compute(leaf, sheet));
        view.Text.Should().Be("before");

        leaf.Text = "after";
        var again = projection.Project(leaf, cascade.Compute(leaf, sheet));

        again.Should().BeSameAs(view);
        again.Text.Should().Be("after");
    }

    [Fact]
    public void Color_scheme_reflects_the_cascaded_foreground()
    {
        var leaf = new RenderTestNode("Row", text: "x");
        var (cascade, sheet) = Build("Row { color: #ff0000; }");
        using var projection = new TerminalGuiProjection();

        var view = projection.Project(leaf, cascade.Compute(leaf, sheet));

        view.ColorScheme.Normal.Foreground.Should().Be(new Terminal.Gui.Color(255, 0, 0));
    }

    [Fact]
    public void Re_cascade_after_focus_change_restyles_the_focused_row_in_the_same_view()
    {
        // The shared-engine guarantee: a :focused rule re-styles the row's existing view rather than
        // rebuilding it. Focus survives because the view instance is preserved (see reconcile test).
        var table = Table(out var rows);
        var (cascade, sheet) = Build(
            "Row { color: white; } Row:focused { color: #00ff00; }");
        using var projection = new TerminalGuiProjection();

        var first = projection.Project(table, cascade.Compute(table, sheet));
        var rowView = first.Subviews[0];
        rowView.ColorScheme.Normal.Foreground.Should().Be(new Terminal.Gui.Color(170, 170, 170)); // white

        rows[0].AddPseudoState("focused");
        var second = projection.Project(table, cascade.Compute(table, sheet));

        second.Subviews[0].Should().BeSameAs(rowView);
        rowView.ColorScheme.Normal.Foreground.Should().Be(new Terminal.Gui.Color(0, 255, 0));
    }

    [Fact]
    public void Project_after_dispose_throws()
    {
        var leaf = new RenderTestNode("Row", text: "x");
        var (cascade, sheet) = Build("Row { color: white; }");
        var projection = new TerminalGuiProjection();
        var result = cascade.Compute(leaf, sheet);
        projection.Dispose();

        var act = () => projection.Project(leaf, result);

        act.Should().Throw<ObjectDisposedException>();
    }

    // ---- Native Button widget ----

    [Fact]
    public void Button_kind_projects_to_a_native_button_view()
    {
        var button = new RenderTestNode("Button", text: "OK");
        var (cascade, sheet) = Build("Button { color: white; }");
        using var projection = new TerminalGuiProjection();

        var view = projection.Project(button, cascade.Compute(button, sheet));

        view.Should().BeOfType<Button>();
        view.Text.Should().Be("OK");
        view.CanFocus.Should().BeTrue("a button takes keyboard focus for activation");
    }

    [Fact]
    public void Button_text_updates_in_place_on_re_cascade()
    {
        var button = new RenderTestNode("Button", text: "OK");
        var (cascade, sheet) = Build("Button { color: white; }");
        using var projection = new TerminalGuiProjection();

        var first = projection.Project(button, cascade.Compute(button, sheet));
        button.Text = "Cancel";
        var second = projection.Project(button, cascade.Compute(button, sheet));

        second.Should().BeSameAs(first, "the button view is reconciled, not rebuilt");
        second.Text.Should().Be("Cancel");
    }

    // ---- Popup / modal / dialog ----

    [Fact]
    public void Dialog_kind_projects_to_a_modal_titled_window()
    {
        var dialog = new RenderTestNode(
            "Dialog",
            attributes: new Dictionary<string, object?> { ["Title"] = "Confirm" },
            text: "body");
        var (cascade, sheet) = Build("Dialog { color: white; }");
        using var projection = new TerminalGuiProjection();

        var view = projection.Project(dialog, cascade.Compute(dialog, sheet));

        view.Should().BeOfType<Window>();
        ((Window)view).Modal.Should().BeTrue("a dialog routes input to itself until dismissed");
        ((Window)view).Title?.ToString().Should().Contain("Confirm");
    }

    // ---- Z-index / overlay / floating layout ----

    [Fact]
    public void Z_index_orders_overlapping_siblings_back_to_front()
    {
        var root = new RenderTestNode("Panel");
        var low = new RenderTestNode("Cell", id: "low", text: "low");
        var high = new RenderTestNode("Cell", id: "high", text: "high");
        root.Add(low).Add(high);

        // Document order is low-then-high, but z-index 9 must lift "low" above "high" (z 1).
        var (cascade, sheet) = BuildWithLayout("#low { z-index: 9; } #high { z-index: 1; }");
        using var projection = new TerminalGuiProjection();

        var view = projection.Project(root, cascade.Compute(root, sheet));

        var lowView = ((RenderTestNode)low).Let(projection);
        var highView = ((RenderTestNode)high).Let(projection);
        var lowZ = view.Subviews.IndexOf(lowView);
        var highZ = view.Subviews.IndexOf(highView);
        lowZ.Should().BeGreaterThan(highZ, "the higher z-index sits frontmost in the subview order");
    }

    [Fact]
    public void Absolute_child_is_placed_at_its_top_left_inset()
    {
        var root = new RenderTestNode("Panel");
        var floatable = new RenderTestNode("Cell", id: "f", text: "note");
        root.Add(new RenderTestNode("Cell", text: "body")).Add(floatable);

        var (cascade, sheet) = BuildWithLayout("#f { position: absolute; top: 3; left: 2; }");
        using var projection = new TerminalGuiProjection();

        projection.Project(root, cascade.Compute(root, sheet));

        projection.TryGetView(floatable, out var view).Should().BeTrue();
        view.X.Should().Be(Pos.Absolute(2));
        view.Y.Should().Be(Pos.Absolute(3));
    }

    private static (Cascade cascade, IStylesheet sheet) BuildWithLayout(string css)
    {
        var props = StylingProperties.CreateRegistry();
        LayoutProperties.RegisterAll(props);
        var parser = new CssStylesheetParser(new CssSelectorLanguage(), props);
        return (new Cascade(props), parser.Parse(css));
    }
}

internal static class ProjectionTestExtensions
{
    /// <summary>Resolve the live view for a node, asserting it exists.</summary>
    public static View Let(this RenderTestNode node, TerminalGuiProjection projection)
    {
        if (!projection.TryGetView(node, out var view))
        {
            throw new InvalidOperationException($"No view projected for node '{node.Kind}'.");
        }

        return view;
    }
}
