using System.Text;

namespace Perpetuum.WikiGenerate;

/// <summary>
/// Minimal GBF (GXY2) archive reader that pulls the client's string dictionaries
/// straight out of the archive — no extraction step required.
/// Format: docs/file_formats/gbf_specification.md; reference parser: script/extract_gbf.py.
/// </summary>
public static class Gbf
{
    private static readonly byte[] Key =
        Enumerable.Range(0, 256).Select(i => (byte)(((i + 1) * (i ^ 0xCA) - 84) & 0xFF)).ToArray();

    /// <summary>The client's English string table, parsed from the lang0000/misc/dictionary
    /// entries in the archive. The archive bundles one dictionary per language; the English
    /// one is picked as the parse with the least non-ASCII content.</summary>
    public static Dictionary<string, string> LoadDictionary(string path)
    {
        Dictionary<string, string>? best = null;
        var bestScore = int.MaxValue;
        using var fs = File.OpenRead(path);

        var header = new byte[16];
        fs.ReadExactly(header, 0, 16);
        if (header[0] != 'G' || header[1] != 'X' || header[2] != 'Y' || header[3] != '2')
            throw new InvalidDataException($"not a GBF (GXY2) file: {path}");
        var payloadSize = BitConverter.ToInt32(header, 4);
        var tocSize = BitConverter.ToInt32(header, 12);

        // Multi-revision archives keep the original template header, so the TOC is not
        // always at file end: probe the same candidate offsets as the reference parser
        // (script/extract_gbf.py) and take the first that yields a sane entry count.
        var fileSize = fs.Length;
        foreach (var (offset, size) in new[]
                 {
                     (fileSize - (long)tocSize, (long)tocSize),
                     (payloadSize, tocSize),
                     (16L + payloadSize, tocSize),
                     (16L, tocSize),
                 })
        {
            if (offset < 0 || offset >= fileSize || size <= 0) continue;
            var len = (int)Math.Min(size, fileSize - offset);
            fs.Seek(offset, SeekOrigin.Begin);
            var toc = new byte[len];
            fs.ReadExactly(toc, 0, len);
            Xor(toc);
            var count = BitConverter.ToInt32(toc, 0);
            if (count <= 0 || count > 500_000) continue;

            int pos = 4;
            for (var i = 0; i < count && pos < toc.Length; i++)
            {
                int start = pos;
                while (pos < toc.Length && toc[pos] != 0) pos++;
                if (pos >= toc.Length) break;
                var name = Encoding.UTF8.GetString(toc, start, pos - start);
                pos++; // null terminator
                if (pos + 33 > toc.Length) break;
                var dataOffset = BitConverter.ToInt64(toc, pos);
                var dataLength = BitConverter.ToInt64(toc, pos + 8);
                pos += 33; // 4 x uint64 (offset, size, f3, version) + 1 flag byte

                if ((name == "dictionary" || name.StartsWith("dictionary_")) && dataLength > 0 && dataLength < 20_000_000)
                {
                    // The reference parser seeks the DataOffset absolutely (verified
                    // against Perpetuum.gbf) despite the spec saying payload-relative.
                    fs.Seek(dataOffset, SeekOrigin.Begin);
                    var data = new byte[dataLength];
                    fs.ReadExactly(data, 0, (int)dataLength);
                    Xor(data);
                    var candidate = new Dictionary<string, string>();
                    ParseDictionary(Encoding.UTF8.GetString(data), candidate);
                    var score = candidate.Values.Sum(v => v.Count(c => c > 0x7F));
                    if (score < bestScore) { bestScore = score; best = candidate; }
                }
            }
            return best ?? new Dictionary<string, string>();
        }
        throw new InvalidDataException($"no valid table of contents found in {path}");
    }

    /// <summary>Parses the GenXY-ish dictionary text: one <c>|key=$value</c> entry per line.</summary>
    internal static void ParseDictionary(string text, Dictionary<string, string> into)
    {
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("|")) continue;
            var eq = line.IndexOf("=$", 1);
            if (eq <= 0) continue;
            var key = line[1..eq];
            var value = Unescape(line[(eq + 2)..]);
            into[key] = value; // later (newer) revisions win
        }
    }

    private static string Unescape(string s) => s
        .Replace("\\2C", ",")
        .Replace("\\5B", "[")
        .Replace("\\5D", "]")
        .Replace("\\5C", "\\");

    private static void Xor(byte[] data)
    {
        for (var i = 0; i < data.Length; i++) data[i] ^= Key[i % 256];
    }
}
