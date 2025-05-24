using System;

namespace QuickType.Model;

public static class StringExtension
{
    public static string RemoveFirst(this string text, string search)
    {
        var pos = text.IndexOf(search, StringComparison.Ordinal);
        if (pos < 0) return text;
        return text.Remove(pos, search.Length);
    }
}