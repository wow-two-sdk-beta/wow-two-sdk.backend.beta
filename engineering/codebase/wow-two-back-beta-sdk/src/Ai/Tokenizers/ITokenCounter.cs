namespace WoW.Two.Sdk.Backend.Beta.Ai.Tokenizers;

/// <summary>Counts model tokens in text — for prompt-budget checks and cost estimation before a call.</summary>
public interface ITokenCounter
{
    /// <summary>Returns the number of tokens <paramref name="text"/> encodes to for the configured model.</summary>
    /// <param name="text">The text to measure.</param>
    /// <returns>The token count.</returns>
    int Count(string text);
}
