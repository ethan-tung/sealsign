using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SealSign.Api.Models;
using SealSign.Api.Security;
using SealSign.Core.Entities;
using SealSign.Infrastructure.Persistence;

namespace SealSign.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AppDbContext db, JwtTokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Phone) || req.Password.Length < 6)
            return BadRequest("姓名/手机号/密码（至少6位）不能为空");
        if (await db.Users.AnyAsync(u => u.Phone == req.Phone, ct))
            return Conflict("手机号已注册");

        var user = new User
        {
            Name = req.Name.Trim(),
            Phone = req.Phone.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return Ok(new { user.Id, user.Name, Token = tokens.CreateToken(user.Id, user.Name) });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Phone == req.Phone, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized("账号或密码错误");
        return Ok(new { user.Id, user.Name, Token = tokens.CreateToken(user.Id, user.Name) });
    }
}
