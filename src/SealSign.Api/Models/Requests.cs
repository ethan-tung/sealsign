namespace SealSign.Api.Models;

public sealed record RegisterRequest(string Name, string Phone, string Password);
public sealed record LoginRequest(string Phone, string Password);

public sealed record SealUploadRequest(string Name, IFormFile? Image);

/// <summary>盖章位置：页码从 1 开始；坐标毫米，从页面左上角起算。</summary>
public sealed record PlacementDto(int Page, double Xmm, double Ymm, double WidthMm, double? HeightMm);

public sealed record StampRequest(long DocumentId, long SealId, List<PlacementDto> Placements);
