using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PdfSharpCore.Fonts;
using SealSign.Api.Security;
using SealSign.Core;
using SealSign.Infrastructure.Crypto;
using SealSign.Infrastructure.Pdf;
using SealSign.Infrastructure.Persistence;
using SealSign.Infrastructure.Services;
using SealSign.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(); // /openapi/v1.json

// ---------- MySQL（Pomelo）：读多写少，全局默认 NoTracking ----------
var conn = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("未配置 ConnectionStrings:Default");
builder.Services.AddDbContext<AppDbContext>(opt => opt
    .UseMySql(conn, ServerVersion.AutoDetect(conn))
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

// ---------- JWT ----------
var jwt = builder.Configuration.GetSection("Jwt");
var jwtKey = Encoding.UTF8.GetBytes(jwt["Key"]
    ?? throw new InvalidOperationException("未配置 Jwt:Key（至少 32 字符）"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
            ClockSkew = TimeSpan.FromMinutes(2),
        };
    });
builder.Services.AddAuthorization();

// ---------- 基础设施 ----------
var storageRoot = builder.Configuration["Storage:RootPath"] ?? "data/files";
builder.Services.AddSingleton<IFileStorage>(_ => new LocalFileStorage(storageRoot));
builder.Services.AddSingleton<ISystemClock, SystemClock>();

var keyDir = builder.Configuration["Crypto:KeyDirectory"] ?? "data/keys";
Directory.CreateDirectory(keyDir);
var masterKey = builder.Configuration["Crypto:MasterKey"];
if (string.IsNullOrWhiteSpace(masterKey))
    masterKey = Environment.GetEnvironmentVariable("SEALSIGN_MASTER_KEY");
if (string.IsNullOrWhiteSpace(masterKey))
{
    // 开发模式兜底：生成主密钥并落盘，生产环境必须显式配置
    var fallback = Path.Combine(keyDir, "master.key");
    if (File.Exists(fallback))
    {
        masterKey = File.ReadAllText(fallback).Trim();
    }
    else
    {
        masterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        File.WriteAllText(fallback, masterKey);
        Console.WriteLine($"[警告] 未配置主密钥，已生成开发用主密钥并保存到 {fallback}。生产环境请配置 Crypto:MasterKey。");
    }
}
builder.Services.AddSingleton(new KeyProtector(masterKey));
var keyVersion = builder.Configuration.GetValue("Crypto:KeyVersion", 1);
builder.Services.AddSingleton(sp => new PlatformKeyStore(keyDir, sp.GetRequiredService<KeyProtector>(), keyVersion));
builder.Services.AddSingleton<IPlatformSigner>(sp =>
    new RsaPlatformSigner(sp.GetRequiredService<PlatformKeyStore>(), keyVersion));

// PDF 注记字体：配了中文字体 TTF 才安装自定义解析器，否则用内置 Helvetica
var cjkPath = builder.Configuration["Pdf:CjkFontPath"];
var fontFamily = "Helvetica";
if (!string.IsNullOrWhiteSpace(cjkPath) && File.Exists(cjkPath))
{
    var fallback = GlobalFontSettings.FontResolver
        ?? throw new InvalidOperationException("PDF 默认字体解析器不可用");
    GlobalFontSettings.FontResolver = new FileFontResolver(cjkPath, fallback);
    fontFamily = "CJK";
}
builder.Services.AddSingleton(_ => new PdfStamper(fontFamily));
var verifyBase = (builder.Configuration["App:PublicBaseUrl"] ?? "http://localhost:5000").TrimEnd('/');
builder.Services.AddScoped<SigningService>(sp => new SigningService(
    sp.GetRequiredService<AppDbContext>(),
    sp.GetRequiredService<IFileStorage>(),
    sp.GetRequiredService<PdfStamper>(),
    sp.GetRequiredService<IPlatformSigner>(),
    sp.GetRequiredService<ISystemClock>(),
    verifyBase));
builder.Services.AddScoped<VerificationService>();
builder.Services.AddSingleton<JwtTokenService>();

// PDF 中文字体：必须在第一次使用 XFont 之前设置（已在上方按需安装）

var app = builder.Build();

app.MapOpenApi();

// 启动时自动迁移（含 MySQL 未就绪重试）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    const int maxTry = 15;
    for (var i = 1; i <= maxTry; i++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (i < maxTry)
        {
            Console.WriteLine($"[启动] 数据库未就绪（{i}/{maxTry}）：{ex.Message}，2 秒后重试…");
            await Task.Delay(2000);
        }
    }
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
