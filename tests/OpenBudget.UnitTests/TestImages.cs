using System.Buffers.Binary;
using System.Text;

namespace OpenBudget.Tests;

/// <summary>Deterministic synthetic images shared by unit and integration tests.</summary>
public static class TestImages
{
    public static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
    public static readonly byte[] Gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
    public static readonly byte[] Svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

    public static byte[] PngWithSize(uint width, uint height)
    {
        var copy = (byte[])Png.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(copy.AsSpan(16), width);
        BinaryPrimitives.WriteUInt32BigEndian(copy.AsSpan(20), height);
        return copy;
    }

    public static byte[] PngWithChunk(string type, byte[] data)
    {
        var chunk = new byte[12 + data.Length];
        BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)data.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(chunk, 4);
        data.CopyTo(chunk, 8);
        // Insert after the IHDR chunk (8 signature + 25 IHDR bytes).
        return [.. Png[..33], .. chunk, .. Png[33..]];
    }

    /// <summary>A structurally valid JPEG header (not decodable pixels) with an EXIF APP1 carrying orientation and GPS bytes.</summary>
    public static byte[] Jpeg(int width = 640, int height = 480, ushort? orientation = 6, bool withGps = true)
    {
        using var stream = new MemoryStream();
        stream.Write([0xFF, 0xD8]);
        stream.Write([0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0]);
        if (orientation is not null || withGps)
        {
            var tiff = new List<byte> { (byte)'I', (byte)'I', 0x2A, 0, 8, 0, 0, 0 };
            var entries = new List<byte[]>();
            if (orientation is not null) entries.Add([0x12, 0x01, 3, 0, 1, 0, 0, 0, (byte)orientation, 0, 0, 0]);
            if (withGps) entries.Add([0x25, 0x88, 4, 0, 1, 0, 0, 0, 0, 0, 0, 0]);
            tiff.AddRange([(byte)entries.Count, 0]);
            foreach (var entry in entries) tiff.AddRange(entry);
            tiff.AddRange([0, 0, 0, 0]);
            if (withGps) tiff.AddRange(Encoding.ASCII.GetBytes("GPSLatitude=52.3676N;GPSLongitude=4.9041E"));
            var app1 = Encoding.ASCII.GetBytes("Exif\0\0").Concat(tiff).ToArray();
            stream.Write([0xFF, 0xE1, (byte)((app1.Length + 2) >> 8), (byte)(app1.Length + 2)]);
            stream.Write(app1);
        }
        var comment = Encoding.ASCII.GetBytes("Camera owner: Synthetic Person");
        stream.Write([0xFF, 0xFE, (byte)((comment.Length + 2) >> 8), (byte)(comment.Length + 2)]);
        stream.Write(comment);
        stream.Write([0xFF, 0xC0, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x03,
            0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01]);
        stream.Write([0xFF, 0xDA, 0x00, 0x0C, 0x03, 0x01, 0x00, 0x02, 0x11, 0x03, 0x11, 0x00, 0x3F, 0x00]);
        stream.Write([0x12, 0x34, 0x56, 0x78, 0xFF, 0x00, 0x9A]);
        stream.Write([0xFF, 0xD9]);
        return stream.ToArray();
    }

    /// <summary>A lossless WebP container (VP8L header) with optional EXIF and XMP chunks.</summary>
    public static byte[] WebP(int width = 300, int height = 200, bool withMetadata = true)
    {
        var chunks = new List<byte>();
        void Chunk(string type, byte[] data)
        {
            chunks.AddRange(Encoding.ASCII.GetBytes(type));
            var size = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)data.Length); chunks.AddRange(size);
            chunks.AddRange(data);
            if (data.Length % 2 == 1) chunks.Add(0);
        }
        var bits = (uint)(width - 1) | (uint)(height - 1) << 14;
        var vp8l = new byte[] { 0x2F, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        BinaryPrimitives.WriteUInt32LittleEndian(vp8l.AsSpan(1), bits);
        Chunk("VP8L", vp8l);
        if (withMetadata)
        {
            Chunk("EXIF", Encoding.ASCII.GetBytes("MM\0*GPSLatitude=52.3676N"));
            Chunk("XMP ", Encoding.ASCII.GetBytes("<x:xmpmeta>creator</x:xmpmeta>"));
        }
        var header = new byte[12];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)(4 + chunks.Count));
        Encoding.ASCII.GetBytes("WEBP").CopyTo(header, 8);
        return [.. header, .. chunks];
    }

    public static bool Contains(byte[] haystack, string text) => haystack.AsSpan().IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;
}
