using System.Text;

namespace SportsClubHub.Web.Services;

/// <summary>Builds CSV text for admin exports, guarding against spreadsheet formula injection.</summary>
public static class AdminCsv
{
    public static string Cell(string? value)
    {
        var text = value ?? string.Empty;

        // Spreadsheets may ignore leading whitespace before a formula marker, so inspect the
        // first non-whitespace character while retaining the original cell text.
        var firstContent = text.TrimStart();
        if (firstContent.Length > 0 && firstContent[0] is '=' or '+' or '-' or '@')
        {
            text = "'" + text;
        }

        return text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
    }

    public static string Build(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        builder.Append(string.Join(',', headers.Select(Cell))).Append("\r\n");
        foreach (var row in rows)
        {
            builder.Append(string.Join(',', row.Select(Cell))).Append("\r\n");
        }

        return builder.ToString();
    }
}
