using System.Security.Cryptography;

namespace SealSign.Infrastructure.Crypto;

/// <summary>
/// 平台 RSA 密钥对的生成、加密存储与加载。
/// MVP 方案：私钥经主密钥加密后落盘。生产环境应替换为 HSM / 云 KMS 实现。
/// </summary>
public sealed class PlatformKeyStore
{
    private readonly string _dir;
    private readonly KeyProtector _protector;
    private readonly int _keyVersion;

    public PlatformKeyStore(string directory, KeyProtector protector, int keyVersion = 1)
    {
        _dir = directory;
        _protector = protector;
        _keyVersion = keyVersion;
        Directory.CreateDirectory(_dir);
    }

    public RSA LoadOrCreatePrivateKey()
    {
        var path = Path.Combine(_dir, $"platform.v{_keyVersion}.key.enc");
        if (!File.Exists(path))
        {
            using var fresh = RSA.Create(2048);
            var exported = fresh.ExportPkcs8PrivateKey();
            File.WriteAllText(path, _protector.Protect(exported));
            File.WriteAllText(
                Path.Combine(_dir, $"platform.v{_keyVersion}.pub.pem"),
                fresh.ExportSubjectPublicKeyInfoPem());
            CryptographicOperations.ZeroMemory(exported);
        }

        var pkcs8 = _protector.Unprotect(File.ReadAllText(path));
        try
        {
            var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(pkcs8, out _);
            return rsa;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs8);
        }
    }
}
