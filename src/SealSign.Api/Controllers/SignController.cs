using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SealSign.Api.Models;
using SealSign.Core;
using SealSign.Infrastructure.Pdf;
using SealSign.Infrastructure.Persistence;
using SealSign.Infrastructure.Services;

namespace SealSign.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/sign")]
public sealed class SignController(AppDbContext db, SigningService signing) : ApiBase(db)
{
    /// <summary>盖章并生成签署记录。placements 坐标单位毫米，从页面左上角起算。</summary>
    [HttpPost("stamp")]
    public async Task<IActionResult> Stamp([FromBody] StampRequest req, CancellationToken ct)
    {
        if (req.Placements is null || req.Placements.Count == 0)
            return BadRequest("至少指定一个盖章位置");
        if (req.Placements.Any(p => p.Page < 1 || p.WidthMm <= 0))
            return BadRequest("盖章位置参数非法");

        try
        {
            var record = await signing.StampAndRecordAsync(
                req.DocumentId, req.SealId, UserId, UserName,
                req.Placements
                    .Select(p => new StampPlacement(p.Page, p.Xmm, p.Ymm, p.WidthMm, p.HeightMm))
                    .ToList(),
                HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
            return Ok(new
            {
                record.RecordNo,
                SignedHash = record.SignedDocHash,
                record.SignedAt,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("records")]
    public async Task<IActionResult> Records([FromQuery] long? documentId, CancellationToken ct)
    {
        var q = Db.SignRecords.Where(r => r.SignerId == UserId);
        if (documentId.HasValue)
            q = q.Where(r => r.DocumentId == documentId.Value);
        return Ok(await q
            .OrderByDescending(r => r.Id)
            .Take(100)
            .Select(r => new
            {
                r.RecordNo, r.DocumentId, r.SignedDocHash, r.SignedAt, r.SealId,
            })
            .ToListAsync(ct));
    }

    [HttpGet("records/{recordNo}")]
    public async Task<IActionResult> Detail(string recordNo, CancellationToken ct)
    {
        var r = await Db.SignRecords
            .FirstOrDefaultAsync(x => x.RecordNo == recordNo && x.SignerId == UserId, ct);
        return r is null ? NotFound() : Ok(new
        {
            r.RecordNo, r.DocumentId, r.SignedDocHash, r.SignedAt,
            r.SealId, r.PrevHash, r.ChainHash, r.PlatformSignature, r.KeyVersion, r.Ip,
        });
    }

    [HttpGet("records/{recordNo}/download")]
    public async Task<IActionResult> DownloadSigned(
        string recordNo, [FromServices] IFileStorage storage, CancellationToken ct)
    {
        var r = await Db.SignRecords
            .FirstOrDefaultAsync(x => x.RecordNo == recordNo && x.SignerId == UserId, ct);
        if (r is null)
            return NotFound();
        var stream = await storage.OpenReadAsync(r.SignedObjectKey, ct);
        return File(stream, "application/pdf", $"{recordNo}.pdf");
    }
}
