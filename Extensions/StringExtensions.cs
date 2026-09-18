using System.Text.RegularExpressions;

public static class StringExtensions
{
    public static string Truncate(this string str, int length, bool useEllipsis = true)
    {
        if (string.IsNullOrEmpty(str)) return "";
        var trimmed = str.Trim();
        trimmed = trimmed.Substring(0, Math.Min(length, trimmed.Length));
        if (str.Length > length && useEllipsis) trimmed = trimmed.Substring(0, length - 2) + "...";
        return trimmed;
    }

    public static string ToNumeric(this string text)
    {
        var digitsRegex = new Regex(@"[^\d]");
        text = digitsRegex.Replace(text, "");
        if (string.IsNullOrWhiteSpace(text)) text = "0";
        return text;
    }
}