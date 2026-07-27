using WoW.Two.Sdk.Backend.Beta.Codes.Models;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Matrix;

/// <summary>Defines the contract for producing the bare QR module matrix (no quiet zone) for a payload at a given ECC level.</summary>
public interface IQrMatrixGenerator
{
    /// <summary>Encodes <paramref name="payload"/> and returns its module grid with the quiet zone stripped.</summary>
    /// <param name="payload">The data to encode.</param>
    /// <param name="ecc">The error-correction level to encode at.</param>
    ModuleMatrix Generate(string payload, EccLevel ecc);
}
