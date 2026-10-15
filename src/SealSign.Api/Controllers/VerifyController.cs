using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SealSign.Infrastructure.Persistence;
using SealSign.Infrastructure.Services;

namespace SealSign.Api.Controllers;

/// <summary>验真接口全部公开：验真的意义就是让任何人都能查。</summary>
[ApiController]
[Route("api/verify")]
public sealed class VerifyController(AppDbContext db, VerificationService verifier) : ControllerBase
{
    /// <summary>验文件：上传 PDF，返回是否匹配签署记录、记录是否可信。</summary>
    [HttpPost("file")]
    [AllowAnonymous]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> VerifyFile(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("请上传 PDF 文件");
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var result = await verifier.VerifyDocumentAsync(
            ms.ToArray(), HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Ok(new
        {
            result.Matched,
            result.ChainValid,
            result.Message,
            Record = result.Record is null ? null : new
            {
                result.Record.RecordNo,
                result.Record.SignedAt,
                Signer = result.SignerName,
                result.Record.SealId,
            },
        });
    }

    /// <summary>凭记录编号查（二维码扫码落地页）。</summary>
    [HttpGet("{recordNo}")]
    [AllowAnonymous]
    public async Task<IActionResult> ByRecordNo(string recordNo, CancellationToken ct)
    {
        var r = await db.SignRecords.FirstOrDefaultAsync(x => x.RecordNo == recordNo, ct);
        if (r is null)
            return NotFound("记录不存在");
        var signer = await db.Users
            .Where(u => u.Id == r.SignerId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(ct);
        return Ok(new
        {
            r.RecordNo, r.SignedDocHash, r.SignedAt,
            Signer = signer, r.SealId, r.KeyVersion,
        });
    }

    /// <summary>全链巡检：逐条重算链哈希、验平台签名、验前后链接。</summary>
    [HttpPost("chain")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyChain([FromQuery] int take = 1000, CancellationToken ct = default)
    {
        var (valid, checkedCount, brokenAt) =
            await verifier.VerifyChainAsync(Math.Min(Math.Max(take, 1), 5000), ct);
        return Ok(new { valid, checkedCount, brokenAt });
    }
}
