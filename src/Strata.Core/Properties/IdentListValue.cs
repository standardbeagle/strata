using System.Collections.Immutable;

namespace Strata.Core.Properties;

/// <summary>
/// A comma-separated identifier list. Used for properties like <c>behavior: kill, meter</c>
/// where multiple named items contribute additively rather than overriding.
/// </summary>
public readonly record struct IdentListValue(ImmutableArray<string> Idents) : IPropertyValue
{
    /// <summary>Empty list.</summary>
    public static IdentListValue Empty { get; } = new(ImmutableArray<string>.Empty);

    /// <inheritdoc/>
    public Type Type => typeof(ImmutableArray<string>);
}

/// <summary>Descriptor for an ident-list property.</summary>
public sealed class IdentListPropertyDescriptor : IPropertyDescriptor
{
    /// <summary>Create an ident-list descriptor with an empty initial value.</summary>
    public IdentListPropertyDescriptor(string name, bool inherits = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Inherits = inherits;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public Type ValueType => typeof(ImmutableArray<string>);

    /// <inheritdoc/>
    public bool Inherits { get; }

    /// <inheritdoc/>
    public IPropertyValue Initial => IdentListValue.Empty;

    /// <inheritdoc/>
    public IPropertyValue Parse(ReadOnlySpan<char> source)
    {
        var text = source.Trim();
        if (text.IsEmpty)
        {
            return IdentListValue.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<string>();
        var i = 0;
        while (i < text.Length)
        {
            // Skip whitespace.
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            if (i >= text.Length)
            {
                break;
            }

            var start = i;
            while (i < text.Length && text[i] != ',')
            {
                i++;
            }

            var ident = text[start..i].ToString().Trim();
            if (ident.Length == 0)
            {
                throw new FormatException($"Empty identifier in list for property '{Name}'.");
            }

            builder.Add(ident);

            if (i < text.Length && text[i] == ',')
            {
                i++;
            }
        }

        return new IdentListValue(builder.ToImmutable());
    }
}
