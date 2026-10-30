using System.Buffers.Binary;
using System.Text;

namespace OpenBudget.Api;

/// <summary>An image accepted by <see cref="TransactionImageValidator"/>; the format is detected from content, never from client metadata.</summary>
public sealed record ValidatedImage(string ContentType, string Extension, int Width, int Height, byte[] Content);

public sealed class ImageValidationException(string message) : Exception(message);

/// <summary>
/// Accepts only JPEG, PNG, WebP and GIF identified by magic bytes, checks the header-declared dimensions,
/// rejects markup in the leading bytes (polyglot defence in depth) and strips privacy metadata.
/// Pixel data is not decoded; browsers decode only after the authenticated, nosniff, sandboxed response.
/// </summary>
public static class TransactionImageValidator
{
    public const long MaxImageBytes = 10 * 1024 * 1024;
    public const int MaxDimension = 20_000;
    public const long MaxPixels = 100_000_000;
    private static readonly string[] MarkupMarkers = ["<html", "<script", "<svg", "<?xml", "<!doctype", "<body", "<iframe", "<object", "<embed"];

    public static ValidatedImage Validate(byte[] content)
    {
        if (content.Length == 0) throw new ImageValidationException("The image is empty.");
        if (content.Length > MaxImageBytes) throw new ImageValidationException("Each image must be 10 MB or smaller.");
        var format = Detect(content) ?? throw new ImageValidationException("Only JPEG, PNG, WebP and GIF images are supported.");
        var stripped = format.ContentType switch
        {
            "image/jpeg" => ImageMetadataStripper.StripJpeg(content),
            "image/png" => ImageMetadataStripper.StripPng(content),
            "image/webp" => ImageMetadataStripper.StripWebP(content),
            _ => content,
        };
        if (ContainsMarkup(stripped)) throw new ImageValidationException("Only JPEG, PNG, WebP and GIF images are supported.");
        var size = ReadDimensions(format.ContentType, stripped) ?? throw new ImageValidationException("The image is damaged or its dimensions could not be read.");
        if (size.Width < 1 || size.Height < 1 || size.Width > MaxDimension || size.Height > MaxDimension || (long)size.Width * size.Height > MaxPixels)
            throw new ImageValidationException("Image dimensions are outside the supported range.");
        return new ValidatedImage(format.ContentType, format.Extension, size.Width, size.Height, stripped);
    }

    public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return ("image/jpeg", ".jpg");
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ("image/png", ".png");
        if (data.Length >= 6 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8))) return ("image/gif", ".gif");
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8)) return ("image/webp", ".webp");
        return null;
    }

    public static bool ContainsMarkup(ReadOnlySpan<byte> data)
    {
        var head = Encoding.Latin1.GetString(data[..Math.Min(data.Length, 1024)]).ToLowerInvariant();
        return MarkupMarkers.Any(head.Contains);
    }

    public static (int Width, int Height)? ReadDimensions(string contentType, ReadOnlySpan<byte> data) => contentType switch
    {
        "image/png" => Png(data),
        "image/gif" => data.Length >= 10 ? (BinaryPrimitives.ReadUInt16LittleEndian(data[6..]), BinaryPrimitives.ReadUInt16LittleEndian(data[8..])) : null,
        "image/webp" => WebP(data),
        "image/jpeg" => Jpeg(data),
        _ => null,
    };

    private static (int, int)? Png(ReadOnlySpan<byte> data)
    {
        if (data.Length < 24 || BinaryPrimitives.ReadUInt32BigEndian(data[8..]) != 13 || !data[12..16].SequenceEqual("IHDR"u8)) return null;
        var width = BinaryPrimitives.ReadUInt32BigEndian(data[16..]); var height = BinaryPrimitives.ReadUInt32BigEndian(data[20..]);
        return width > int.MaxValue || height > int.MaxValue ? null : ((int)width, (int)height);
    }

    private static (int, int)? WebP(ReadOnlySpan<byte> data)
    {
        if (data.Length < 30) return null;
        var chunk = data[12..16];
        if (chunk.SequenceEqual("VP8X"u8))
            return (1 + (data[24] | data[25] << 8 | data[26] << 16), 1 + (data[27] | data[28] << 8 | data[29] << 16));
        if (chunk.SequenceEqual("VP8L"u8))
        {
            if (data[20] != 0x2F) return null;
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[21..]);
            return ((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }
        if (chunk.SequenceEqual("VP8 "u8))
        {
            if (data[23] != 0x9D || data[24] != 0x01 || data[25] != 0x2A) return null;
            return (BinaryPrimitives.ReadUInt16LittleEndian(data[26..]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(data[28..]) & 0x3FFF);
        }
        return null;
    }

    private static (int, int)? Jpeg(ReadOnlySpan<byte> data)
    {
        var i = 2;
        while (i + 4 <= data.Length)
        {
            if (data[i] != 0xFF) return null;
            var marker = data[i + 1];
            if (marker == 0xFF) { i++; continue; }
            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7)) { i += 2; continue; }
            if (marker is 0xD9 or 0xDA) return null;
            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(i + 2)..]);
            if (length < 2 || i + 2 + length > data.Length) return null;
            var isFrame = marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;
            if (isFrame)
            {
                if (length < 7) return null;
                return (BinaryPrimitives.ReadUInt16BigEndian(data[(i + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(data[(i + 5)..]));
            }
            i += 2 + length;
        }
        return null;
    }
}

/// <summary>
/// Removes privacy-sensitive metadata without re-encoding pixels: JPEG APP1 (EXIF/XMP, including GPS) and APP13 (IPTC),
/// PNG eXIf/tEXt/zTXt/iTXt/tIME chunks and WebP EXIF/XMP chunks. A JPEG EXIF orientation is preserved in a minimal
/// replacement APP1 segment so phone photos still display upright. GIF has no EXIF container and is returned unchanged.
/// </summary>
public static class ImageMetadataStripper
{
    public static byte[] StripJpeg(byte[] data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8) return data;
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 2);
        ushort? orientation = null;
        var wroteOrientation = false;
        var i = 2;
        while (i + 4 <= data.Length && data[i] == 0xFF)
        {
            var marker = data[i + 1];
            if (marker == 0xFF) { i++; continue; }
            if (marker is 0x01 or (>= 0xD0 and <= 0xD7)) { output.Write(data, i, 2); i += 2; continue; }
            if (marker == 0xD9) break;
            var length = (data[i + 2] << 8) | data[i + 3];
            if (length < 2 || i + 2 + length > data.Length) break;
            if (marker == 0xDA)
            {
                if (orientation is not null && !wroteOrientation) { output.Write(OrientationSegment(orientation.Value)); wroteOrientation = true; }
                // Start of scan: copy the entropy-coded data and everything after it verbatim.
                output.Write(data, i, data.Length - i);
                return output.ToArray();
            }
            if (marker == 0xE1)
            {
                orientation ??= ReadExifOrientation(data.AsSpan(i + 4, length - 2));
            }
            else if (marker != 0xED && marker != 0xFE)
            {
                // Keep JFIF/ICC/Adobe/quantisation/huffman/frame segments; drop APP1, APP13 and comments.
                if (orientation is not null && !wroteOrientation && marker is not (>= 0xE0 and <= 0xEF)) { output.Write(OrientationSegment(orientation.Value)); wroteOrientation = true; }
                output.Write(data, i, 2 + length);
            }
            i += 2 + length;
        }
        // Malformed structure: copy the rest unchanged so validation can still reject or accept on dimensions.
        if (i < data.Length) output.Write(data, i, data.Length - i);
        return output.ToArray();
    }

    public static ushort? ReadExifOrientation(ReadOnlySpan<byte> app1)
    {
        if (app1.Length < 14 || !app1[..6].SequenceEqual("Exif\0\0"u8)) return null;
        var tiff = app1[6..];
        bool little;
        if (tiff[0] == 'I' && tiff[1] == 'I') little = true; else if (tiff[0] == 'M' && tiff[1] == 'M') little = false; else return null;
        ushort U16(ReadOnlySpan<byte> s) => little ? BinaryPrimitives.ReadUInt16LittleEndian(s) : BinaryPrimitives.ReadUInt16BigEndian(s);
        uint U32(ReadOnlySpan<byte> s) => little ? BinaryPrimitives.ReadUInt32LittleEndian(s) : BinaryPrimitives.ReadUInt32BigEndian(s);
        var offset = U32(tiff[4..]);
        if (offset < 8 || offset + 2 > tiff.Length) return null;
        var count = U16(tiff[(int)offset..]);
        for (var n = 0; n < count; n++)
        {
            var entry = (int)offset + 2 + n * 12;
            if (entry + 12 > tiff.Length) return null;
            if (U16(tiff[entry..]) == 0x0112 && U16(tiff[(entry + 2)..]) == 3)
            {
                var value = U16(tiff[(entry + 8)..]);
                return value is >= 1 and <= 8 ? value : null;
            }
        }
        return null;
    }

    private static byte[] OrientationSegment(ushort orientation)
    {
        // APP1 "Exif\0\0" + big-endian TIFF header + IFD0 with a single Orientation (SHORT) entry.
        byte[] segment = [0xFF, 0xE1, 0x00, 0x22, (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'M', (byte)'M', 0x00, 0x2A, 0, 0, 0, 8,
            0x00, 0x01, 0x01, 0x12, 0x00, 0x03, 0, 0, 0, 1, (byte)(orientation >> 8), (byte)orientation, 0, 0,
            0, 0, 0, 0];
        return segment;
    }

    private static readonly HashSet<string> PngMetadataChunks = ["eXIf", "tEXt", "zTXt", "iTXt", "tIME"];

    public static byte[] StripPng(byte[] data)
    {
        if (data.Length < 8) return data;
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 8);
        var i = 8;
        while (i + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i));
            if (length > int.MaxValue || i + 12 + (long)length > data.Length) break;
            var type = Encoding.ASCII.GetString(data, i + 4, 4);
            var total = 12 + (int)length;
            if (!PngMetadataChunks.Contains(type)) output.Write(data, i, total);
            i += total;
            if (type == "IEND") return output.ToArray();
        }
        if (i < data.Length) output.Write(data, i, data.Length - i);
        return output.ToArray();
    }

    public static byte[] StripWebP(byte[] data)
    {
        if (data.Length < 20) return data;
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 12);
        var i = 12;
        var vp8xOffset = -1;
        while (i + 8 <= data.Length)
        {
            var type = Encoding.ASCII.GetString(data, i, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i + 4));
            var padded = size + (size & 1);
            if (size > int.MaxValue || i + 8 + (long)padded > data.Length)
            {
                output.Write(data, i, data.Length - i);
                i = data.Length;
                break;
            }
            if (type is not ("EXIF" or "XMP "))
            {
                if (type == "VP8X") vp8xOffset = (int)output.Position;
                output.Write(data, i, 8 + (int)padded);
            }
            i += 8 + (int)padded;
        }
        if (i < data.Length) output.Write(data, i, data.Length - i);
        var result = output.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)(result.Length - 8));
        if (vp8xOffset >= 0 && vp8xOffset + 9 <= result.Length) result[vp8xOffset + 8] &= unchecked((byte)~0x0C);
        return result;
    }
}

public static class ImageFileNameSanitizer
{
    public const int MaxLength = 100;

    /// <summary>Returns an ASCII-only display name (no paths, quotes or control characters) ending in the detected extension.</summary>
    public static string Sanitize(string? fileName, string extension)
    {
        var name = (fileName ?? "").Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        if (dot > 0) name = name[..dot];
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '(' or ')') builder.Append(c);
            else if (c is ' ' or '.') builder.Append(c);
        }
        var cleaned = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim('.', ' ');
        if (cleaned.Length == 0) cleaned = "image";
        var maxStem = MaxLength - extension.Length;
        if (cleaned.Length > maxStem) cleaned = cleaned[..maxStem].TrimEnd('.', ' ');
        return cleaned + extension;
    }
}
