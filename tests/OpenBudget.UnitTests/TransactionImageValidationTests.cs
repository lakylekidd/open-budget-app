using System.Net;
using System.Text;
using OpenBudget.Api;
using OpenBudget.Tests;

namespace OpenBudget.UnitTests;

public sealed class TransactionImageValidationTests
{
    [Fact]
    public void Detects_supported_formats_from_magic_bytes_only()
    {
        Assert.Equal("image/png", TransactionImageValidator.Detect(TestImages.Png)?.ContentType);
        Assert.Equal("image/gif", TransactionImageValidator.Detect(TestImages.Gif)?.ContentType);
        Assert.Equal("image/jpeg", TransactionImageValidator.Detect(TestImages.Jpeg())?.ContentType);
        Assert.Equal("image/webp", TransactionImageValidator.Detect(TestImages.WebP())?.ContentType);
        Assert.Null(TransactionImageValidator.Detect(TestImages.Svg));
        Assert.Null(TransactionImageValidator.Detect("%PDF-1.7"u8));
        Assert.Null(TransactionImageValidator.Detect("<html><body>hi</body></html>"u8));
        Assert.Null(TransactionImageValidator.Detect("BM6\0\0\0"u8));
        Assert.Null(TransactionImageValidator.Detect("RIFF\0\0\0\0WAVEfmt "u8));
        Assert.Null(TransactionImageValidator.Detect([0x89, 0x50, 0x4E]));
        Assert.Null(TransactionImageValidator.Detect([]));
    }

    [Theory]
    [InlineData("png")]
    [InlineData("gif")]
    [InlineData("jpeg")]
    [InlineData("webp")]
    public void Accepts_valid_images_and_reads_dimensions(string kind)
    {
        var (bytes, type, width, height) = kind switch
        {
            "png" => (TestImages.Png, "image/png", 1, 1),
            "gif" => (TestImages.Gif, "image/gif", 1, 1),
            "jpeg" => (TestImages.Jpeg(640, 480), "image/jpeg", 640, 480),
            _ => (TestImages.WebP(300, 200), "image/webp", 300, 200),
        };
        var image = TransactionImageValidator.Validate(bytes);
        Assert.Equal(type, image.ContentType);
        Assert.Equal((width, height), (image.Width, image.Height));
    }

    [Fact]
    public void Rejects_svg_html_empty_oversized_and_damaged_content()
    {
        Assert.Throws<ImageValidationException>(() => TransactionImageValidator.Validate(TestImages.Svg));
        Assert.Throws<ImageValidationException>(() => TransactionImageValidator.Validate(Encoding.UTF8.GetBytes("<!DOCTYPE html><script>alert(1)</script>")));
        Assert.Throws<ImageValidationException>(() => TransactionImageValidator.Validate([]));
        var huge = new byte[TransactionImageValidator.MaxImageBytes + 1]; TestImages.Png.CopyTo(huge, 0);
        Assert.Contains("10 MB", Assert.Throws<ImageValidationException>(() => TransactionImageValidator.Validate(huge)).Message);
        Assert.Throws<ImageValidationException>(() => TransactionImageValidator.Validate(TestImages.Png[..20]));
        Assert.Throws<ImageValidationException>(() => TransactionImageValidator.Validate([0xFF, 0xD8, 0xFF, 0xD9]));
    }

    [Fact]
    public void Rejects_polyglots_that_start_with_image_magic_but_carry_markup()
    {
        var gifHtml = Encoding.Latin1.GetBytes("GIF89a\x01\x00\x01\x00<html><script>alert(document.cookie)</script></html>");
        Assert.Equal("image/gif", TransactionImageValidator.Detect(gifHtml)?.ContentType);
        Assert.Throws<ImageValidationException>(() => TransactionImageValidator.Validate(gifHtml));
        var pngSvg = TestImages.PngWithChunk("prVt", Encoding.ASCII.GetBytes("<svg onload=alert(1)>"));
        Assert.Throws<ImageValidationException>(() => TransactionImageValidator.Validate(pngSvg));
    }

    [Theory]
    [InlineData(0u, 10u)]
    [InlineData(10u, 0u)]
    [InlineData(20_001u, 10u)]
    [InlineData(15_000u, 15_000u)]
    [InlineData(uint.MaxValue, 1u)]
    public void Rejects_out_of_range_dimensions(uint width, uint height) =>
        Assert.Throws<ImageValidationException>(() => TransactionImageValidator.Validate(TestImages.PngWithSize(width, height)));

    [Fact]
    public void Jpeg_stripping_removes_exif_gps_and_comments_but_keeps_orientation_and_pixels()
    {
        var original = TestImages.Jpeg(orientation: 6, withGps: true);
        Assert.True(TestImages.Contains(original, "GPSLatitude"));
        var image = TransactionImageValidator.Validate(original);
        Assert.False(TestImages.Contains(image.Content, "GPSLatitude"));
        Assert.False(TestImages.Contains(image.Content, "Synthetic Person"));
        Assert.True(TestImages.Contains(image.Content, "JFIF"));
        Assert.Equal((ushort)6, ExifOrientation(image.Content));
        Assert.Equal((640, 480), (image.Width, image.Height));
        // Entropy-coded scan data and the end marker are preserved verbatim.
        Assert.Equal(original[^9..], image.Content[^9..]);
    }

    [Fact]
    public void Jpeg_without_orientation_gets_no_exif_segment()
    {
        var stripped = ImageMetadataStripper.StripJpeg(TestImages.Jpeg(orientation: null, withGps: true));
        Assert.False(TestImages.Contains(stripped, "Exif"));
        Assert.False(TestImages.Contains(stripped, "GPSLatitude"));
    }

    [Fact]
    public void Png_and_webp_metadata_chunks_are_removed()
    {
        var png = TestImages.PngWithChunk("tEXt", Encoding.ASCII.GetBytes("Author\0Synthetic Person"));
        var cleanPng = TransactionImageValidator.Validate(png).Content;
        Assert.False(TestImages.Contains(cleanPng, "Synthetic Person"));
        Assert.Equal(TestImages.Png, cleanPng);

        var webp = TestImages.WebP(withMetadata: true);
        var cleanWebp = TransactionImageValidator.Validate(webp).Content;
        Assert.False(TestImages.Contains(cleanWebp, "GPSLatitude"));
        Assert.False(TestImages.Contains(cleanWebp, "xmpmeta"));
        Assert.Equal(TestImages.WebP(withMetadata: false), cleanWebp);
    }

    [Theory]
    [InlineData("receipt.jpg", ".jpg", "receipt.jpg")]
    [InlineData("../../etc/passwd", ".png", "passwd.png")]
    [InlineData("C:\\Users\\someone\\Desktop\\scan 1.PNG", ".png", "scan 1.png")]
    [InlineData("evil\".jpg\r\nX-Header: 1", ".jpg", "evil.jpg")]
    [InlineData("invoice.svg", ".png", "invoice.png")]
    [InlineData("", ".gif", "image.gif")]
    [InlineData(null, ".webp", "image.webp")]
    [InlineData("....", ".jpg", "image.jpg")]
    [InlineData("café 😀 rëceipt.jpeg", ".jpg", "caf rceipt.jpg")]
    public void Sanitizes_display_file_names(string? input, string extension, string expected)
    {
        var name = ImageFileNameSanitizer.Sanitize(input, extension);
        Assert.Equal(expected, name);
        Assert.DoesNotContain('"', name); Assert.DoesNotContain('/', name); Assert.DoesNotContain('\\', name);
        Assert.True(name.Length <= ImageFileNameSanitizer.MaxLength);
    }

    [Fact]
    public void Truncates_long_file_names_and_keeps_detected_extension()
    {
        var name = ImageFileNameSanitizer.Sanitize(new string('a', 500) + ".exe", ".png");
        Assert.Equal(ImageFileNameSanitizer.MaxLength, name.Length);
        Assert.EndsWith(".png", name);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.255.255.254")]
    [InlineData("0.0.0.0")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("169.254.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("224.0.0.1")]
    [InlineData("239.255.255.250")]
    [InlineData("255.255.255.255")]
    [InlineData("240.0.0.1")]
    [InlineData("192.0.0.170")]
    [InlineData("192.0.2.1")]
    [InlineData("198.18.0.1")]
    [InlineData("198.51.100.7")]
    [InlineData("203.0.113.9")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::ffff:10.1.2.3")]
    [InlineData("::ffff:192.168.0.1")]
    [InlineData("::127.0.0.1")]
    [InlineData("64:ff9b::a9fe:a9fe")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456:789a::1")]
    [InlineData("fe80::1")]
    [InlineData("fe80::1%1")]
    [InlineData("fec0::1")]
    [InlineData("ff02::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2001::1")]
    [InlineData("2002:a9fe:a9fe::1")]
    [InlineData("fd00:ec2::254")]
    public void Blocks_internal_and_special_addresses(string address) =>
        Assert.True(SsrfAddressPolicy.IsBlocked(IPAddress.Parse(address)), address);

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.215.14")]
    [InlineData("172.32.0.1")]
    [InlineData("172.15.255.255")]
    [InlineData("100.128.0.1")]
    [InlineData("169.255.0.1")]
    [InlineData("192.169.0.1")]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("2a00:1450:4001:80b::200e")]
    public void Allows_public_unicast_addresses(string address) =>
        Assert.False(SsrfAddressPolicy.IsBlocked(IPAddress.Parse(address)), address);

    [Theory]
    [InlineData("http://example.com/a.png", "scheme")]
    [InlineData("ftp://example.com/a.png", "scheme")]
    [InlineData("file:///etc/passwd", "scheme")]
    [InlineData("https://user:pass@example.com/a.png", "credentials")]
    [InlineData("https://example.com:8443/a.png", "port")]
    [InlineData("https://127.0.0.1/a.png", "blocked-address")]
    [InlineData("https://[::1]/a.png", "blocked-address")]
    [InlineData("https://169.254.169.254/latest/meta-data", "blocked-address")]
    [InlineData("https://[::ffff:a9fe:a9fe]/", "blocked-address")]
    [InlineData("https://example.com/receipt.png?token=abc", null)]
    public void Validates_import_urls_before_any_connection(string url, string? problem) =>
        Assert.Equal(problem, SafeTransactionImageUrlFetcher.ValidateUrl(new Uri(url)));

    private static ushort? ExifOrientation(byte[] jpeg)
    {
        for (var i = 2; i + 4 < jpeg.Length && jpeg[i] == 0xFF;)
        {
            var length = jpeg[i + 2] << 8 | jpeg[i + 3];
            if (jpeg[i + 1] == 0xE1) return ImageMetadataStripper.ReadExifOrientation(jpeg.AsSpan(i + 4, length - 2));
            i += 2 + length;
        }
        return null;
    }
}
