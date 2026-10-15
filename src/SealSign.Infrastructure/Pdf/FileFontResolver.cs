using PdfSharpCore.Fonts;

namespace SealSign.Infrastructure.Pdf;

/// <summary>
/// 中文字体解析器：仅当配置了 TTF 路径时才安装。
/// family "CJK" 走配置的 TTF；其他字体委托给默认解析器（内置 Helvetica 等 base-14 字体）。
/// </summary>
public sealed class FileFontResolver : IFontResolver
{
    private readonly string _ttfPath;
    private readonly IFontResolver _fallback;

    public FileFontResolver(string ttfPath, IFontResolver fallback)
    {
        _ttfPath = ttfPath;
        _fallback = fallback;
    }

    public string DefaultFontName => _fallback.DefaultFontName;

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => familyName == "CJK"
            ? new FontResolverInfo("CJK")
            : _fallback.ResolveTypeface(familyName, isBold, isItalic);

    public byte[] GetFont(string faceName)
        => faceName == "CJK"
            ? File.ReadAllBytes(_ttfPath)
            : _fallback.GetFont(faceName);
}
