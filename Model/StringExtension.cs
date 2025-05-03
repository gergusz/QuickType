using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model
{
    public static class StringExtension
    {
        public static string RemoveFirst(this string text, string search)
        {
            int pos = text.IndexOf(search);
            if (pos < 0) return text;
            return text.Remove(pos, search.Length);
        }
    }
}
