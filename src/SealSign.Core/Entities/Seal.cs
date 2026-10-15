namespace SealSign.Core.Entities;

/// <summary>电子印章：印章图片 + 持有人绑定。</summary>
public sealed class Seal
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageObjectKey { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
