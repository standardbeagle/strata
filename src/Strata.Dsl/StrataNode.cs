namespace Strata.Dsl;

/// <summary>
/// Factory the PowerShell DSL calls to build <see cref="StrataElement"/> nodes. Keeping a
/// static entry point gives the `.psm1` functions a single, stable call shape and normalizes a
/// blank id to <see langword="null"/>.
/// </summary>
public static class StrataNode
{
    /// <summary>Build a detached <see cref="StrataElement"/>; the caller wires children via Add.</summary>
    public static StrataElement Create(
        string kind,
        string? id = null,
        string[]? classes = null,
        IDictionary<string, object?>? attributes = null)
        => new(kind, string.IsNullOrWhiteSpace(id) ? null : id, classes, attributes);
}
