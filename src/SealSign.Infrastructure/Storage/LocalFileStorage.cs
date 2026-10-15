using SealSign.Core;

namespace SealSign.Infrastructure.Storage;

/// <summary>本地磁盘存储（MVP）。生产换 MinIO/S3/OSS 时替换此实现即可。</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    private string Resolve(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root, StringComparison.Ordinal))
            throw new InvalidOperationException("非法的文件路径");
        return full;
    }

    public async Task<string> SaveAsync(Stream content, string objectKey, string contentType, CancellationToken ct = default)
    {
        var full = Resolve(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return objectKey;
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct = default)
        => Task.FromResult<Stream>(File.OpenRead(Resolve(objectKey)));

    public Task<bool> ExistsAsync(string objectKey, CancellationToken ct = default)
        => Task.FromResult(File.Exists(Resolve(objectKey)));
}
