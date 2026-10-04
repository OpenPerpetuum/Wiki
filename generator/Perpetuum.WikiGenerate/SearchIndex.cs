using System.Text.Json;

namespace Perpetuum.WikiGenerate;

/// <summary>
/// Builds wiki/static/search_index.json for the lightweight client search (wiki/static/search.js).
/// Scans every markdown page under wiki/content/, reads the front-matter title and description,
/// and computes the Zola URL path for each page (slug = filename with underscores turned into
/// hyphens; _index.md and index.md map to their section root).
/// </summary>
public static class SearchIndex
{
    public static void Build(string wikiRoot)
    {
        var contentRoot = Path.Combine(wikiRoot, "content");
        var entries = new List<Dictionary<string, string>>();
        foreach (var file in Directory.EnumerateFiles(contentRoot, "*.md", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(contentRoot, file);
            var parts = rel.Split(Path.DirectorySeparatorChar);
            var fileName = parts[^1];
            var isSectionPage = fileName is "_index.md" or "index.md";
            // Drop the file name from the segments; regular pages re-add it as the
            // page slug, section pages map to their section root.
            var urlParts = parts
                .Take(parts.Length - 1)
                .Select(p => p.Replace('_', '-'))
                .Concat(isSectionPage ? Enumerable.Empty<string>() : new[] { Path.GetFileNameWithoutExtension(fileName).Replace('_', '-') })
                .ToArray();
            var (title, description) = FrontMatter(File.ReadAllText(file));
            if (string.IsNullOrEmpty(title)) continue;
            var url = urlParts.Length == 0 ? "/" : "/" + string.Join("/", urlParts) + "/";
            entries.Add(new()
            {
                ["t"] = title,
                ["d"] = description,
                ["u"] = url
            });
        }

        var outPath = Path.Combine(wikiRoot, "static", "search_index.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(entries) + "\n");
        Console.WriteLine($"wrote static/search_index.json ({entries.Count} pages)");
    }

    private static (string Title, string Description) FrontMatter(string text)
    {
        if (!text.StartsWith("---\n")) return ("", "");
        var end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (end < 0) return ("", "");
        var (title, description) = ("", "");
        foreach (var line in text[4..end].Split('\n'))
        {
            if (line.StartsWith("title:")) title = Unquote(line["title:".Length..].Trim());
            else if (line.StartsWith("description:")) description = Unquote(line["description:".Length..].Trim());
        }
        return (title, description);
    }

    private static string Unquote(string s) => s.Length >= 2 && s.StartsWith('"') && s.EndsWith('"') ? s[1..^1] : s;
}
