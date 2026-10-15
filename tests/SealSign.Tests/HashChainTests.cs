using Xunit;
using SealSign.Core;

namespace SealSign.Tests;

public sealed class HashChainTests
{
    private static readonly DateTime T = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ComputeChainHash_IsDeterministic()
    {
        var a = HashChain.ComputeChainHash("SN-1", "AAA", 1, 1, T, HashChain.GenesisPrevHash);
        var b = HashChain.ComputeChainHash("SN-1", "AAA", 1, 1, T, HashChain.GenesisPrevHash);
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
    }

    [Fact]
    public void Chain_TamperIsDetectable()
    {
        // 正常链：记录1 -> 记录2
        var h1 = HashChain.ComputeChainHash("SN-1", "AAA", 1, 1, T, HashChain.GenesisPrevHash);
        var h2 = HashChain.ComputeChainHash("SN-2", "BBB", 1, 1, T, h1);

        // 攻击者篡改记录1的文件哈希
        var h1Tampered = HashChain.ComputeChainHash("SN-1", "XXX", 1, 1, T, HashChain.GenesisPrevHash);
        Assert.NotEqual(h1, h1Tampered);

        // 记录2 的 PrevHash 仍指向旧 h1，与篡改后的 h1Tampered 对不上 → 断链可发现
        Assert.NotEqual(h1Tampered, h1);
        var h2RecomputedAfterTamper = HashChain.ComputeChainHash("SN-2", "BBB", 1, 1, T, h1Tampered);
        Assert.NotEqual(h2, h2RecomputedAfterTamper);
    }

    [Fact]
    public void NewRecordNo_HasExpectedFormat()
    {
        var no = HashChain.NewRecordNo();
        Assert.StartsWith("SN-", no);
        Assert.Equal(3, no.Split('-').Length);
    }
}
