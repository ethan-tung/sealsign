using Xunit;
using System.Security.Cryptography;
using System.Text;
using SealSign.Core;
using SealSign.Infrastructure.Crypto;

namespace SealSign.Tests;

public sealed class CryptoTests
{
    private static (KeyProtector protector, string dir) NewEnv()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sealsign-test", Guid.NewGuid().ToString("N"));
        var protector = new KeyProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        return (protector, dir);
    }

    [Fact]
    public void KeyProtector_RoundTrip()
    {
        var (protector, _) = NewEnv();
        var plain = Encoding.UTF8.GetBytes("platform-private-key-bytes");
        var enc = protector.Protect(plain);
        Assert.Equal(plain, protector.Unprotect(enc));
    }

    [Fact]
    public void PlatformSigner_SignAndVerify()
    {
        var (protector, dir) = NewEnv();
        using var signer = new RsaPlatformSigner(new PlatformKeyStore(dir, protector), 1);

        var data = Encoding.UTF8.GetBytes("chain-hash-bytes");
        var sig = signer.SignToBase64(data);

        Assert.True(signer.VerifyFromBase64(data, sig));
        Assert.StartsWith("-----BEGIN PUBLIC KEY-----", signer.GetPublicKeyPem().Trim());
    }

    [Fact]
    public void PlatformSigner_TamperedData_FailsVerify()
    {
        var (protector, dir) = NewEnv();
        using var signer = new RsaPlatformSigner(new PlatformKeyStore(dir, protector), 1);

        var sig = signer.SignToBase64(Encoding.UTF8.GetBytes("original"));
        Assert.False(signer.VerifyFromBase64(Encoding.UTF8.GetBytes("tampered"), sig));
        Assert.False(signer.VerifyFromBase64(Encoding.UTF8.GetBytes("original"), "aGVsbG8="));
    }

    [Fact]
    public void PlatformKeyStore_PersistsAcrossInstances()
    {
        var (protector, dir) = NewEnv();
        string sig;
        using (var s1 = new RsaPlatformSigner(new PlatformKeyStore(dir, protector), 1))
            sig = s1.SignToBase64(Encoding.UTF8.GetBytes("data"));

        // 新实例从加密文件加载同一私钥，旧签名仍可验过
        using var s2 = new RsaPlatformSigner(new PlatformKeyStore(dir, protector), 1);
        Assert.True(s2.VerifyFromBase64(Encoding.UTF8.GetBytes("data"), sig));
    }
}
