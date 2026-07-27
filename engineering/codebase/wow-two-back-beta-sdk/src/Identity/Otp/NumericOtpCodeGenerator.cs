using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Default <see cref="IOtpCodeGenerator"/> — cryptographically random digits, <see cref="OtpOptions.CodeLength"/> long (leading zeros included).</summary>
public sealed class NumericOtpCodeGenerator : IOtpCodeGenerator
{
    private readonly int _length;

    /// <summary>Creates the generator from configured options.</summary>
    /// <param name="options">Source of <see cref="OtpOptions.CodeLength"/>.</param>
    public NumericOtpCodeGenerator(IOptions<OtpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _length = options.Value.CodeLength;
        ArgumentOutOfRangeException.ThrowIfLessThan(_length, 4, nameof(options));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(_length, 10, nameof(options));
    }

    /// <inheritdoc />
    public string Generate()
    {
        Span<char> digits = stackalloc char[_length];
        for (var i = 0; i < digits.Length; i++)
        {
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        return new string(digits);
    }
}
