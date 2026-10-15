using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SealSign.Infrastructure.Persistence;

namespace SealSign.Api.Controllers;

public abstract class ApiBase(AppDbContext db) : ControllerBase
{
    protected AppDbContext Db => db;
    protected long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    protected string UserName => User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
}
