using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Controller
{
    public static class StringExtension
    {
        //https://stackoverflow.com/questions/8809354/replace-first-occurrence-of-pattern-in-a-string
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0) return text;
            return text.Remove(pos, search.Length).Insert(pos, replace);
        }

        public static string RemoveFirst(this string text, string search)
        {
            int pos = text.IndexOf(search);
            if (pos < 0) return text;
            return text.Remove(pos, search.Length);
        }
    }
}
