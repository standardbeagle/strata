using System.Collections.Immutable;

namespace Strata.Interaction;

/// <summary>
/// A single stylesheet-declared command binding: a command name paired with the event name that
/// triggers it. Parsed from a <c>command: "name" when "event"</c> declaration.
/// </summary>
/// <param name="Command">The command name dispatched to <see cref="ICommandRegistry"/>.</param>
/// <param name="Event">The triggering event name (e.g. <c>key.j</c>, <c>focus</c>, <c>tick</c>).</param>
public readonly record struct CommandBinding(string Command, string Event);

/// <summary>
/// The value of a <c>command:</c> declaration: an ordered list of <see cref="CommandBinding"/>
/// pairs.
/// </summary>
/// <remarks>
/// The <c>command:</c> property has <b>additive</b> cascade semantics — a deliberate deviation
/// from CSS override (documented in <c>docs/05-interaction-redesign.md</c> §2.3 and the original
/// <c>behavior:</c> design in <c>docs/03-tech-design.md</c> §12 Q2). Every matched rule that
/// declares <c>command:</c> contributes its bindings; none overrides another. The additive merge
/// happens at the interaction layer (<see cref="InteractionHost"/>), which reads bindings from
/// <see cref="ICascadeResult.GetMatchedRules"/> rather than the single cascade winner.
/// </remarks>
public readonly record struct CommandValue(ImmutableArray<CommandBinding> Bindings) : IPropertyValue
{
    /// <summary>An empty binding list.</summary>
    public static CommandValue Empty { get; } = new(ImmutableArray<CommandBinding>.Empty);

    /// <inheritdoc/>
    public Type Type => typeof(ImmutableArray<CommandBinding>);
}

/// <summary>
/// Descriptor for the <c>command:</c> property. Parses <c>"command-name" when "event-name"</c>
/// items (comma-separated within one declaration are equivalent to separate declarations).
/// </summary>
/// <remarks>
/// The property never inherits and has no meaningful cascade winner — the descriptor exists so the
/// stylesheet parser can validate and type the value; the additive merge is the host's job.
/// </remarks>
public sealed class CommandPropertyDescriptor : IPropertyDescriptor
{
    /// <summary>The canonical property name.</summary>
    public const string PropertyName = "command";

    private const string WhenKeyword = "when";

    /// <inheritdoc/>
    public string Name => PropertyName;

    /// <inheritdoc/>
    public Type ValueType => typeof(ImmutableArray<CommandBinding>);

    /// <inheritdoc/>
    public bool Inherits => false;

    /// <inheritdoc/>
    public IPropertyValue Initial => CommandValue.Empty;

    /// <inheritdoc/>
    public IPropertyValue Parse(ReadOnlySpan<char> source)
    {
        var text = source.Trim();
        if (text.IsEmpty)
        {
            return CommandValue.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<CommandBinding>();
        foreach (var range in SplitTopLevelCommas(text))
        {
            var item = text[range].Trim();
            if (item.IsEmpty)
            {
                throw new FormatException(
                    $"Empty command item in '{PropertyName}' declaration.");
            }

            builder.Add(ParseItem(item));
        }

        return new CommandValue(builder.ToImmutable());
    }

    private static CommandBinding ParseItem(ReadOnlySpan<char> item)
    {
        // Form: "command-name" when "event-name"
        var command = ReadQuoted(item, out var afterCommand);

        var rest = item[afterCommand..].TrimStart();
        if (!rest.StartsWith(WhenKeyword, StringComparison.Ordinal)
            || (rest.Length > WhenKeyword.Length && !char.IsWhiteSpace(rest[WhenKeyword.Length])))
        {
            throw new FormatException(
                $"Expected '{WhenKeyword}' after command name in '{item.ToString()}'. " +
                "Form is: command: \"name\" when \"event\".");
        }

        rest = rest[WhenKeyword.Length..].TrimStart();
        var eventName = ReadQuoted(rest, out var afterEvent);

        var trailing = rest[afterEvent..].Trim();
        if (!trailing.IsEmpty)
        {
            throw new FormatException(
                $"Unexpected trailing text '{trailing.ToString()}' in command item '{item.ToString()}'.");
        }

        return new CommandBinding(command, eventName);
    }

    private static string ReadQuoted(ReadOnlySpan<char> span, out int consumed)
    {
        var s = span.TrimStart();
        var leading = span.Length - s.Length;
        if (s.IsEmpty || (s[0] != '"' && s[0] != '\''))
        {
            throw new FormatException(
                $"Expected a quoted string at '{span.ToString()}'.");
        }

        var quote = s[0];
        var end = s[1..].IndexOf(quote);
        if (end < 0)
        {
            throw new FormatException(
                $"Unterminated quoted string at '{span.ToString()}'.");
        }

        var value = s.Slice(1, end).ToString();
        if (value.Length == 0)
        {
            throw new FormatException("Quoted string in command declaration must be non-empty.");
        }

        // leading trim + opening quote + content + closing quote.
        consumed = leading + 1 + end + 1;
        return value;
    }

    private static List<Range> SplitTopLevelCommas(ReadOnlySpan<char> text)
    {
        var ranges = new List<Range>();
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '"' || c == '\'')
            {
                // Skip over quoted regions so commas inside quotes are not split points.
                var close = text[(i + 1)..].IndexOf(c);
                i = close < 0 ? text.Length : i + 1 + close + 1;
                continue;
            }

            if (c == ',')
            {
                ranges.Add(start..i);
                start = i + 1;
            }

            i++;
        }

        ranges.Add(start..text.Length);
        return ranges;
    }
}
