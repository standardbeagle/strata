namespace Strata.Dsl.TerminalGui;

/// <summary>
/// The context handed to a widget behavior callback (from <c>-OnSelect</c> / <c>-OnChange</c> /
/// <c>-OnEnter</c>): the reactive store, the element that fired, and the relevant value (field text
/// or selected list item).
/// </summary>
/// <param name="Store">The reactive store driving the app.</param>
/// <param name="Element">The DSL element whose widget raised the event.</param>
/// <param name="Value">Field text, selected item, or <see langword="null"/>.</param>
public sealed record StrataUiEvent(StrataStore Store, StrataElement Element, object? Value);
