using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SealSign.Api.Models;
using SealSign.Core;
using SealSign.Core.Entities;
using SealSign.Infrastructure.Persistence;

namespace SealSign.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/seals")]
public sealed class SealsController(AppDbContext db, IFileStorage storage) : ApiBase(db)
{
    private static readonly string[] AllowedExt = [".png", ".jpg", ".jpeg"];

    /// <summary>上传印章图片（建议透明底 PNG）。</summary>
    [HttpPost]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Upload([FromForm] SealUploadRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("印章名称不能为空");
        if (req.Image is null || req.Image.Length == 0)
            return BadRequest("请上传印章图片");
        var ext = Path.GetExtension(req.Image.FileName).ToLowerInvariant();
        if (!AllowedExt.Contains(ext))
            return BadRequest("仅支持 png/jpg");

        var key = $"seals/{UserId}/{Guid.NewGuid():N}{ext}";
        await using var s = req.Image.OpenReadStream();
        await storage.SaveAsync(s, key, req.Image.ContentType, ct);

        var seal = new Seal
        {
            UserId = UserId,
            Name = req.Name.Trim(),
            ImageObjectKey = key,
            CreatedAt = DateTime.UtcNow,
        };
        Db.Seals.Add(seal);
        await Db.SaveChangesAsync(ct);
        return Ok(new { seal.Id, seal.Name });
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await Db.Seals
            .Where(s => s.UserId == UserId && s.IsActive)
            .OrderByDescending(s => s.Id)
            .Select(s => new { s.Id, s.Name, s.CreatedAt })
            .ToListAsync(ct));

    /// <summary>停用印章（不物理删除，历史签署记录保持可查）。</summary>
    [HttpPatch("{id:long}/disable")]
    public async Task<IActionResult> Disable(long id, CancellationToken ct)
    {
        var n = await Db.Seals
            .Where(s => s.Id == id && s.UserId == UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
        return n == 0 ? NotFound() : Ok();
    }
}
