// GenXY settings parser for the wiki generator.
//
// Two encodings appear in the data:
//  1. Settings files (e.g. $GameRoot/plantrules/*.txt): newline-separated "key=value" lines.
//  2. Inline options (entitydefaults.options, robottemplates.description): "#key=value#key2=value2".
//
// Value tokens (first character selects type + number base), per
// docs/content/claude_game_content_guide.md ("Options Metadata"):
//   n  int, decimal          N  int[], decimal, comma-separated
//   f  double, decimal       $  string
//   i  int, hex              L  long, hex
//
// Plant rules additionally support inheritance: a "source" key names another rule file
// whose values are overridden by the current file (see PlantRuleLoader.cs).
namespace Perpetuum.WikiGenerate;

public static class Genxy
{
    public static Dictionary<string, object> Parse(string text)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        // Two encodings coexist: inline "#k=v#k2=v2" and settings files with one "k=value" per line.
        // Split on both '#' and line breaks so either form tokenizes identically.
        foreach (var rawToken in text.Split(['#', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var token = rawToken.Trim();
            if (token.Length == 0) continue;
            var eq = token.IndexOf('=');
            if (eq <= 0) continue;
            var key = token[..eq].Trim();
            var value = token[(eq + 1)..].Trim();
            result[key] = ParseValue(key, value);
        }
        return result;
    }

    private static object ParseValue(string key, string value)
    {
        if (value.Length == 0) return string.Empty;
        switch (value[0])
        {
            case 'n':
                return int.Parse(value[1..], System.Globalization.CultureInfo.InvariantCulture);
            case 'N':
                return value[1..].Split(',').Select(p => int.Parse(p.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            case 'f':
                return double.Parse(value[1..], System.Globalization.CultureInfo.InvariantCulture);
            case 'i':
                return int.Parse(value[1..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            case 'L':
                return long.Parse(value[1..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            case '$':
                return value[1..];
            default:
                // Unknown token: keep the raw value so nothing is silently lost.
                return value;
        }
    }

    public static T? Get<T>(this Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) && v is T t ? t : default;

    public static T GetOrDefault<T>(this Dictionary<string, object> map, string key, T fallback) =>
        map.TryGetValue(key, out var v) && v is T t ? t : fallback;

    public static string FormatValue(object? v) => v switch
    {
        null => "",
        int[] arr => string.Join(",", arr),
        double d => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
        _ => v.ToString() ?? "",
    };
}
