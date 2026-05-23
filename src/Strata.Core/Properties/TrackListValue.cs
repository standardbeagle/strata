using System.Collections.Immutable;

namespace Strata.Core.Properties;

/// <summary>
/// A grid track list: the space-separated sizes for <c>grid-template-columns</c> or
/// <c>grid-template-rows</c> (e.g. <c>10 1fr 20%</c>). Each entry is a <see cref="LengthValue"/>,
/// so cells, <c>fr</c>, <c>%</c>, and <c>auto</c> are all expressible per track.
/// </summary>
public readonly record struct TrackListValue(ImmutableArray<LengthValue> Tracks) : IPropertyValue
{
    /// <summary>An empty track list (no explicit grid template).</summary>
    public static TrackListValue Empty { get; } = new(ImmutableArray<LengthValue>.Empty);

    /// <inheritdoc/>
    public Type Type => typeof(ImmutableArray<LengthValue>);
}

/// <summary>
/// Descriptor for a grid track-list property. Parses space-separated length tokens, reusing
/// the <see cref="LengthValue"/> grammar (cells, <c>%</c>, <c>fr</c>, <c>auto</c>) for each.
/// </summary>
public sealed class TrackListPropertyDescriptor : IPropertyDescriptor
{
    // A throwaway length descriptor purely to reuse its single-token parse grammar.
    private readonly LengthPropertyDescriptor _trackParser;

    /// <summary>Create a track-list descriptor with an empty initial value.</summary>
    public TrackListPropertyDescriptor(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        _trackParser = new LengthPropertyDescriptor(name, LengthValue.Auto, inherits: false);
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public Type ValueType => typeof(ImmutableArray<LengthValue>);

    /// <inheritdoc/>
    public bool Inherits => false;

    /// <inheritdoc/>
    public IPropertyValue Initial => TrackListValue.Empty;

    /// <inheritdoc/>
    public IPropertyValue Parse(ReadOnlySpan<char> source)
    {
        var text = source.Trim();
        if (text.IsEmpty || text.SequenceEqual("none"))
        {
            return TrackListValue.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<LengthValue>();
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            if (i >= text.Length)
            {
                break;
            }

            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            var token = text[start..i];
            builder.Add((LengthValue)_trackParser.Parse(token));
        }

        return new TrackListValue(builder.ToImmutable());
    }
}
