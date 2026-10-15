namespace SealSign.Core.Entities;

/// <summary>用户（签署人）。MVP 阶段仅做账号体系，不做实名认证。</summary>
public sealed class User
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<Seal> Seals { get; set; } = new List<Seal>();
}
