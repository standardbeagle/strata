namespace Strata.Core.Tests.TestFixtures;

/// <summary>Trivial string-backed property value for tests.</summary>
internal readonly record struct StringValue(string Text) : IPropertyValue
{
    public Type Type => typeof(string);
}

/// <summary>Trivial string-typed property descriptor for tests.</summary>
internal sealed class StringPropertyDescriptor : IPropertyDescriptor
{
    public StringPropertyDescriptor(string name, string initial, bool inherits)
    {
        Name = name;
        Initial = new StringValue(initial);
        Inherits = inherits;
    }

    public string Name { get; }

    public Type ValueType => typeof(string);

    public bool Inherits { get; }

    public IPropertyValue Initial { get; }

    public IPropertyValue Parse(ReadOnlySpan<char> source) => new StringValue(source.ToString());
}
