using System.Text;
using Microsoft.EntityFrameworkCore;
using SealSign.Core;
using SealSign.Core.Entities;
using SealSign.Infrastructure.Pdf;
using SealSign.Infrastructure.Persistence;

namespace SealSign.Infrastructure.Services;

/// <summary>
/// 签署服务：盖章 → 算哈希 → 写哈希链 → 平台签名 → 落库（同一事务）。
/// 注意：并发盖章时哈希链需要串行写入，单机用信号量，多实例换 Redis 分布式锁。
/// </summary>
public sealed class SigningService
{
    private static readonly SemaphoreSlim ChainLock = new(1, 1);

    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly PdfStamper _stamper;
    private readonly IPlatformSigner _signer;
    private readonly ISystemClock _clock;
    private readonly string _verifyBaseUrl;

    public SigningService(
        AppDbContext db, IFileStorage storage, PdfStamper stamper,
        IPlatformSigner signer, ISystemClock clock, string verifyBaseUrl)
    {
        _db = db;
        _storage = storage;
        _stamper = stamper;
        _signer = signer;
        _clock = clock;
        _verifyBaseUrl = verifyBaseUrl.TrimEnd('/');
    }

    public async Task<SignRecord> StampAndRecordAsync(
        long documentId, long sealId, long signerId, string signerName,
        IReadOnlyList<StampPlacement> placements, string? ip,
        CancellationToken ct = default)
    {
        await ChainLock.WaitAsync(ct);
        try
        {
            var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
                ?? throw new InvalidOperationException("文档不存在");
            var seal = await _db.Seals.FirstOrDefaultAsync(s => s.Id == sealId && s.UserId == signerId && s.IsActive, ct)
                ?? throw new InvalidOperationException("印章不存在、无权使用或已停用");

            // 编号先生成：盖章注记里要印编号，避免"盖完才知道编号"的死锁
            var recordNo = HashChain.NewRecordNo();
            var signedAt = _clock.UtcNow;

            // 1) 盖章渲染
            await using var pdfIn = await _storage.OpenReadAsync(doc.ObjectKey, ct);
            await using var sealImg = await _storage.OpenReadAsync(seal.ImageObjectKey, ct);
            await using var stamped = new MemoryStream();
            _stamper.Stamp(pdfIn, sealImg, placements,
                new StampOptions(recordNo, signerName, signedAt, $"{_verifyBaseUrl}/api/verify/{recordNo}"),
                stamped);
            var stampedBytes = stamped.ToArray();
            var signedHash = HashChain.Sha256Hex(stampedBytes);

            // 2) 存盖章后文件
            var signedKey = $"signed/{doc.Id}/{recordNo}.pdf";
            await using var toSave = new MemoryStream(stampedBytes);
            await _storage.SaveAsync(toSave, signedKey, "application/pdf", ct);

            // 3) 哈希链：取上一条记录的 ChainHash
            var prevHash = await _db.SignRecords
                .OrderByDescending(r => r.Id)
                .Select(r => r.ChainHash)
                .FirstOrDefaultAsync(ct) ?? HashChain.GenesisPrevHash;
            var chainHash = HashChain.ComputeChainHash(recordNo, signedHash, signerId, seal.Id, signedAt, prevHash);

            // 4) 平台私钥签名（信任根）
            var signature = _signer.SignToBase64(Encoding.UTF8.GetBytes(chainHash));

            // 5) 落库：记录 + 更新文档的最新盖章文件（同一 SaveChanges）
            var record = new SignRecord
            {
                RecordNo = recordNo,
                DocumentId = doc.Id,
                SignedDocHash = signedHash,
                SignedObjectKey = signedKey,
                SignerId = signerId,
                SealId = seal.Id,
                SignedAt = signedAt,
                Ip = ip,
                PrevHash = prevHash,
                ChainHash = chainHash,
                PlatformSignature = signature,
                KeyVersion = _signer.KeyVersion,
            };
            _db.SignRecords.Add(record);
            // 全局 NoTracking 下用 ExecuteUpdateAsync 做无跟踪更新
            await _db.Documents.Where(d => d.Id == doc.Id)
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.SignedObjectKey, signedKey), ct);
            await _db.SaveChangesAsync(ct);
            return record;
        }
        finally
        {
            ChainLock.Release();
        }
    }
}
