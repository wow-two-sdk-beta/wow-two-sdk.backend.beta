namespace WoW.Two.Sdk.Backend.Beta.Foundation.Identifiers.Generators;

/// <summary>Defines generation of random identifiers from a caller-selected length and alphabet.</summary>
public interface IIdGenerator
{
    /// <summary>Generates an identifier; storage uniqueness and collision retries remain caller-owned.</summary>
    string Generate(int length, string alphabet = IdAlphabetConstants.Alphanumeric);
}
