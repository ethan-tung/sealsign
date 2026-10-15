using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using QRCoder;

namespace SealSign.Infrastructure.Pdf;

/// <summary>盖章位置：页码（从 1 开始），坐标为毫米、从页面左上角起算。</summary>
public sealed record StampPlacement(int Page, double Xmm, double Ymm, double WidthMm, double? HeightMm = null);

/// <summary>盖章注记：编号/签署人/时间渲染在印章下方，二维码指向验真页。</summary>
public sealed record StampOptions(string RecordNo, string SignerName, DateTime SignedAtUtc, string? VerifyUrl);

/// <summary>把印章图片渲染进 PDF。注意：部分特殊 PDF 在 Modify 模式下可能打不开，见 README 已知限制。</summary>
public sealed class PdfStamper
{
    private const double MmToPt = 2.8346457;
    private readonly string _fontFamily;

    /// <param name="fontFamily">
    /// 注记字体。有中文字体时传 "CJK"（需配合 FileFontResolver 安装）；
    /// 否则传 "Helvetica" 等内置字体（ASCII 注记正常，中文显示方框）。
    /// </param>
    public PdfStamper(string fontFamily = "Helvetica") => _fontFamily = fontFamily;

    public int GetPageCount(Stream pdf)
    {
        using var doc = PdfReader.Open(pdf, PdfDocumentOpenMode.ReadOnly);
        return doc.PageCount;
    }

    public void Stamp(
        Stream pdfInput, Stream sealImage,
        IReadOnlyList<StampPlacement> placements, StampOptions options,
        Stream output)
    {
        using var doc = PdfReader.Open(pdfInput, PdfDocumentOpenMode.Modify);
        var sealBytes = ReadAll(sealImage);

        foreach (var p in placements)
        {
            if (p.Page < 1 || p.Page > doc.PageCount)
                throw new ArgumentOutOfRangeException(nameof(p.Page), $"页码 {p.Page} 超出范围");
            var page = doc.Pages[p.Page - 1];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            var wPt = p.WidthMm * MmToPt;
            var hPt = (p.HeightMm ?? p.WidthMm) * MmToPt; // 默认正方形
            var xPt = p.Xmm * MmToPt;
            var yPt = page.Height.Point - p.Ymm * MmToPt - hPt; // 输入是左上角坐标，PDF 是左下角

            // 印章图（支持 PNG 透明底）
            using var img = XImage.FromStream(() => new MemoryStream(sealBytes));
            gfx.DrawImage(img, xPt, yPt, wPt, hPt);

            // 注记：编号 / 时间 / 签署人（印章红）
            var font = new XFont(_fontFamily, 8, XFontStyle.Regular);
            var brush = new XSolidBrush(XColor.FromArgb(200, 30, 30));
            var tx = xPt;
            var ty = yPt + hPt + 5;
            gfx.DrawString(options.RecordNo, font, brush, new XPoint(tx, ty));
            gfx.DrawString(options.SignedAtUtc.ToString("yyyy-MM-dd HH:mm") + " UTC", font, brush, new XPoint(tx, ty + 11));
            if (!string.IsNullOrWhiteSpace(options.SignerName))
                gfx.DrawString(options.SignerName, font, brush, new XPoint(tx, ty + 22));

            // 二维码：扫码直达验真页
            if (!string.IsNullOrWhiteSpace(options.VerifyUrl))
            {
                var qrBytes = BuildQr(options.VerifyUrl);
                using var qrImg = XImage.FromStream(() => new MemoryStream(qrBytes));
                var qrSize = 22 * MmToPt;
                gfx.DrawImage(qrImg, xPt + wPt - qrSize, yPt + hPt + 5, qrSize, qrSize);
            }
        }

        doc.Save(output);
    }

    private static byte[] BuildQr(string text)
    {
        using var gen = new QRCodeGenerator();
        var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(10);
    }

    private static byte[] ReadAll(Stream s)
    {
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
