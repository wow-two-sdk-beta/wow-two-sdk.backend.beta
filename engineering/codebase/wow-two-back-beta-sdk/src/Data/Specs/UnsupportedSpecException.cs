namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>Represents spec features a mapper's backend cannot express, under <see cref="UnsupportedSpecMode.Throw"/>.</summary>
public sealed class UnsupportedSpecException : Exception
{
    /// <summary>Creates the exception for <paramref name="mapper"/> and the features it cannot express.</summary>
    /// <param name="mapper">The mapper, such as <c>EntityFrameworkCore</c>.</param>
    /// <param name="features">Each unsupported feature, described.</param>
    public UnsupportedSpecException(string mapper, IReadOnlyList<string> features)
        : base($"{mapper} cannot map: {string.Join("; ", features ?? [])}. Set Data:Specs:Unsupported to Skip to leave them out.")
    {
        ArgumentNullException.ThrowIfNull(features);
        Mapper = mapper;
        Features = features;
    }

    /// <summary>Creates the exception with a default message.</summary>
    public UnsupportedSpecException()
        : this("mapper", [])
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public UnsupportedSpecException(string message)
        : base(message)
    {
        Mapper = string.Empty;
        Features = [];
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public UnsupportedSpecException(string message, Exception innerException)
        : base(message, innerException)
    {
        Mapper = string.Empty;
        Features = [];
    }

    /// <summary>The mapper.</summary>
    public string Mapper { get; }

    /// <summary>Each unsupported feature, described.</summary>
    public IReadOnlyList<string> Features { get; }
}
