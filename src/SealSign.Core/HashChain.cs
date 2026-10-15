using System.Security.Cryptography;
using System.Text;

namespace SealSign.Core;

/// <summary>哈希链与编号工具。所有哈希均为大写十六进制 SHA256。</summary>
public static class HashChain
{
    public const string GenesisPrevHash = "GENESIS";

    public static string Sha256Hex(byte[] data)
        => Convert.ToHexString(SHA256.HashData(data));

    public static string Sha256Hex(string text)
        => Sha256Hex(Encoding.UTF8.GetBytes(text));

    /// <summary>生成业务编号。先生成后盖章，避免"盖章后才知道编号"的死锁。</summary>
    public static string NewRecordNo()
        => $"SN-{DateTime.UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3))}";

    /// <summary>
    /// 计算一条签署记录的链哈希。字段按固定顺序拼接，保证可重算、可校验。
    /// 任何字段被改动，重算结果即与存量不一致。
    /// </summary>
    public static string ComputeChainHash(
        string recordNo, string signedDocHash, long signerId, long sealId,
        DateTime signedAtUtc, string prevHash)
    {
        var canonical = string.Join("|",
            recordNo, signedDocHash, signerId, sealId,
            signedAtUtc.ToString("O"), prevHash);
        return Sha256Hex(canonical);
    }
}
