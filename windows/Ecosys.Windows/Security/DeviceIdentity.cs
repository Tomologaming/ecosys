using System.Security.Cryptography;

namespace Ecosys.Windows.Security;

public sealed class DeviceIdentity : IDisposable
{
    private readonly CngKey key;
    private readonly ECDsaCng signingKey;

    public DeviceIdentity(string keyName = "Ecosys.Identity")
    {
        key = CngKey.Exists(keyName, CngProvider.MicrosoftSoftwareKeyStorageProvider)
            ? CngKey.Open(keyName, CngProvider.MicrosoftSoftwareKeyStorageProvider)
            : CngKey.Create(
                CngAlgorithm.ECDsaP256,
                keyName,
                new CngKeyCreationParameters
                {
                    Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider
                });

        signingKey = new ECDsaCng(key);
    }

    public byte[] PublicKey => signingKey.ExportSubjectPublicKeyInfo();

    public string DeviceId => Convert.ToHexString(SHA256.HashData(PublicKey)[..12]).ToLowerInvariant();

    public byte[] Sign(ReadOnlySpan<byte> data) =>
        signingKey.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature) =>
        signingKey.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

    public ECDiffieHellman CreateEphemeralKey() =>
        ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    public static byte[] DeriveSharedSecret(ECDiffieHellman local, ECDiffieHellmanPublicKey peer) =>
        local.DeriveKeyMaterial(peer);

    public static ECDiffieHellmanPublicKey ImportEcdhPublicKey(byte[] encoded)
    {
        using var key = ECDiffieHellman.Create();
        key.ImportSubjectPublicKeyInfo(encoded, out _);
        return key.PublicKey;
    }

    public static byte[] AesGcmEncrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> aad = default)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
        return [..nonce, ..ciphertext, ..tag];
    }

    public static byte[] AesGcmDecrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> sealedData, ReadOnlySpan<byte> aad = default)
    {
        if (sealedData.Length < 28) throw new CryptographicException("Ciphertext too short");
        var nonce = sealedData[..12];
        var ciphertext = sealedData[12..^16];
        var tag = sealedData[^16..];
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
        return plaintext;
    }

    public void Dispose()
    {
        signingKey.Dispose();
        key.Dispose();
    }
}
