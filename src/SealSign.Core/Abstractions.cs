namespace SealSign.Core;

/// <summary>文件存储抽象。MVP 用本地磁盘，生产换 MinIO/S3/OSS 时只需换实现。</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string objectKey, string contentType, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct = default);
    Task<bool> ExistsAsync(string objectKey, CancellationToken ct = default);
}

/// <summary>平台签名抽象：信任根。生产环境换成 HSM/KMS 实现。</summary>
public interface IPlatformSigner
{
    int KeyVersion { get; }
    string SignToBase64(byte[] data);
    bool VerifyFromBase64(byte[] data, string signatureBase64);
    string GetPublicKeyPem();
}

/// <summary>时钟抽象，便于测试。</summary>
public interface ISystemClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
