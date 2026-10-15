using System.Security.Cryptography;
using System.Text;
using SealSign.Core;

namespace SealSign.Infrastructure.Crypto;

/// <summary>平台签名：RSA-2048 + SHA256 + PKCS#1 v1.5。单例持有私钥。</summary>
public sealed class RsaPlatformSigner : IPlatformSigner, IDisposable
{
    private readonly RSA _rsa;

    public int KeyVersion { get; }

    public RsaPlatformSigner(PlatformKeyStore store, int keyVersion = 1)
    {
        KeyVersion = keyVersion;
        _rsa = store.LoadOrCreatePrivateKey();
    }

    public string SignToBase64(byte[] data)
        => Convert.ToBase64String(
            _rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    public bool VerifyFromBase64(byte[] data, string signatureBase64)
    {
        try
        {
            return _rsa.VerifyData(
                data, Convert.FromBase64String(signatureBase64),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    public string GetPublicKeyPem() => _rsa.ExportSubjectPublicKeyInfoPem();

    public void Dispose() => _rsa.Dispose();
}
