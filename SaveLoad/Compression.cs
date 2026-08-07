using System.IO;
using System.IO.Compression;
using System.Text;

public static class Compression
{
    public static byte[] Compress(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source);

        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        using var brotliStream = new BrotliStream(output, CompressionLevel.Fastest);

        input.CopyTo(brotliStream);
        brotliStream.Flush();

        return output.ToArray();
    }

    public static string Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var brotliStream = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        brotliStream.CopyTo(output);
        brotliStream.Flush();

        return Encoding.UTF8.GetString(output.ToArray());
    }
}