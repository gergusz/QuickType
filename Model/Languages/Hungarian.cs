using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using QuickType.Model.Trie;
using System.Reflection;

namespace QuickType.Model.Languages
{
    internal partial class Hungarian : ILanguage
    {
        private ITrie Trie { get; set; }
        public string Name => "Hungarian";

        [GeneratedRegex(@"([a-z]+)|([A-Z]+)|([áéóöőúüűí]+)|([ÁÉÓÖŐÚÜŰÍ]+)")]
        private static partial Regex ValidCharsRegex();

        public Hungarian(string? path = null)
        {
            path ??= $@"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickType")}\{Name}.db";
            Trie = new HybridTrie($@"Data Source={path}");
        }

        public List<Word> SearchByPrefix(string word, int amount = 5)
        {
            return Trie.SearchByPrefix(word, amount);
        }

        public void Insert(string word, int frequency)
        {
            Trie.Insert(word, frequency);
        }

    }
}
