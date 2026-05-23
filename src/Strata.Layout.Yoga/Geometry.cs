namespace Strata.Layout.Yoga;

/// <summary>
/// An available layout area in integer terminal cells. The unit throughout the layout pass is
/// terminal cells, matching Yoga's pixel unit one-to-one.
/// </summary>
/// <param name="Width">Available width in cells. Use a negative value to mean "unbounded".</param>
/// <param name="Height">Available height in cells. Use a negative value to mean "unbounded".</param>
public readonly record struct Size(int Width, int Height)
{
    /// <summary>An unbounded size on both axes (Yoga computes intrinsic dimensions).</summary>
    public static Size Unbounded { get; } = new(-1, -1);

    /// <summary>Whether the given axis length is unbounded (negative).</summary>
    public bool IsWidthBounded => Width >= 0;

    /// <inheritdoc cref="IsWidthBounded"/>
    public bool IsHeightBounded => Height >= 0;
}

/// <summary>
/// A computed box rectangle in integer terminal cells. Coordinates are absolute within the
/// laid-out root: <see cref="X"/>/<see cref="Y"/> are the cell offset from the root's origin.
/// </summary>
/// <param name="X">Left edge, cells from root origin.</param>
/// <param name="Y">Top edge, cells from root origin.</param>
/// <param name="Width">Box width in cells.</param>
/// <param name="Height">Box height in cells.</param>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    /// <summary>The zero rectangle at the origin.</summary>
    public static Rect Empty { get; }

    /// <summary>Right edge (exclusive): <see cref="X"/> + <see cref="Width"/>.</summary>
    public int Right => X + Width;

    /// <summary>Bottom edge (exclusive): <see cref="Y"/> + <see cref="Height"/>.</summary>
    public int Bottom => Y + Height;

    /// <summary>Whether this rectangle encloses no cells.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
