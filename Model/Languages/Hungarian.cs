using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Reflection;
using QuickType.Model.Trie;

namespace QuickType.Model.Languages
{
    internal class Hungarian : BaseLanguage
    {
        internal Hungarian()
        {
            Priority = 10;
            Name = nameof(Hungarian);
            HasAccents = true;
            AccentDict = new Dictionary<char, List<char>>()
            {
                {'a', ['á'] },
                {'e', ['é'] },
                {'i', ['í'] },
                {'o', ['ó', 'ö', 'ő'] },
                {'u', ['ú', 'ü', 'ű'] }
            };
        }
    }
}
