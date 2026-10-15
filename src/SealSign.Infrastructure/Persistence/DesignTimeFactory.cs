using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SealSign.Infrastructure.Persistence;

/// <summary>
/// 供 dotnet-ef 生成迁移使用，不参与运行时。
/// 设计时用固定 ServerVersion，避免依赖实时数据库（AutoDetect 会尝试连接）。
/// </summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var opt = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(
                "server=localhost;port=3306;database=sealsign;user=root;password=123456;charset=utf8mb4",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        return new AppDbContext(opt);
    }
}
