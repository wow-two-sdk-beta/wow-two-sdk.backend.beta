using System.Buffers.Binary;
using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.Accounts;

/// <summary>
/// A passkey authenticator in software: an ES256 key, "none" attestation, and the WebAuthn JSON a browser's
/// <c>navigator.credentials</c> returns through <c>toJSON()</c>.
/// </summary>
internal sealed class SoftwareAuthenticator : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly bool _countsSignatures;
    private byte[] _userHandle = [];

    /// <param name="countsSignatures">False behaves like synced passkeys, which always report counter 0.</param>
    /// <param name="credentialId">Reuses another authenticator's credential id, to forge its assertions.</param>
    public SoftwareAuthenticator(bool countsSignatures = true, byte[]? credentialId = null)
    {
        _countsSignatures = countsSignatures;
        CredentialId = credentialId ?? RandomNumberGenerator.GetBytes(16);
    }

    public byte[] CredentialId { get; }

    public string Origin { get; set; } = "https://app.test";

    public uint SignCount { get; private set; }

    /// <summary>Answers creation options with a new credential.</summary>
    public string Create(string optionsJson)
    {
        var options = JsonNode.Parse(optionsJson)!;
        _userHandle = Base64Url.DecodeFromChars(options["user"]!["id"]!.GetValue<string>());
        byte[] authenticatorData =
        [
            .. RpIdHash(options["rp"]!["id"]!.GetValue<string>()),
            0x45, // user present, user verified, attested credential data
            .. Counter(advance: false),
            .. new byte[16], // AAGUID
            .. BigEndian((ushort)CredentialId.Length),
            .. CredentialId,
            .. CoseKey(),
        ];

        var attestation = new CborWriter(CborConformanceMode.Ctap2Canonical);
        attestation.WriteStartMap(3);
        attestation.WriteTextString("fmt");
        attestation.WriteTextString("none");
        attestation.WriteTextString("attStmt");
        attestation.WriteStartMap(0);
        attestation.WriteEndMap();
        attestation.WriteTextString("authData");
        attestation.WriteByteString(authenticatorData);
        attestation.WriteEndMap();

        return JsonSerializer.Serialize(new
        {
            id = Encode(CredentialId),
            rawId = Encode(CredentialId),
            type = "public-key",
            response = new
            {
                attestationObject = Encode(attestation.Encode()),
                clientDataJSON = Encode(ClientData("webauthn.create", options["challenge"]!.GetValue<string>())),
            },
            clientExtensionResults = new { },
        });
    }

    /// <summary>Answers request options with a signed assertion.</summary>
    public string Get(string optionsJson)
    {
        var options = JsonNode.Parse(optionsJson)!;
        byte[] authenticatorData = [.. RpIdHash(options["rpId"]!.GetValue<string>()), 0x05, .. Counter(advance: true)];
        var clientData = ClientData("webauthn.get", options["challenge"]!.GetValue<string>());
        var signature = _key.SignData([.. authenticatorData, .. SHA256.HashData(clientData)], HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return JsonSerializer.Serialize(new
        {
            id = Encode(CredentialId),
            rawId = Encode(CredentialId),
            type = "public-key",
            response = new
            {
                authenticatorData = Encode(authenticatorData),
                signature = Encode(signature),
                clientDataJSON = Encode(clientData),
                userHandle = Encode(_userHandle),
            },
            clientExtensionResults = new { },
        });
    }

    public void Dispose() => _key.Dispose();

    private byte[] ClientData(string type, string challenge)
        => JsonSerializer.SerializeToUtf8Bytes(new { type, challenge, origin = Origin, crossOrigin = false });

    private byte[] Counter(bool advance)
    {
        if (advance && _countsSignatures)
            SignCount++;

        var counter = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(counter, SignCount);
        return counter;
    }

    private byte[] CoseKey()
    {
        var point = _key.ExportParameters(includePrivateParameters: false).Q;
        var key = new CborWriter(CborConformanceMode.Ctap2Canonical);
        key.WriteStartMap(5);
        key.WriteInt32(1);
        key.WriteInt32(2); // kty: EC2
        key.WriteInt32(3);
        key.WriteInt32(-7); // alg: ES256
        key.WriteInt32(-1);
        key.WriteInt32(1); // crv: P-256
        key.WriteInt32(-2);
        key.WriteByteString(point.X!);
        key.WriteInt32(-3);
        key.WriteByteString(point.Y!);
        key.WriteEndMap();
        return key.Encode();
    }

    private static byte[] RpIdHash(string rpId) => SHA256.HashData(Encoding.UTF8.GetBytes(rpId));

    private static byte[] BigEndian(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }

    private static string Encode(byte[] bytes) => Base64Url.EncodeToString(bytes);
}
