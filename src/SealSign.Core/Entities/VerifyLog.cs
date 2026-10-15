namespace SealSign.Core.Entities;

/// <summary>验真日志：谁在何时验过哪个文件，验真本身也留痕。</summary>
public sealed class VerifyLog
{
    public long Id { get; set; }
    public string DocHash { get; set; } = string.Empty;
    public bool Matched { get; set; }
    public long? SignRecordId { get; set; }
    public string? Ip { get; set; }
    public DateTime CheckedAt { get; set; }
}
