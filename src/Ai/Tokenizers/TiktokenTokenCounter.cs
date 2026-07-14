using Microsoft.ML.Tokenizers;

namespace WoW.Two.Sdk.Backend.Beta.Ai.Tokenizers;

/// <summary>Default <see cref="ITokenCounter"/> over a <see cref="Tokenizer"/> (e.g. tiktoken for OpenAI models). Thread-safe.</summary>
public sealed class TiktokenTokenCounter : ITokenCounter
{
    private readonly Tokenizer _tokenizer;

    /// <summary>Creates the counter over a configured tokenizer.</summary>
    /// <param name="tokenizer">The tokenizer to count with.</param>
    public TiktokenTokenCounter(Tokenizer tokenizer)
    {
        ArgumentNullException.ThrowIfNull(tokenizer);
        _tokenizer = tokenizer;
    }

    /// <inheritdoc />
    public int Count(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _tokenizer.CountTokens(text);
    }
}
