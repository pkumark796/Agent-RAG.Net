using System.Globalization;
using System.Text;

namespace Rag.Console.Services;

internal static class TextSanitizer
{
    public static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);

        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);

            if (category == UnicodeCategory.Control &&
                rune.Value is not (13 or 10 or 9))
            {
                continue;
            }

            builder.Append(rune.ToString());
        }

        return builder.ToString();
    }
}