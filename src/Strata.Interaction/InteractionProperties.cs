using Strata.Core;

namespace Strata.Interaction;

/// <summary>
/// Registers the interaction property set (currently just <c>command:</c>) into an
/// <see cref="IPropertyRegistry"/> so a stylesheet that declares commands can be parsed.
/// </summary>
public static class InteractionProperties
{
    /// <inheritdoc cref="CommandPropertyDescriptor.PropertyName"/>
    public const string Command = CommandPropertyDescriptor.PropertyName;

    /// <summary>Register every interaction descriptor into <paramref name="registry"/>.</summary>
    public static void RegisterAll(IPropertyRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new CommandPropertyDescriptor());
    }

    /// <summary>Create a fresh registry pre-populated with every interaction descriptor.</summary>
    public static IPropertyRegistry CreateRegistry()
    {
        var registry = new PropertyRegistry();
        RegisterAll(registry);
        return registry;
    }
}
