namespace WoW.Two.Sdk.Backend.Beta.Foundation.Configuration;

/// <summary>Marks a settings property to be overlaid from an environment variable by <see cref="ConfigurationLoader"/>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EnvironmentVariableAttribute : Attribute
{
    /// <summary>Initializes the attribute for the given environment variable.</summary>
    /// <param name="name">The environment variable name to read the value from.</param>
    /// <param name="required">Whether the value must be present after binding and env overlay.</param>
    public EnvironmentVariableAttribute(string name, bool required = false)
    {
        Name = name;
        Required = required;
    }

    /// <summary>Gets the environment variable name to read.</summary>
    public string Name { get; }

    /// <summary>Gets a value indicating whether the value must be present after binding and env overlay (throws if missing).</summary>
    public bool Required { get; }
}
