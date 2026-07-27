using QRCoder;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Matrix;

/// <summary>Provides a QRCoder-backed <see cref="IQrMatrixGenerator"/>, stripping QRCoder's baked quiet border so the emitter owns the quiet zone.</summary>
public sealed class QrMatrixGenerator : IQrMatrixGenerator
{
    /// <summary>The quiet-zone width in modules per side that QRCoder bakes into its matrix.</summary>
    private const int QrCoderBakedQuietZone = 4;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The QRCoder matrix is smaller than the baked quiet border.</exception>
    public ModuleMatrix Generate(string payload, EccLevel ecc)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(payload, Map(ecc));
        var rows = data.ModuleMatrix;

        var full = rows.Count;
        var inner = full - (2 * QrCoderBakedQuietZone);
        if (inner <= 0)
            throw new InvalidOperationException($"Unexpected QRCoder matrix size {full}; cannot strip the {QrCoderBakedQuietZone}-module quiet border.");

        var modules = new bool[inner, inner];
        for (var r = 0; r < inner; r++)
        {
            var srcRow = rows[r + QrCoderBakedQuietZone];
            for (var c = 0; c < inner; c++)
                modules[r, c] = srcRow[c + QrCoderBakedQuietZone];
        }

        return new ModuleMatrix(modules);
    }

    private static QRCodeGenerator.ECCLevel Map(EccLevel level) => level switch
    {
        EccLevel.L => QRCodeGenerator.ECCLevel.L,
        EccLevel.M => QRCodeGenerator.ECCLevel.M,
        EccLevel.Q => QRCodeGenerator.ECCLevel.Q,
        EccLevel.H => QRCodeGenerator.ECCLevel.H,
        _ => QRCodeGenerator.ECCLevel.Q,
    };
}
