using System.Diagnostics.CodeAnalysis;
using Strata.Css.Expressions;

namespace Strata.Css;

/// <summary>
/// Opt-in switch for the <c>[expr]</c> typed-predicate evaluator.
/// </summary>
/// <remarks>
/// The <c>[expr]</c> form (e.g. <c>Process[CPU &gt; 50 and Name.StartsWith("chr")]</c>) is
/// evaluated by a reflection-based tree-walking interpreter. Reflection over arbitrary
/// source types is not trim-safe, so the evaluator is <b>off by default</b>: a stylesheet
/// that uses <c>[expr]</c> will throw at match time until <see cref="Enable"/> is called.
///
/// <para>This keeps <c>Strata.Css</c> fully AOT- and trim-clean for the common case
/// (plain selectors, attribute matchers, pseudo-classes). Callers that genuinely need
/// typed predicates call <see cref="Enable"/> once at startup and accept the
/// <see cref="RequiresUnreferencedCodeAttribute"/> contract — at which point the trimmer
/// keeps <see cref="ExprEvaluator"/> and the reflection it relies on.</para>
/// </remarks>
public static class CssPredicates
{
    internal static Func<ExprNode, object, bool>? Evaluator;

    /// <summary>True once <see cref="Enable"/> has been called.</summary>
    public static bool IsEnabled => Evaluator is not null;

    /// <summary>
    /// Enable reflection-based evaluation of <c>[expr]</c> typed predicates. Call once at
    /// application startup. Idempotent.
    /// </summary>
    [RequiresUnreferencedCode(
        "Enables the [expr] predicate evaluator, which reflects over arbitrary source " +
        "types. Preserve referenced members via DynamicallyAccessedMembers on the source " +
        "type, or avoid [expr] selectors in trimmed/AOT builds.")]
    public static void Enable()
    {
        Evaluator = static (ast, target) => new ExprEvaluator(ast).EvaluateBool(target);
    }
}
