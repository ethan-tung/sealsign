namespace SealSign.Core.Entities;

/// <summary>待签署的原始文档。</summary>
public sealed class Document
{
    public long Id { get; set; }
    public long UploadedBy { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string SignedObjectKey { get; set; } = string.Empty; // 最后一次盖章后的文件
    public string Sha256 { get; set; } = string.Empty;          // 原始文件哈希
    public int Pages { get; set; }
    public DateTime CreatedAt { get; set; }
}
