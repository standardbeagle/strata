namespace Strata.Layout.Yoga.Tests;

using Strata.Core;
using Strata.Css;
using Strata.Properties.Styling;

public sealed class YogaLayoutPassTests
{
    private static ICascadeResult Style(string css, ITreeNode root)
    {
        var registry = StylingProperties.CreateRegistry();
        LayoutProperties.RegisterAll(registry);
        var parser = new CssStylesheetParser(new CssSelectorLanguage(), registry);
        var stylesheet = parser.Parse(css);
        return new Cascade(registry).Compute(root, stylesheet);
    }

    [Fact]
    public void Flex_row_splits_available_width_between_grow_children()
    {
        var root = new LayoutTestNode("Row", id: "row")
            .Add(new LayoutTestNode("Cell", id: "a"))
            .Add(new LayoutTestNode("Cell", id: "b"));

        var cascade = Style(
            """
            #row { display: flex; flex-direction: row; }
            Cell { flex-grow: 1; }
            """, root);

        var result = YogaLayoutPass.Compute(root, cascade, new Size(30, 3));

        var a = result.GetRect(root.Children.First());
        var b = result.GetRect(root.Children.Last());

        a.X.Should().Be(0);
        a.Width.Should().Be(15);
        b.X.Should().Be(15);
        b.Width.Should().Be(15);
        a.Right.Should().Be(b.X, "adjacent boxes must share an exact integer boundary (no sub-cell drift)");
    }

    [Fact]
    public void Fixed_width_child_takes_its_declared_cells()
    {
        var root = new LayoutTestNode("Row")
            .Add(new LayoutTestNode("Cell", id: "fixed"))
            .Add(new LayoutTestNode("Cell", id: "rest"));

        var cascade = Style(
            """
            Row { display: flex; flex-direction: row; }
            #fixed { width: 8; }
            #rest { flex-grow: 1; }
            """, root);

        var result = YogaLayoutPass.Compute(root, cascade, new Size(20, 1));

        result.GetRect(root.Children.First()).Width.Should().Be(8);
        result.GetRect(root.Children.Last()).Width.Should().Be(12);
    }

    [Fact]
    public void Grid_template_columns_places_cells_at_track_offsets()
    {
        var root = new LayoutTestNode("Grid", id: "grid")
            .Add(new LayoutTestNode("Cell"))
            .Add(new LayoutTestNode("Cell"))
            .Add(new LayoutTestNode("Cell"))
            .Add(new LayoutTestNode("Cell"));

        // Two columns (10, 20 cells), implicit two rows.
        var cascade = Style(
            """
            #grid { display: grid; grid-template-columns: 10 20; grid-template-rows: 2 2; width: 30; height: 4; }
            """, root);

        var result = YogaLayoutPass.Compute(root, cascade, new Size(30, 4));
        var cells = root.Children.ToList();

        // Row-major fill: c0=(0,0) c1=(10,0) c2=(0,2) c3=(10,2).
        result.GetRect(cells[0]).X.Should().Be(0);
        result.GetRect(cells[1]).X.Should().Be(10);
        result.GetRect(cells[1]).Width.Should().Be(20);
        result.GetRect(cells[2]).Y.Should().Be(2);
        result.GetRect(cells[3]).X.Should().Be(10);
        result.GetRect(cells[3]).Y.Should().Be(2);
    }

    [Fact]
    public void Fractional_widths_round_to_cells_without_drift()
    {
        // 3 equal flex children over 10 cells => 3.33 each. Edges must round so the row
        // remains exactly 10 cells wide with no gaps or overlaps.
        var root = new LayoutTestNode("Row")
            .Add(new LayoutTestNode("Cell"))
            .Add(new LayoutTestNode("Cell"))
            .Add(new LayoutTestNode("Cell"));

        var cascade = Style(
            """
            Row { display: flex; flex-direction: row; }
            Cell { flex-grow: 1; }
            """, root);

        var result = YogaLayoutPass.Compute(root, cascade, new Size(10, 1));
        var cells = root.Children.ToList();

        var r0 = result.GetRect(cells[0]);
        var r1 = result.GetRect(cells[1]);
        var r2 = result.GetRect(cells[2]);

        r0.X.Should().Be(0);
        r1.X.Should().Be(r0.Right, "no gap between cell 0 and cell 1");
        r2.X.Should().Be(r1.Right, "no gap between cell 1 and cell 2");
        r2.Right.Should().Be(10, "the row spans exactly the available width");
    }

    [Fact]
    public void Absolute_positioned_child_uses_inset_offsets()
    {
        var root = new LayoutTestNode("Panel", id: "panel")
            .Add(new LayoutTestNode("Float", id: "float"));

        var cascade = Style(
            """
            #panel { display: flex; width: 40; height: 10; }
            #float { position: absolute; top: 2; left: 5; width: 6; height: 1; }
            """, root);

        var result = YogaLayoutPass.Compute(root, cascade, new Size(40, 10));
        var floatRect = result.GetRect(root.Children.First());

        floatRect.X.Should().Be(5);
        floatRect.Y.Should().Be(2);
        floatRect.Width.Should().Be(6);
        floatRect.Height.Should().Be(1);
    }

    [Fact]
    public void Gap_inserts_space_between_flex_items()
    {
        var root = new LayoutTestNode("Row")
            .Add(new LayoutTestNode("Cell", id: "a"))
            .Add(new LayoutTestNode("Cell", id: "b"));

        var cascade = Style(
            """
            Row { display: flex; flex-direction: row; column-gap: 2; }
            #a { width: 4; }
            #b { width: 4; }
            """, root);

        var result = YogaLayoutPass.Compute(root, cascade, new Size(20, 1));

        result.GetRect(root.Children.First()).Right.Should().Be(4);
        result.GetRect(root.Children.Last()).X.Should().Be(6, "a 2-cell gap separates the two 4-cell items");
    }

    [Fact]
    public void Nested_children_get_absolute_coordinates()
    {
        var inner = new LayoutTestNode("Cell", id: "inner");
        var root = new LayoutTestNode("Outer", id: "outer")
            .Add(new LayoutTestNode("Mid", id: "mid").Add(inner));

        var cascade = Style(
            """
            #outer { display: flex; flex-direction: column; padding: 1; width: 20; height: 10; }
            #mid { display: flex; flex-direction: column; flex-grow: 1; }
            #inner { flex-grow: 1; }
            """, root);

        var result = YogaLayoutPass.Compute(root, cascade, new Size(20, 10));

        // outer padding=1 pushes mid (and thus inner) to (1,1) absolute.
        result.GetRect(inner).X.Should().Be(1);
        result.GetRect(inner).Y.Should().Be(1);
    }

    [Fact]
    public void Tree_with_no_layout_properties_is_trivial()
    {
        var root = new LayoutTestNode("Window")
            .Add(new LayoutTestNode("Process"))
            .Add(new LayoutTestNode("Process"));

        var cascade = Style("Process { color: red; }", root);

        var result = YogaLayoutPass.Compute(root, cascade, new Size(80, 24));

        result.Trivial.Should().BeTrue();
    }

    [Fact]
    public void Display_declaration_makes_layout_non_trivial()
    {
        var root = new LayoutTestNode("Window")
            .Add(new LayoutTestNode("Process"));

        var cascade = Style("Window { display: flex; }", root);

        YogaLayoutPass.Compute(root, cascade, new Size(80, 24)).Trivial.Should().BeFalse();
    }
}
