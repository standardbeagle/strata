namespace Strata.Interaction;

/// <summary>
/// A node whose dynamic pseudo-states can be toggled at runtime. The base
/// <see cref="ITreeNode"/> exposes <see cref="ITreeNode.PseudoStates"/> as read-only because the
/// engine never mutates them — they are "toggled by adapters at runtime" (see <c>Tree.cs</c>).
/// The interaction controllers (<see cref="FocusController"/>, <see cref="SelectionController"/>)
/// drive that toggle through this contract, keeping the redesign's
/// <c>:focused</c>/<c>:selected</c>/<c>:hovered</c>/<c>:expanded</c> attribute-style model
/// (<c>docs/05-interaction-redesign.md</c> line 41) intact.
/// </summary>
/// <remarks>
/// A toggle that changes nothing (adding a state already present, or removing one absent) returns
/// <see langword="false"/> so callers can skip emitting a redundant
/// <see cref="TreeChange.PseudoStateChanged"/> and avoid a spurious re-cascade.
/// </remarks>
public interface IPseudoStateMutable
{
    /// <summary>
    /// Add <paramref name="state"/> to the node's active pseudo-states.
    /// </summary>
    /// <returns><see langword="true"/> if the state was newly added; otherwise <see langword="false"/>.</returns>
    bool AddPseudoState(string state);

    /// <summary>
    /// Remove <paramref name="state"/> from the node's active pseudo-states.
    /// </summary>
    /// <returns><see langword="true"/> if the state was present and removed; otherwise <see langword="false"/>.</returns>
    bool RemovePseudoState(string state);
}
