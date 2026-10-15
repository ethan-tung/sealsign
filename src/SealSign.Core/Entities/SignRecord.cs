namespace SealSign.Core.Entities;

/// <summary>
/// 签署记录：信任链的核心。只增不改。
/// 防篡改靠三层：SignedDocHash（文件哈希）、PrevHash/ChainHash（哈希链）、PlatformSignature（平台私钥签名）。
/// </summary>
public sealed class SignRecord
{
    public long Id { get; set; }
    public string RecordNo { get; set; } = string.Empty;      // SN-YYYYMMDD-XXXXXX，业务唯一编号
    public long DocumentId { get; set; }
    public string SignedDocHash { get; set; } = string.Empty; // 盖章后 PDF 的 SHA256
    public string SignedObjectKey { get; set; } = string.Empty;
    public long SignerId { get; set; }
    public long SealId { get; set; }
    public DateTime SignedAt { get; set; }                    // UTC
    public string? Ip { get; set; }
    public string PrevHash { get; set; } = string.Empty;      // 上一条记录的 ChainHash
    public string ChainHash { get; set; } = string.Empty;     // 本记录链哈希
    public string PlatformSignature { get; set; } = string.Empty; // 平台私钥对 ChainHash 的签名（Base64）
    public int KeyVersion { get; set; } = 1;                 // 签名密钥版本（支持轮换）
}
