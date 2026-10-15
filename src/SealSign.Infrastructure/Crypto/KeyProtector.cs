using System.Security.Cryptography;

namespace SealSign.Infrastructure.Crypto;

/// <summary>
/// 用主密钥对敏感字节做 AES-GCM 加解密。
/// 主密钥来自配置 Crypto:MasterKey（32 字节 Base64）或环境变量 SEALSIGN_MASTER_KEY。
/// </summary>
public sealed class KeyProtector
{
    private readonly byte[] _masterKey;

    public KeyProtector(string masterKeyBase64)
    {
        _masterKey = Convert.FromBase64String(masterKeyBase64);
        if (_masterKey.Length != 32)
            throw new ArgumentException("主密钥必须是 32 字节的 Base64 字符串");
    }

    public string Protect(byte[] plain)
    {
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        using var aes = new AesGcm(_masterKey, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public byte[] Unprotect(string protectedBase64)
    {
        var all = Convert.FromBase64String(protectedBase64);
        var n = AesGcm.NonceByteSizes.MaxSize;
        var t = AesGcm.TagByteSizes.MaxSize;
        var nonce = all[..n];
        var tag = all[n..(n + t)];
        var cipher = all[(n + t)..];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_masterKey, t);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }
}
