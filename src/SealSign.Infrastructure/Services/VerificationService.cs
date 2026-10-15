using System.Text;
using Microsoft.EntityFrameworkCore;
using SealSign.Core;
using SealSign.Core.Entities;
using SealSign.Infrastructure.Persistence;

namespace SealSign.Infrastructure.Services;

public sealed record VerifyResult(
    bool Matched, SignRecord? Record, string? SignerName, bool ChainValid, string Message);

/// <summary>验真服务：文件哈希比对 + 平台签名校验 + 哈希链校验。</summary>
public sealed class VerificationService
{
    private readonly AppDbContext _db;
    private readonly IPlatformSigner _signer;

    public VerificationService(AppDbContext db, IPlatformSigner signer)
    {
        _db = db;
        _signer = signer;
    }

    /// <summary>验文件：任何人上传 PDF，返回是否匹配签署记录、记录是否可信。</summary>
    public async Task<VerifyResult> VerifyDocumentAsync(byte[] pdfBytes, string? ip, CancellationToken ct = default)
    {
        var hash = HashChain.Sha256Hex(pdfBytes);
        var record = await _db.SignRecords
            .Where(r => r.SignedDocHash == hash)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);

        var matched = record is not null;
        var chainValid = false;
        string? signerName = null;
        if (record is not null)
        {
            chainValid = _signer.VerifyFromBase64(Encoding.UTF8.GetBytes(record.ChainHash), record.PlatformSignature)
                && await ChainLinkOkAsync(record, ct);
            signerName = await _db.Users
                .Where(u => u.Id == record.SignerId)
                .Select(u => u.Name)
                .FirstOrDefaultAsync(ct);
        }

        // 验真本身也留痕
        _db.VerifyLogs.Add(new VerifyLog
        {
            DocHash = hash,
            Matched = matched,
            SignRecordId = record?.Id,
            Ip = ip,
            CheckedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        var message = !matched
            ? "未找到匹配的签署记录：文件可能被修改过，或从未在本平台盖章。"
            : chainValid
                ? "验真通过：文件与盖章时完全一致，签署记录完整可信。"
                : "警告：找到签署记录，但链哈希或平台签名校验失败，记录可能被篡改。";
        return new VerifyResult(matched, record, signerName, chainValid, message);
    }

    /// <summary>单条记录的链完整性：链哈希可重算，且 prev 指向真实存在的前一条。</summary>
    private async Task<bool> ChainLinkOkAsync(SignRecord record, CancellationToken ct)
    {
        var recomputed = HashChain.ComputeChainHash(
            record.RecordNo, record.SignedDocHash, record.SignerId,
            record.SealId, record.SignedAt, record.PrevHash);
        if (recomputed != record.ChainHash)
            return false;
        if (record.PrevHash == HashChain.GenesisPrevHash)
            return true;
        return await _db.SignRecords.AnyAsync(r => r.ChainHash == record.PrevHash, ct);
    }

    /// <summary>全链巡检：从新到旧逐条重算链哈希、验平台签名、验前后链接。</summary>
    public async Task<(bool Valid, int Checked, string? BrokenAt)> VerifyChainAsync(int take, CancellationToken ct = default)
    {
        var records = await _db.SignRecords
            .OrderByDescending(r => r.Id)
            .Take(take)
            .ToListAsync(ct);

        string? expectPrev = null; // 上一条（更新的）记录的 PrevHash，应等于本条的 ChainHash
        foreach (var r in records)
        {
            var recomputed = HashChain.ComputeChainHash(
                r.RecordNo, r.SignedDocHash, r.SignerId, r.SealId, r.SignedAt, r.PrevHash);
            if (recomputed != r.ChainHash)
                return (false, records.Count, r.RecordNo);
            if (!_signer.VerifyFromBase64(Encoding.UTF8.GetBytes(r.ChainHash), r.PlatformSignature))
                return (false, records.Count, r.RecordNo);
            if (expectPrev is not null && r.ChainHash != expectPrev)
                return (false, records.Count, r.RecordNo);
            expectPrev = r.PrevHash;
        }
        return (true, records.Count, null);
    }
}
