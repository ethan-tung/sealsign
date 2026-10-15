using Xunit;
using PdfSharpCore.Pdf;
using SealSign.Core;
using SealSign.Infrastructure.Pdf;

namespace SealSign.Tests;

public sealed class PdfStamperTests
{
    // 1x1 红色 PNG（测试用印章图占位）
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    public PdfStamperTests()
    {
        // 无中文字体时用内置 Helvetica（ASCII 注记不受影响）
        _stamper = new PdfStamper("Helvetica");
    }

    private readonly PdfStamper _stamper;

    private static byte[] CreateBlankPdf(int pages = 2)
    {
        using var doc = new PdfDocument();
        for (var i = 0; i < pages; i++)
            doc.AddPage();
        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Stamp_AddsSeal_AndChangesHash()
    {
        var original = CreateBlankPdf();
        var stamper = _stamper;

        using var input = new MemoryStream(original);
        using var seal = new MemoryStream(Convert.FromBase64String(TinyPngBase64));
        using var output = new MemoryStream();
        stamper.Stamp(
            input, seal,
            new List<StampPlacement> { new(1, 20, 20, 30, 30), new(2, 20, 20, 30) },
            new StampOptions("SN-TEST-001", "Tester", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), null),
            output);

        var stamped = output.ToArray();
        Assert.True(stamped.Length > original.Length);
        Assert.Equal(2, stamper.GetPageCount(new MemoryStream(stamped)));
        Assert.NotEqual(HashChain.Sha256Hex(original), HashChain.Sha256Hex(stamped));
    }

    [Fact]
    public void Stamp_InvalidPage_Throws()
    {
        var original = CreateBlankPdf(pages: 1);
        var stamper = _stamper;
        using var input = new MemoryStream(original);
        using var seal = new MemoryStream(Convert.FromBase64String(TinyPngBase64));
        using var output = new MemoryStream();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            stamper.Stamp(input, seal,
                new List<StampPlacement> { new(99, 20, 20, 30) },
                new StampOptions("SN-TEST-002", "Tester", DateTime.UtcNow, null),
                output));
    }
}
