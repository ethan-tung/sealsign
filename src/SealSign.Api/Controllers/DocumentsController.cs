using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SealSign.Core;
using SealSign.Core.Entities;
using SealSign.Infrastructure.Pdf;
using SealSign.Infrastructure.Persistence;

namespace SealSign.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(AppDbContext db, IFileStorage storage, PdfStamper stamper) : ApiBase(db)
{
    [HttpPost("upload")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("请上传 PDF 文件");
        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("仅支持 PDF");

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var hash = HashChain.Sha256Hex(bytes);

        var key = $"docs/{UserId}/{Guid.NewGuid():N}.pdf";
        await using var toSave = new MemoryStream(bytes);
        await storage.SaveAsync(toSave, key, "application/pdf", ct);

        int pages;
        await using var toRead = new MemoryStream(bytes);
        try
        {
            pages = stamper.GetPageCount(toRead);
        }
        catch (Exception ex)
        {
            return BadRequest($"PDF 解析失败：{ex.Message}");
        }

        var doc = new Document
        {
            UploadedBy = UserId,
            ObjectKey = key,
            Sha256 = hash,
            Pages = pages,
            CreatedAt = DateTime.UtcNow,
        };
        Db.Documents.Add(doc);
        await Db.SaveChangesAsync(ct);
        return Ok(new { doc.Id, doc.Sha256, doc.Pages });
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await Db.Documents
            .Where(d => d.UploadedBy == UserId)
            .OrderByDescending(d => d.Id)
            .Select(d => new
            {
                d.Id, d.Pages, d.Sha256, d.CreatedAt,
                Signed = d.SignedObjectKey != string.Empty,
            })
            .ToListAsync(ct));

    /// <summary>下载：有盖章记录时返回最新盖章版，否则返回原件。</summary>
    [HttpGet("{id:long}/download")]
    public async Task<IActionResult> Download(long id, CancellationToken ct)
    {
        var doc = await Db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UploadedBy == UserId, ct);
        if (doc is null)
            return NotFound();
        var key = string.IsNullOrEmpty(doc.SignedObjectKey) ? doc.ObjectKey : doc.SignedObjectKey;
        var stream = await storage.OpenReadAsync(key, ct);
        return File(stream, "application/pdf", $"document-{id}.pdf");
    }
}
