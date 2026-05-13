namespace Strata;

/// <summary>
/// Consumes a styled tree and produces a target output. Projections are pure with respect
/// to <c>(root, cascade)</c>: two invocations with the same inputs MUST produce equivalent
/// outputs. Side effects (e.g. behavior attachment) belong to the behavior lifecycle, not
/// to projection.
/// </summary>
/// <typeparam name="TOutput">The output value space (e.g. <c>Spectre.Console.IRenderable</c>,
/// <c>Terminal.Gui.View</c>, a GraphQL resolver descriptor, a React element).</typeparam>
public interface IProjection<out TOutput>
{
    /// <summary>Project the styled tree rooted at <paramref name="root"/> into <typeparamref name="TOutput"/>.</summary>
    TOutput Project(ITreeNode root, ICascadeResult cascade);
}
